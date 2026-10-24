using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Threading;
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
        this.DoAfterInitTasksF();

        MainGrid = this.FindControl<Grid>("MainGrid");

        sv = this.FindControl<ScrollViewer>("sv");

        var red = WindowExtensions.envBackend.GetString("SAPLRCColor")
            .ParseBackground(new SolidColorBrush(Color.FromRgb(230, 255, 0)));
        if (red is SolidColorBrush scb)
        {
            scb.Opacity = 1;
        }
        var white = new SolidColorBrush(KnownColor.White.ToColor());

        Guid? songGuid = null;
        var close = false;
        Closed += (_, _) => close = true;

        SelectableTextBlock masterTextBlock = new()
        {
            TextWrapping = TextWrapping.Wrap,
            Inlines = new InlineCollection()
        };

        Dictionary<double, List<Run>> lyricRuns = new();
        Dictionary<Run, double> inverseLyricRuns = new();

        DispatcherTimer.Run(() =>
        {
            if (close)
                return false;
            if (songGuid != mainWindow.CurrentSong?.Guid)
            {
                lyricRuns.Clear();
                inverseLyricRuns.Clear();
                masterTextBlock.Inlines.Clear();
                songGuid = mainWindow?.CurrentSong?.Guid;

                sv.Content = masterTextBlock;

                var lyrics = mainWindow?.CurrentSong?.Metadata?.SyncedLyrics ?? [];

                for (var lyricIdx = 0; lyricIdx < lyrics.Count; lyricIdx++)
                {
                    var lyric = lyrics[lyricIdx];
                    if (string.IsNullOrEmpty(lyric.Content))
                        continue;

                    var trimmedContent = lyric.Content.TrimEnd('\r', '\n');
                    var lines = trimmedContent.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

                    for (var i = 0; i < lines.Length; i++)
                    {
                        var line = lines[i];
                        if (string.IsNullOrEmpty(line))
                            continue;

                        Run run = new()
                        {
                            Text = line,
                            Foreground = white
                        };

                        masterTextBlock.Inlines.Add(run);

                        if (lyricRuns.TryGetValue(lyric.TimeStampInMilliSeconds, out var runs))
                        {
                            runs.Add(run);
                        }
                        else
                        {
                            lyricRuns[lyric.TimeStampInMilliSeconds] = [run];
                        }
                        inverseLyricRuns[run] = lyric.TimeStampInMilliSeconds;

                        if (i < lines.Length - 1)
                        {
                            masterTextBlock.Inlines.Add(new LineBreak());
                        }
                    }

                    if (lyricIdx < lyrics.Count - 1 && masterTextBlock.Inlines.Count > 0)
                    {
                        masterTextBlock.Inlines.Add(new LineBreak());
                    }
                }
            }

            var pos = mainWindow?.Player?.GetPosition().TotalMilliseconds;
            double? lastActiveTs = null;

            foreach (var (timestamp, runs) in lyricRuns)
            {
                if (timestamp <= pos)
                {
                    foreach (var run in runs)
                    {
                        run.Foreground = red;
                    }
                    lastActiveTs = timestamp;
                }
                else
                {
                    foreach (var run in runs)
                    {
                        run.Foreground = white;
                    }
                }
            }

            if (mainWindow != null && mainWindow.config.LyricsAdvance != -1 && lastActiveTs is { } activeTs) 
            {
                if (lyricRuns.TryGetValue(activeTs, out var activeRuns) && activeRuns.Count > 0 && masterTextBlock.TextLayout != null)
                {
                    var firstRun = activeRuns[0];
                    int charOffset = GetCharacterOffsetForRun(masterTextBlock, firstRun);
                    
                    if (charOffset >= 0)
                    {
                        var rects = masterTextBlock.TextLayout.HitTestTextRange(charOffset, firstRun.Text?.Length ?? 1);
                        var firstRect = rects.FirstOrDefault();
                        
                        if (firstRect != default)
                        {
                            // Center active line in ScrollViewer viewport
                            double targetY = firstRect.Top - (sv.Bounds.Height / 2) + (firstRect.Height / 2);
                            sv.Offset = new Vector(sv.Offset.X, Math.Max(0, targetY));
                        }
                    }
                }
            }

            return true;
        }, TimeSpan.FromMilliseconds(25), DispatcherPriority.Render);

        masterTextBlock.DoubleTapped += (sender, e) =>
        {
            if (lyricRuns.Count == 0) 
                return;

            int totalTextLength = 0;
            foreach (var inline in masterTextBlock.Inlines)
            {
                switch (inline)
                {
                    case Run run:
                        totalTextLength += run.Text?.Length ?? 0;
                        break;
                    case LineBreak:
                        totalTextLength += 1;
                        break;
                }
            }

            var selectionLength = Math.Abs(masterTextBlock.SelectionEnd - masterTextBlock.SelectionStart);
            if (selectionLength >= totalTextLength && totalTextLength > 0)
            {
                return;
            }

            var characterIndex = masterTextBlock.SelectionStart;
            Run tappedRun = null;
            var currentOffset = 0;

            foreach (var inline in masterTextBlock.Inlines)
            {
                if (inline is Run run)
                {
                    var length = run.Text?.Length ?? 0;
                    if (characterIndex >= currentOffset && characterIndex <= currentOffset + length)
                    {
                        tappedRun = run;
                        break;
                    }
                    currentOffset += length;
                }
                else if (inline is LineBreak)
                {
                    currentOffset += 1;
                }
            }

            if (tappedRun != null && inverseLyricRuns.TryGetValue(tappedRun, out double timestampMs))
            {
                mainWindow.Logic?.SetPosition(timestampMs / 1000d);
            }
        };
    }

    private static int GetCharacterOffsetForRun(SelectableTextBlock textBlock, Run targetRun)
    {
        int offset = 0;
        foreach (var inline in textBlock.Inlines)
        {
            if (inline == targetRun)
                return offset;

            if (inline is Run r)
                offset += r.Text?.Length ?? 0;
            else if (inline is LineBreak)
                offset += 1;
        }
        return -1;
    }
}