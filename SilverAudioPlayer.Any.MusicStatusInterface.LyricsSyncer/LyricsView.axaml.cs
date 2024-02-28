using System;
using System.Collections.Generic;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using SilverAudioPlayer.Shared;
using SilverCraft.AvaloniaUtils;

namespace SilverAudioPlayer.Any.MusicStatusInterface.LyricsSyncer;

public partial class LyricsView : Window
{

    public LyricsView()
    {
        InitializeComponent();
        KeyDown += LyricsView_KeyDown;

        KeyUp += LyricsView_KeyUp;
    }
    IMusicStatusInterfaceListener _interfaceListener;
    Dictionary<int, TextBlock> d = new Dictionary<int, TextBlock>();
    int CurrentLyric;
    public LyricsView(IMusicStatusInterfaceListener interfaceListener) : this()
    {
        _interfaceListener = interfaceListener;
        Song? s = interfaceListener.GetCurrentTrack();
        if (s.Metadata?.SyncedLyrics?.Count is null or 0)
        {
            //good


            StackPanel g = new()
            {
                Orientation = Orientation.Vertical
            };
            sv.Content = g;


            int i = 0;
            foreach (var lyric in s.Metadata.Lyrics.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(lyric)) { continue; }
                TextBlock textLyricBlock = new()
                {
                    Text = lyric.Replace("\n", "").Replace("\r", "")
                };
                d.Add(i, textLyricBlock);
                g.Children.Add(textLyricBlock);
                i++;



            }

        }
        else
        {
            //noooo
            Close();
        }
    }
    SolidColorBrush red = new SolidColorBrush(Color.FromRgb(230, 255, 0));
    SolidColorBrush white = new SolidColorBrush(KnownColor.White.ToColor());

    private void LyricsView_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if(e.Key == Avalonia.Input.Key.Space && !b)
        {
            b = true;
            if (d.ContainsKey(CurrentLyric))
            {
                box.Text += $"{new LyricTimeSpan(_interfaceListener.GetPosition() *1000)} {d[CurrentLyric].Text}\n";
                d[CurrentLyric].Foreground = red;
                CurrentLyric++;
            }
            e.Handled = true;
        }
        if (e.Key == Avalonia.Input.Key.Z)
        {
            if(CurrentLyric>0)
            {
                CurrentLyric--;
                d[CurrentLyric].Foreground = white;
                box.Text= box.Text.Remove(box.Text.Length - 2, 1);
                box.Text= box.Text.Remove(box.Text.LastIndexOf("\n"));
            }
            e.Handled = true;
        }
    }
    bool b = false;
    private void LyricsView_KeyUp(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Space)
        {
            b = false;
        }
    }
    /*
    public LyricsView(MainWindow mainWindow) : this()
    {
        this.mainWindow = mainWindow;
        MainGrid = this.FindControl<Grid>("MainGrid");
        Dictionary<double, TextBlock> d = new();
        sv = this.FindControl<ScrollViewer>("sv");
        var white =
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
    }*/
}