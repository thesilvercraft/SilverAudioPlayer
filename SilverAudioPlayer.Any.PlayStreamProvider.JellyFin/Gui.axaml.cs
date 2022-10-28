using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Jellyfin.Sdk;
using SilverAudioPlayer.Shared;

namespace SilverAudioPlayer.Any.PlayStreamProvider.JellyFin;

public partial class Gui : Window
{
    private readonly GuiBinding g;
    private readonly JellyFinHelper helper;
    private readonly JellyFinPlayStreamProvider jellyFinPlayStreamProvider;

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
        LB.DoubleTapped += LB_DoubleTapped;
    }

    public Gui(JellyFinPlayStreamProvider jellyFinPlayStreamProvider) : this()
    {
        this.jellyFinPlayStreamProvider = jellyFinPlayStreamProvider;
        helper = new JellyFinHelper();
    }

    private async void Button_Click(object? sender, RoutedEventArgs e)
    {
        g.SearchResults = new ObservableCollection<WrappedDto>(new List<BaseItemDto>(await helper.GetDefaultItems())
            .OrderBy(x => x.IndexNumber).Select(x => new WrappedDto(x)));
        LB.Items = g.SearchResults;
        Debug.WriteLine(g.SearchResults.Count);
        LB.InvalidateVisual();
    }

    private async void AddEntireScreen(object? sender, RoutedEventArgs e)
    {
        foreach (var song in g.SearchResults.Where(x => x.IsFolder != true).OrderBy(x => x.IndexNumber))
        {
            var ws = await helper.GetStream(song.dto);

            jellyFinPlayStreamProvider.ProviderListner.LoadSong(ws);
        }
    }

    private async void Gui_Opened(object? sender, EventArgs e)
    {
        if (helper != null)
        {
            await helper.MakeSureUserLogsIn(this);
            g.SearchResults =
                new ObservableCollection<WrappedDto>(
                    new List<BaseItemDto>(await helper.GetDefaultItems()).Select(x => new WrappedDto(x)));
            LB.Items = g.SearchResults;
            Debug.WriteLine(g.SearchResults.Count);
            LB.InvalidateVisual();
            LB.DoubleTapped += LB_DoubleTapped;
        }
    }

    private async void LB_DoubleTapped(object? sender, RoutedEventArgs e)
    {
        if (LB.SelectedItem is WrappedDto si)
        {
            Debug.WriteLine(si);
            if (si.IsFolder == true)
            {
                var o = (await helper.GetItemsFromItem(si.dto)).Select(x => new WrappedDto(x));

                g.SearchResults = new ObservableCollection<WrappedDto>(o);
                foreach (var u in g.SearchResults)
                {
                    var s = await helper.GetImageStream(u.dto);
                    if (s != null)
                    {
                        var strm = s.GetStream();
                        if (strm != null) u.Cover = Bitmap.DecodeToHeight(strm, 200);
                    }
                }

                LB.Items = g.SearchResults;
                Debug.WriteLine(g.SearchResults.Count);
                LB.InvalidateVisual();
            }
            else
            {
                jellyFinPlayStreamProvider.ProviderListner.LoadSong(await helper.GetStream(si.dto));
            }
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        LB = this.FindControl<ListBox>("LB");
    }
}

public class WrappedDto
{
    public BaseItemDto dto;

    public WrappedDto(BaseItemDto dto, WrappedStream? ws = null)
    {
        this.dto = dto;
        if (ws != null) Cover = Bitmap.DecodeToHeight(ws.GetStream(), 200);
    }

    public string Name => dto.Name;
    public string AlbumArtist => dto.AlbumArtist;

    public bool? IsFolder => dto.IsFolder;
    public int? IndexNumber => dto.IndexNumber;
    public Bitmap? Cover { get; set; }
}

public class GuiBinding
{
    public ObservableCollection<WrappedDto> SearchResults { get; set; } = new();
}