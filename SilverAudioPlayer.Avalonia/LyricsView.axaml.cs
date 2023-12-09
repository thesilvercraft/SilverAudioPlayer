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
                StackPanel parentPanel = new()
                {
                    Orientation = Orientation.Horizontal,
                };
                g.Children.Add(parentPanel);
                foreach (var lyric in mainWindow.CurrentSong.Metadata.SyncedLyrics)
                {
                    TextBlock textLyricBlock = new()
                    {
                        Text = lyric.Content.Replace("\n","").Replace("\r","\r")
                    };
                  

                    if (!d.TryGetValue(lyric.TimeStampInMilliSeconds, out TextBlock? value))
                    {
                        parentPanel.Children.Add(textLyricBlock);
                        d.Add(lyric.TimeStampInMilliSeconds, textLyricBlock);
                        textLyricBlock.DoubleTapped+=(a,c)=>{
                            mainWindow.Logic.SetPosition(lyric.TimeStampInMilliSeconds/1000d);
                        };
                    }
                    else if (string.IsNullOrWhiteSpace(lyric.Content))
                    {
                        StackPanel emptyLyricNewLinePanel = new()
                        {
                            Orientation = Orientation.Horizontal
                        };
                        g.Children.Add(emptyLyricNewLinePanel);
                        parentPanel = emptyLyricNewLinePanel;
                    }
                    else
                    {
                        value.Text += lyric.Content;
                    }
                      if (lyric.Content.EndsWith("\n"))
                    {
                        StackPanel newLinePanel = new()
                        {
                            Orientation = Orientation.Horizontal
                        };
                        g.Children.Add(newLinePanel);
                        parentPanel = newLinePanel;
                    }
                }
            }
var pos=mainWindow?.Player?.GetPosition().TotalMilliseconds;
            foreach (var x in d)
                if (x.Key <= pos)
                    x.Value.Foreground = red;
                else
                    x.Value.Foreground = white;

            return !close;
        }, TimeSpan.FromMilliseconds(25), DispatcherPriority.Render);
    }
}