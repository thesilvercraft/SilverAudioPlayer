using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Jellyfin.Sdk.Generated.Models;
using Serilog;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.Metadata;
using SilverCraft.AvaloniaUtils;

namespace SilverAudioPlayer.Any.PlayStreamProvider.JellyFin;

public partial class Gui : Window
{
    private readonly GuiBinding g;

    private CancellationTokenSource? _imageCts;
    private readonly TextBox? _searchBox;

    private readonly Stack<WrappedDto> backStack = new();
    private WrappedDto? current;
    private readonly JellyFinHelper helper;

    private readonly IPlayStreamProviderListener ProviderListner;

    public Gui(IPlayStreamProviderListener jellyFinPlayStreamProvider, JellyFinHelper helper)
    {
        InitializeComponent();
        ProviderListner = jellyFinPlayStreamProvider;
        this.helper = helper; 
        Opened += Gui_Opened;
        g = new GuiBinding();
        DataContext = g;
        this.DoAfterInitTasks(true);
        LB = this.FindControl<ListBox>("LB");
        _searchBox = this.FindControl<TextBox>("SearchBox");
        LB.DoubleTapped += LB_DoubleTapped;
        Closed += OnClosed;
    }
  

    protected void OnClosed(object? o, EventArgs e)
    {
        g.SetResults([]);
        while (backStack.Count > 0) backStack.Pop().Dispose();
        _imageCts?.Cancel();
        _imageCts?.Dispose();
        current?.Dispose();
        GC.Collect();
    }

    private async void SearchButton_Click(object? sender, RoutedEventArgs e)
    {
        await DoSearch();
    }

