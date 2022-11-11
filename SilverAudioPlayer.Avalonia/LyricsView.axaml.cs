using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using SilverCraft.AvaloniaUtils;

namespace SilverAudioPlayer.Avalonia;

public partial class LyricsView : Window
{
    private MainWindow mainWindow;

    public LyricsView()
    {
        InitializeComponent();
        this.DoAfterInitTasksF();
    }

    public LyricsView(MainWindow mainWindow) : this()
    {
        this.mainWindow = mainWindow;
        MainGrid = this.FindControl<Grid>("MainGrid");
        Dictionary<double, TextBlock> d = new();
        sv = this.FindControl<ScrollViewer>("sv");
        var red = new SolidColorBrush(Color.FromRgb(230, 255, 0));
        var white = new SolidColorBrush(KnownColor.White.ToColor());
        Guid? Song = null;
        var close = false;
        Closed += (_, _) => close = true;
        DispatcherTimer.Run(() =>
        {
            if (Song != mainWindow?.CurrentSong?.Guid)
            {
                d = new Dictionary<double, TextBlock>();
                Song = mainWindow?.CurrentSong?.Guid;
                StackPanel g = new()
                {
                    Orientation = Orientation.Vertical
                };
                sv.Content = g;
                StackPanel p = new()
                {
                    Orientation = Orientation.Horizontal
                };
                g.Children.Add(p);
                foreach (var x in mainWindow.CurrentSong.Metadata.SyncedLyrics)
                {
                    TextBlock b = new()
                    {
                        Text = x.Content
                    };
                    if (x.Content.EndsWith("\n"))
                    {
                        StackPanel p2 = new()
                        {
                            Orientation = Orientation.Horizontal
                        };
                        g.Children.Add(p2);
                        p = p2;
                    }

                    if (!d.ContainsKey(x.TimeStampInMilliSeconds))
                    {
                        p.Children.Add(b);
                        d.Add(x.TimeStampInMilliSeconds, b);
                    }
                    else if (string.IsNullOrWhiteSpace(x.Content))
                    {
                        StackPanel p2 = new()
                        {
                            Orientation = Orientation.Horizontal
                        };
                        g.Children.Add(p2);
                        p = p2;
                    }
                    else
                    {
                        d[x.TimeStampInMilliSeconds].Text += x.Content;
                    }
                }
            }

            foreach (var x in d)
                if (x.Key <= mainWindow?.Player?.GetPosition().TotalMilliseconds)
                    x.Value.Foreground = red;
                else
                    x.Value.Foreground = white;

            return !close;
        }, TimeSpan.FromMilliseconds(25), DispatcherPriority.Render);
    }
}