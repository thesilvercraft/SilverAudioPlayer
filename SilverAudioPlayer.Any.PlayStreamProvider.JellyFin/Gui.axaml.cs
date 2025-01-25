using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Jellyfin.Sdk;
using Jellyfin.Sdk.Generated.Models;
using Serilog;
using SilverAudioPlayer.Shared;
using SilverCraft.AvaloniaUtils;

namespace SilverAudioPlayer.Any.PlayStreamProvider.JellyFin;

public partial class Gui : Window
{
    private readonly GuiBinding g;
    private readonly JellyFinHelper helper;

    public Gui()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
        Opened += Gui_Opened;
        g = new GuiBinding();
        DataContext = g;
        this.DoAfterInitTasks(true);
        LB = this.FindControl<ListBox>("LB");
    }

    private IPlayStreamProviderListener ProviderListner;

    public Gui(IPlayStreamProviderListener jellyFinPlayStreamProvider) : this()
    {
        ProviderListner = jellyFinPlayStreamProvider;
        helper = new JellyFinHelper();
    }

    private async void Button_Click(object? sender, RoutedEventArgs e)
    {
        g.SearchResults = new ObservableCollection<WrappedDto>(new List<BaseItemDto>(await helper.GetDefaultViews())
            .OrderBy(x => x.IndexNumber).Select(x => new WrappedDto(x)));
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
        List<WrappedStream> streams = [];

        async Task Add(IEnumerable<WrappedDto> dtos)
        {
            foreach (var song in dtos.OrderBy(x => x.IndexNumber))
            {
                var ws = await helper.GetStream(song.dto);
                streams.Add(ws);
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
        g.SearchResults =
            new ObservableCollection<WrappedDto>(
                new List<BaseItemDto>(await helper.GetDefaultViews()).Select(x => new WrappedDto(x)));
        LB.ItemsSource = g.SearchResults;
        LB.InvalidateVisual();
        LB.DoubleTapped += LB_DoubleTapped;
    }

    async Task SetDtoFolderView(WrappedDto si)
    {
        Cursor = new Cursor(StandardCursorType.Wait);

        var o = (await helper.GetItemsFromItem(si.dto)).Select(x => new WrappedDto(x));
        g.SearchResults = new ObservableCollection<WrappedDto>(o);
        foreach (var u in g.SearchResults)
        {
            var wrappedStream = await helper.GetImageStream(u.dto);
            var stream = wrappedStream?.GetStream();
            if (stream == null) continue;
            try
            {
                u.Cover = Bitmap.DecodeToHeight(stream, 200);
            }
            catch (Exception exception)
            {
                Log.Error(exception, "Error occured while reading bitmap");
                
            }
            finally
            {
                await stream.DisposeAsync();
            }
        }

        LB.ItemsSource = g.SearchResults;
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
            ProviderListner.LoadSong(await helper.GetStream(si.dto));
        }
    }
}