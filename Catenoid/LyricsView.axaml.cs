using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using DynamicData;
using SilverCraft.AvaloniaUtils;

namespace Catenoid;

public partial class LyricsView : Window
{
    private MainWindow mainWindow;
    public LyricsView(MainWindow mainWindow) 
    {
        this.mainWindow = mainWindow;
        DataContext = mainWindow.config;
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
        this.DoAfterInitTasksF();
        MainGrid = this.FindControl<Grid>("MainGrid");
        if (OperatingSystem.IsLinux())
        {
            MainGrid.RowDefinitions[0].Height = GridLength.Parse("0");
        }
        Dictionary<double, SelectableTextBlock> d = [];
        sv = this.FindControl<ScrollViewer>("sv");
        var red = WindowExtensions.envBackend.GetString("SAPLRCColor").ParseBackground(new SolidColorBrush(Color.FromRgb(230, 255, 0)));
        if(red is SolidColorBrush scb)
        {
            scb.Opacity=1;
        }
        var white = new SolidColorBrush(KnownColor.White.ToColor());
        Guid? Song = null;
        var close = false;
        Closed += (_, _) => close = true;
        DispatcherTimer.Run(() =>
        {
            if (Song != mainWindow?.CurrentSong?.Guid)
            {
                d = [];
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
                foreach (var lyric in mainWindow?.CurrentSong?.Metadata?.SyncedLyrics ?? [])
                {
                    SelectableTextBlock textLyricBlock = new()
                    {
                        Text = lyric.Content.Replace("\n","").Replace("\r","\r")
                    };
                  

                    if (!d.TryGetValue(lyric.TimeStampInMilliSeconds, out SelectableTextBlock? value))
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

                    if (!lyric.Content.EndsWith('\n')) continue;
                    StackPanel newLinePanel = new()
                    {
                        Orientation = Orientation.Horizontal
                    };
                    g.Children.Add(newLinePanel);
                    parentPanel = newLinePanel;
                }
            }
            var pos=mainWindow?.Player?.GetPosition().TotalMilliseconds;
            double? lastr = null;
            foreach (var x in d)
            {
                if (heightm == 20.12242)
                {
                    if(x.Value.Bounds.Size.Height>heightm)
                        heightm=x.Value.Bounds.Size.Height+ x.Value.Padding.Top+
                                x.Value.Padding.Bottom;
                }
            
                
                if (x.Key <= pos)
                {
                    x.Value.Foreground = red;
                    lastr = x.Key;
                }
                else
                {
                    x.Value.Foreground = white;
                }
            }


            if (mainWindow == null || mainWindow.config.LyricsAdvance == -1 || lastr is not { } c) return !close;
            {
                var a = mainWindow.config.LyricsAdvance;
                if (a < -1)
                {
                    a = Math.Max(1, (int)(Height / heightm / 2));
                }
                SelectableTextBlock t = d[d.Keys.ElementAt( Math.Min( d.Keys.Count-1, d.Keys.IndexOf(c)+a))];
                t.BringIntoView(new Rect(0, t.Bounds.Height, t.Bounds.Width, t.Bounds.Height));
            }
            return !close;
        }, TimeSpan.FromMilliseconds(25), DispatcherPriority.Render);
    }

    


    double heightm = 20.12242;

}