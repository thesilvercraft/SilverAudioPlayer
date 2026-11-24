using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.StandardRequests;
using SilverCraft.AvaloniaUtils;

namespace Catenoid;

public partial class LyricsView : Window
{
    private readonly MainWindow? mainWindow;
    private static readonly List<LyricsView> _openInstances = [];
    private static readonly IBrush DefaultWhiteBrush = new SolidColorBrush(KnownColor.White.ToColor());

    private IBrush _highlightBrush = CreateDefaultHighlightBrush();
    private Dictionary<double, List<Run>> _lyricRuns = new();
    private readonly Dictionary<Run, double> _inverseLyricRuns = new();
    private readonly List<TimedEvent> _registeredEvents = [];
    private readonly SelectableTextBlock _masterTextBlock = new()
    {
        TextWrapping = TextWrapping.Wrap,
        Inlines = new InlineCollection()
    };

    private Guid? _songGuid;
    private int _lyricCount = -1;
    private bool _closed;

    private static IBrush CreateDefaultHighlightBrush()
    {
        var b = WindowExtensions.envBackend.GetString("SAPLRCColor")
            .ParseBackground(new SolidColorBrush(Color.FromRgb(230, 255, 0)));
        return b is SolidColorBrush scb ? new SolidColorBrush(scb.Color, 1) : b;
    }

    public static void NotifyColorChanged(IBrush? brush = null)
    {
        Dispatcher.UIThread.Post(() =>
        {
            foreach (var instance in _openInstances.ToArray())
            {
                instance.UpdateHighlightBrush(brush);
            }
        });
    }

    public void UpdateHighlightBrush(IBrush? brush = null)
    {
        _highlightBrush = brush ?? CreateDefaultHighlightBrush();
        var pos = mainWindow?.Player?.GetPosition().TotalMilliseconds ?? 0;
        UpdateHighlightAndScroll(pos);
    }

    public LyricsView()
    {
        InitializeComponent();
        this.DoAfterInitTasksF();
        MainGrid = this.FindControl<Grid>("MainGrid");
        sv = this.FindControl<ScrollViewer>("sv");
    }

