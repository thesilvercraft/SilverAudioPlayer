using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Jellyfin.Sdk;
using Jellyfin.Sdk.Generated.Models;
using Serilog;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.Metadata;
using SilverCraft.AvaloniaUtils;

namespace SilverAudioPlayer.Any.PlayStreamProvider.JellyFin;

public partial class Gui : Window
{
    private readonly GuiBinding g;
    private JellyFinHelper helper;
    private TextBox? _searchBox;
    public Gui()
    {
    InitializeComponent();
#if DEBUG
    this.AttachDevTools();
#endif
    helper = JellyFinPlayStreamProvider.GetJellyFinHelper();
    Opened += Gui_Opened;
    g = new GuiBinding();
    DataContext = g;
    this.DoAfterInitTasks(true);
    LB = this.FindControl<ListBox>("LB");
    _searchBox = this.FindControl<TextBox>("SearchBox");
    }
    private async void SearchButton_Click(object? sender, RoutedEventArgs e)
    {
        await DoSearch();
    }

    private async void SearchBox_KeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await DoSearch();
        }
    }

    private async Task DoSearch()
    {
        if (_searchBox == null || string.IsNullOrWhiteSpace(_searchBox.Text))
            return;
        Cursor = new Cursor(StandardCursorType.Wait);
        var results = await helper.SearchItems(_searchBox.Text.Trim());
        var o = new ObservableCollection<WrappedDto>(results.Select(x => new WrappedDto(x)));
        g.SetResults(o);
        LB.ItemsSource = g.SearchResults;
        await DownloadPictures(o);
        
        LB.InvalidateVisual();
        Cursor = Cursor.Default;
    }

    async Task DownloadPictures(IEnumerable<WrappedDto> d)
    {
        foreach (var u in d)
        {
            if (await helper.GetImageStreamAndId(u.dto) is not { Item1: { } wrappedStream, Item2: { } id }) continue;
            var stream = wrappedStream?.GetStream();
            if (stream == null) continue;
            Debug.WriteLine("Not a null stream?");
            try
            {
                u.Cover = Bitmap.DecodeToHeight(stream, 400);
            }
            catch (Exception exception)
            {
                Log.Error(exception, "Error occured while reading bitmap");
            }
            finally
            {
               // await stream.DisposeAsync();
              //  wrappedStream.Dispose();
            }
        }
    }
    private IPlayStreamProviderListener ProviderListner;

    public Gui(IPlayStreamProviderListener jellyFinPlayStreamProvider) : this()
    {
        ProviderListner = jellyFinPlayStreamProvider;
    }

    private async void Button_Click(object? sender, RoutedEventArgs e)
    {
        g.SetResults( new ObservableCollection<WrappedDto>((await helper.GetDefaultViews())
            .OrderBy(x => x.IndexNumber).Select(x => new WrappedDto(x))));
        LB.ItemsSource = g.SearchResults;
        LB.InvalidateVisual();
    }

    private async void BackButton_Click(object? sender, RoutedEventArgs e)
    {
        if (back != null)
        {
            await SetDtoFolderView(back);
        }
    }

    private async void AddEntireScreen(object? sender, RoutedEventArgs e)
    {
        Cursor = new Cursor(StandardCursorType.Wait);
        List<Tuple<WrappedStream, IMetadata>> streams = [];

        async Task Add(IEnumerable<WrappedDto> dtos)
        {
            foreach (var song in dtos.OrderBy(x => x.IndexNumber))
            {
                var ws = await helper.GetStream(song.dto);
                var lyrics = await helper.GetLyrics(song.dto);
                streams.Add(new(ws, new JellyfinMeta(song.dto,lyrics)));
            }
        }

        List<Task> tasks = [];
        var groups = g.SearchResults.GroupBy(x => x.IsFolder);
        foreach (var group in groups)
        {
            if (group.Key == true)
            {
                foreach (var folder in group)
                {
                    var o = (await helper.GetItemsFromItem(folder.dto)).Select(x => new WrappedDto(x));
                    tasks.Add(Add(o));
                }
            }
            else
            {
                tasks.Add(Add(group));
            }
        }

        await Task.WhenAll(tasks);
        ProviderListner.LoadSongs(streams);
        Cursor = Cursor.Default;
    }

    private async void Gui_Opened(object? sender, EventArgs e)
    {
        if (helper == null) return;
        await Task.Delay(100);
        await helper.MakeSureUserLogsIn(this);
        g.SetResults(new ObservableCollection<WrappedDto>((await helper.GetDefaultViews()).Select(x => new WrappedDto(x))));
        
        LB.ItemsSource = g.SearchResults;
        LB.InvalidateVisual();
        LB.DoubleTapped += LB_DoubleTapped;
    }

    async Task SetDtoFolderView(WrappedDto si)
    {
        Cursor = new Cursor(StandardCursorType.Wait);

    var o = (await helper.GetItemsFromItem(si.dto)).Select(x => new WrappedDto(x));

        g.SetResults(new ObservableCollection<WrappedDto>(o));
        LB.ItemsSource = g.SearchResults;
        await DownloadPictures(o);
        
        LB.InvalidateVisual();
        back = current;
        current = si;
        Cursor = Cursor.Default;
    }

    private WrappedDto? back;
    private WrappedDto? current;

    private async void LB_DoubleTapped(object? sender, RoutedEventArgs e)
    {
        if (LB.SelectedItem is not WrappedDto si) return;
        if (si.IsFolder == true)
        {
            await SetDtoFolderView(si);
        }
        else
        {
            ProviderListner.LoadSong(await helper.GetStream(si.dto), new JellyfinMeta(si.dto, await helper.GetLyrics(si.dto)));
        }
    }
}