using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Jellyfin.Sdk;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace SilverAudioPlayer.Any.PlayStreamProvider.JellyFin
{
    public partial class Gui : Window
    {
        private JellyFinPlayStreamProvider jellyFinPlayStreamProvider;
        private JellyFinHelper helper;
        private GuiBinding g;

        public Gui()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            Opened += Gui_Opened;
            g = new();
            this.DataContext = g;
        }

        private async void Button_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            g.SearchResults = new(new List<BaseItemDto>(await helper.GetDefaultItems()).OrderBy(x => x.IndexNumber));
            LB.Items = g.SearchResults;
            Debug.WriteLine(g.SearchResults.Count);
            LB.InvalidateVisual();
            LB.DoubleTapped += LB_DoubleTapped;
        }

        private async void AddEntireScreen(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            foreach (var song in g.SearchResults.Where(x => x.IsFolder != true).OrderBy(x => x.IndexNumber))
            {
                var ws = await helper.GetStream(song);
                await Task.Delay(100);

                jellyFinPlayStreamProvider.ProviderListner.LoadSong(ws);
            }
        }

        private async void Gui_Opened(object? sender, EventArgs e)
        {
            if (helper != null)
            {
                await helper.MakeSureUserLogsIn(this);
                g.SearchResults = new(new List<BaseItemDto>(await helper.GetDefaultItems()));
                LB.Items = g.SearchResults;
                Debug.WriteLine(g.SearchResults.Count);
                LB.InvalidateVisual();
                LB.DoubleTapped += LB_DoubleTapped;
            }
        }

        private async void LB_DoubleTapped(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (LB.SelectedItem is BaseItemDto si)
            {
                Debug.WriteLine(si);
                if (si.IsFolder == true)
                {
                    g.SearchResults = new(new List<BaseItemDto>(await helper.GetItemsFromItem(si)));
                    LB.Items = g.SearchResults;
                    Debug.WriteLine(g.SearchResults.Count);
                    LB.InvalidateVisual();
                }
                else
                {
                    jellyFinPlayStreamProvider.ProviderListner.LoadSong(await helper.GetStream(si));
                }
            }
        }

        public Gui(JellyFinPlayStreamProvider jellyFinPlayStreamProvider) : this()
        {
            this.jellyFinPlayStreamProvider = jellyFinPlayStreamProvider;
            helper = new();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            LB = this.FindControl<ListBox>("LB");
        }
    }

    public class GuiBinding
    {
        public ObservableCollection<BaseItemDto> SearchResults { get; set; } = new();
    }
}