    public LyricsView(MainWindow mainWindow) : this()
    {
        this.mainWindow = mainWindow;
        DataContext = mainWindow.config;
        _masterTextBlock.FontSize = mainWindow.config.LyricsFontSize;
        if (sv != null)
        {
            sv.Content = _masterTextBlock;
        }
        UpdateHighlightBrush();

        PropertyChangedEventHandler configHandler = (_, e) =>
        {
            if (e.PropertyName == nameof(Config.LyricsFontSize))
            {
                Dispatcher.UIThread.Post(() => { _masterTextBlock.FontSize = mainWindow.config.LyricsFontSize; });
            }
        };
        mainWindow.config.PropertyChanged += configHandler;
        mainWindow.Env.TrackChangedNotification += OnTrackChanged;
        mainWindow.Env.PositionChanged += OnPositionChanged;
        mainWindow.Env.PlayerStateChanged += OnPlayerStateChanged;
        mainWindow.Logic.Mediator.OnMessageReceived += OnMediatorMessage;
        _openInstances.Add(this);

        Closed += (_, _) =>
        {
            _closed = true;
            _openInstances.Remove(this);
            mainWindow.config.PropertyChanged -= configHandler;
            mainWindow.Env.TrackChangedNotification -= OnTrackChanged;
            mainWindow.Env.PositionChanged -= OnPositionChanged;
            mainWindow.Env.PlayerStateChanged -= OnPlayerStateChanged;
            mainWindow.Logic.Mediator.OnMessageReceived -= OnMediatorMessage;
            ClearRegisteredTimedEvents();
        };

        AddHandler(PointerWheelChangedEvent, (_, e) =>
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                var delta = e.Delta.Y > 0 ? 2 : -2;
                var newSize = Math.Clamp(mainWindow.config.LyricsFontSize + delta, 8, 200);
                mainWindow.config.LyricsFontSize = newSize;
                e.Handled = true;
            }
        }, RoutingStrategies.Tunnel);

        _masterTextBlock.DoubleTapped += OnLyricsDoubleTapped;

        SyncWithCurrentSong(forceRebuild: true);
    }

    private void OnTrackChanged(object? sender, Song? song)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!_closed) SyncWithCurrentSong(forceRebuild: false);
        });
    }

    private void OnPositionChanged(object? sender, TimeSpan pos)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_closed) return;
            var posMs = pos.TotalMilliseconds;
            UpdateHighlightAndScroll(posMs);
            RescheduleTimedEvents(posMs);
        });
    }

    private void OnPlayerStateChanged(object? sender, PlaybackState state)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_closed || mainWindow == null) return;
            var posMs = state == PlaybackState.Stopped
                ? 0
                : mainWindow.Player?.GetPosition().TotalMilliseconds ?? 0;
            UpdateHighlightAndScroll(posMs);
            if (state is PlaybackState.Playing or PlaybackState.Buffering && _registeredEvents.Count == 0)
            {
                RescheduleTimedEvents(posMs);
            }
        });
    }

    private void OnMediatorMessage(object? sender, IMessage message)
    {
        if (message is RefreshMetadataAndScrollBarMessage)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (!_closed) SyncWithCurrentSong(forceRebuild: false);
            });
        }
    }

    private void SyncWithCurrentSong(bool forceRebuild)
    {
        if (_closed || mainWindow == null) return;
        var song = mainWindow.CurrentSong;
        var lyrics = song?.Metadata?.SyncedLyrics ?? [];
        var posMs = mainWindow.Player?.GetPosition().TotalMilliseconds ?? 0;

        if (!forceRebuild && _songGuid == song?.Guid && _lyricCount == lyrics.Count)
        {
            UpdateHighlightAndScroll(posMs);
            return;
        }

        _songGuid = song?.Guid;
        _lyricCount = lyrics.Count;
        _lyricRuns.Clear();
        _inverseLyricRuns.Clear();
        _masterTextBlock.Inlines?.Clear();

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
                    Foreground = DefaultWhiteBrush
                };
                _masterTextBlock.Inlines?.Add(run);

                if (_lyricRuns.TryGetValue(lyric.TimeStampInMilliSeconds, out var runs))
                {
                    runs.Add(run);
                }
                else
                {
                    _lyricRuns[lyric.TimeStampInMilliSeconds] = [run];
                }
                _inverseLyricRuns[run] = lyric.TimeStampInMilliSeconds;

                if (i < lines.Length - 1)
                {
                    _masterTextBlock.Inlines?.Add(new LineBreak());
                }
            }

            if (lyricIdx < lyrics.Count - 1 && (_masterTextBlock.Inlines?.Count ?? 0) > 0)
            {
                _masterTextBlock.Inlines?.Add(new LineBreak());
            }
        }

        UpdateHighlightAndScroll(posMs);
        RescheduleTimedEvents(posMs);
    }

    private void ClearRegisteredTimedEvents()
    {
        if (mainWindow == null) return;
        foreach (var ev in _registeredEvents)
        {
            mainWindow.Env.RemoveTimedEvent(ev);
        }
        _registeredEvents.Clear();
    }

    private void RescheduleTimedEvents(double currentPosMs)
    {
        if (_closed || mainWindow == null) return;
        ClearRegisteredTimedEvents();

        foreach (var timestampMs in _lyricRuns.Keys.OrderBy(t => t))
        {
            if (timestampMs <= currentPosMs) continue;
            var targetMs = timestampMs;
            var timedEvent = new TimedEvent(targetMs / 1000.0, (_, posSeconds) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (_closed) return;
                    var effectivePosMs = Math.Max(
                        mainWindow.Player?.GetPosition().TotalMilliseconds ?? posSeconds * 1000.0,
                        targetMs);
                    UpdateHighlightAndScroll(effectivePosMs);
                });
                return true;
            });
            _registeredEvents.Add(timedEvent);
            mainWindow.Env.AddTimedEvent(timedEvent);
        }
    }

    private void UpdateHighlightAndScroll(double posMs)
    {
        double? lastActiveTs = null;

        foreach (var (timestamp, runs) in _lyricRuns)
        {
            if (timestamp <= posMs)
            {
                foreach (var run in runs)
                {
                    run.Foreground = _highlightBrush;
                }
                if (lastActiveTs == null || timestamp > lastActiveTs.Value)
                {
                    lastActiveTs = timestamp;
                }
            }
            else
            {
                foreach (var run in runs)
                {
                    run.Foreground = DefaultWhiteBrush;
                }
            }
        }

        if (mainWindow != null && sv != null && mainWindow.config.LyricsAdvance != -1 && lastActiveTs is { } activeTs)
        {
            if (_lyricRuns.TryGetValue(activeTs, out var activeRuns) && activeRuns.Count > 0 &&
                _masterTextBlock.TextLayout != null)
            {
                var firstRun = activeRuns[0];
                int charOffset = GetCharacterOffsetForRun(_masterTextBlock, firstRun);

                if (charOffset >= 0)
                {
                    var rects = _masterTextBlock.TextLayout.HitTestTextRange(charOffset, firstRun.Text?.Length ?? 1);
                    var firstRect = rects.FirstOrDefault();

                    if (firstRect != default)
                    {
                        double targetY = firstRect.Top - (sv.Bounds.Height / 2) + (firstRect.Height / 2);
                        sv.Offset = new Vector(sv.Offset.X, Math.Max(0, targetY));
                    }
                }
            }
        }
    }

    private void OnLyricsDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (mainWindow == null || _lyricRuns.Count == 0 || _masterTextBlock.Inlines == null)
            return;

        int totalTextLength = 0;
        foreach (var inline in _masterTextBlock.Inlines)
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

        var selectionLength = Math.Abs(_masterTextBlock.SelectionEnd - _masterTextBlock.SelectionStart);
        if (selectionLength >= totalTextLength && totalTextLength > 0)
        {
            return;
        }

        var characterIndex = _masterTextBlock.SelectionStart;
        Run? tappedRun = null;
        var currentOffset = 0;

        foreach (var inline in _masterTextBlock.Inlines)
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

        if (tappedRun != null && _inverseLyricRuns.TryGetValue(tappedRun, out double timestampMs))
        {
            mainWindow.Logic?.SetPosition(timestampMs / 1000d);
        }
    }

    private static int GetCharacterOffsetForRun(SelectableTextBlock textBlock, Run targetRun)
    {
        if (textBlock.Inlines == null) return -1;
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