    private async void SearchBox_KeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) await DoSearch();
    }

    private async Task DoSearch()
    {
        if (_searchBox == null || string.IsNullOrWhiteSpace(_searchBox.Text))
            return;
        Cursor = new Cursor(StandardCursorType.Wait);
        var results = await helper.SearchItems(_searchBox.Text.Trim());
        var o = new ObservableCollection<WrappedDto>(results.Select(x => new WrappedDto(x)));
        g.SetResults(o);
        _imageCts?.Cancel();
        _imageCts?.Dispose();
        _imageCts = new CancellationTokenSource();
        await DownloadPictures(_imageCts.Token);

        LB.InvalidateVisual();
        Cursor = Cursor.Default;
    }

    private async Task DownloadPictures(CancellationToken token)
    {
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = 3
        };
        await Parallel.ForEachAsync(g.SearchResults, options, async (u, y) =>
        {
            if( token.IsCancellationRequested) return;
            var result = await helper.GetImageStreamAndId(u.dto);
            if (result == null || token.IsCancellationRequested) return;

            var (wrappedStream, id) = result;
            var stream = wrappedStream?.GetStream();
            if (stream == null)
            {
                Debug.WriteLine($"Image stream is null for: {u.Name} (ID: {u.dto.Id})");
                return;
            }

            try
            {
                var bmp = Bitmap.DecodeToHeight(stream, 200);
                if (token.IsCancellationRequested)
                {
                    bmp.Dispose();
                    return;
                }

                u.Cover = bmp;
            }
            catch (Exception exception)
            {
                Log.Error(exception, $"Error occured while reading bitmap for: {u.Name} (ID: {u.dto.Id})");
            }
            finally
            {
                stream.Dispose();
                wrappedStream?.Dispose();
            }
        });
    }

    private async void Button_Click(object? sender, RoutedEventArgs e)
    {
        g.SetResults(new ObservableCollection<WrappedDto>((await helper.GetDefaultViews())
            .OrderBy(x => x.IndexNumber).Select(x => new WrappedDto(x))));
    }

    private async void BackButton_Click(object? sender, RoutedEventArgs e)
    {
        if (backStack.Count <= 0) return;
        var previous = backStack.Pop();
        await SetDtoFolderView(previous, true);
    }

    private async Task GetSongStreamUrls(IEnumerable<WrappedDto> dtos,
        ConcurrentBag<Tuple<WrappedStream, IMetadata?>> streams)
    {
        foreach (var song in dtos.OrderBy(x => x.IndexNumber))
        {
            var ws = await helper.GetStream(song.dto);
            var lyrics = await helper.GetLyrics(song.dto);
            streams.Add(new Tuple<WrappedStream, IMetadata?>(ws, new JellyfinMeta(song.dto, lyrics)));
        }
    }

    private async Task GetSongStreamUrls(IEnumerable<BaseItemDto> dtos,
        ConcurrentBag<Tuple<WrappedStream, IMetadata>> streams)
    {
        foreach (var dto in dtos.OrderBy(x => x.IndexNumber))
        {
            var ws = await helper.GetStream(dto);
            var lyrics = await helper.GetLyrics(dto);
            streams.Add(new Tuple<WrappedStream, IMetadata>(ws, new JellyfinMeta(dto, lyrics)));
        }
    }

    private async void AddEntireScreen(object? sender, RoutedEventArgs e)
    {
        Cursor = new Cursor(StandardCursorType.Wait);
        ConcurrentBag<Tuple<WrappedStream, IMetadata?>> streams = [];
        List<Task> tasks = [];
        var groups = g.SearchResults.GroupBy(x => x.IsFolder);
        foreach (var group in groups)
            if (group.Key == true)
                foreach (var folder in group)
                {
                    var o = (await helper.GetItemsFromItem(folder.dto)).Select(x => new WrappedDto(x));
                    tasks.Add(GetSongStreamUrls(o, streams));
                }
            else
                tasks.Add(GetSongStreamUrls(group, streams));

        await Task.WhenAll(tasks);
        ProviderListner.LoadSongs(streams);
        Cursor = Cursor.Default;
    }

    private async void Gui_Opened(object? sender, EventArgs e)
    {
        Opened -= Gui_Opened;
        if (helper == null) return;
        await Task.Delay(100);
        await helper.MakeSureUserLogsIn(this);
        g.SetResults(
            new ObservableCollection<WrappedDto>((await helper.GetDefaultViews()).Select(x => new WrappedDto(x))));
    }

    private async Task SetDtoFolderView(WrappedDto si)
    {
        await SetDtoFolderView(si, false);
    }

    private async Task SetDtoFolderView(WrappedDto si, bool isBackNavigation)
    {
        Cursor = new Cursor(StandardCursorType.Wait);
        var o = (await helper.GetItemsFromItem(si.dto)).Select(x => new WrappedDto(x));
        g.SetResults(new ObservableCollection<WrappedDto>(o));
        _imageCts?.Cancel();
        _imageCts?.Dispose();
        _imageCts = new CancellationTokenSource();
        await DownloadPictures(_imageCts.Token);
        if (!isBackNavigation && current != null) backStack.Push(current);

        current = si;
        Cursor = Cursor.Default;
    }

    private async void LB_DoubleTapped(object? sender, RoutedEventArgs e)
    {
        if (LB.SelectedItem is not WrappedDto si) return;
        if (si.IsFolder == true)
            await SetDtoFolderView(si);
        else
            ProviderListner.LoadSong(await helper.GetStream(si.dto),
                new JellyfinMeta(si.dto, await helper.GetLyrics(si.dto)));
    }

    private async void InstantMix_Click(object? sender, RoutedEventArgs e)
    {
        if (LB.SelectedItem is not WrappedDto si) return;

        Cursor = new Cursor(StandardCursorType.Wait);

        ConcurrentBag<Tuple<WrappedStream, IMetadata>> streams = [];
        var instantMix = await helper.InstantMix(si.dto);
        await GetSongStreamUrls(instantMix, streams);
        ProviderListner.LoadSongs(streams);

        Cursor = Cursor.Default;
    }
}