using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using SilverAudioPlayer.Shared;
using SilverCraft.AvaloniaUtils;

namespace SilverAudioPlayer.Any.PlayStreamProvider.SLibIndex;

public partial class SLibIndexWindow : Window
{
    private readonly SLibIndexAutoPlayController _controller;
    private readonly ObservableCollection<SLibIndexLiteTrack> _visibleTracks = [];
    private CancellationTokenSource? _loadCts;
    private CancellationTokenSource? _artCts;
    private Bitmap? _cover;
    private bool _initializingControls;

    public SLibIndexWindow() : this(new SLibIndexAutoPlayController())
    {
    }

    public SLibIndexWindow(SLibIndexAutoPlayController controller)
    {
        _controller = controller;
        InitializeComponent();

        _initializingControls = true;
        ServerUrlBox.Text = _controller.ServerUrl;
        AutoPlayCheckBox.IsChecked = _controller.IsAutoPlayEnabled;
        StatusText.Text = _controller.LastStatusText;
        RecommendReasonText.Text = _controller.LastRecommendReasonText;
        _initializingControls = false;

        TrackList.ItemsSource = _visibleTracks;
        ConnectButton.Click += ConnectButton_Click;
        SearchBox.TextChanged += SearchBox_TextChanged;
        TrackList.SelectionChanged += TrackList_SelectionChanged;
        TrackList.DoubleTapped += TrackList_DoubleTapped;
        PlayButton.Click += PlayButton_Click;
        SmartNextButton.Click += SmartNextButton_Click;
        RadioMixButton.Click += RadioMixButton_Click;
        LikeButton.Click += LikeButton_Click;
        NotAFitButton.Click += NotAFitButton_Click;

        AutoPlayCheckBox.IsCheckedChanged += (_, _) =>
        {
            if (_initializingControls) return;
            _controller.IsAutoPlayEnabled = AutoPlayCheckBox.IsChecked == true;
        };

        _controller.StatusChanged += OnControllerStatusChanged;
        _controller.RecommendReasonChanged += OnControllerRecommendReasonChanged;
        _controller.TracksReloaded += OnControllerTracksReloaded;
        _controller.AutoPlayTrackStarted += OnAutoPlayTrackStarted;

        Closed += Window_Closed;
        FilterTracks();
        this.DoAfterInitTasks(true);
    }

    private void OnControllerStatusChanged(string text)
    {
        Dispatcher.UIThread.Post(() => StatusText.Text = text);
    }

    private void OnControllerRecommendReasonChanged(string text)
    {
        Dispatcher.UIThread.Post(() => RecommendReasonText.Text = text);
    }

    private void OnControllerTracksReloaded()
    {
        Dispatcher.UIThread.Post(FilterTracks);
    }

    private void OnAutoPlayTrackStarted(SLibIndexLiteTrack track)
    {
        Dispatcher.UIThread.Post(() =>
        {
            TrackList.SelectedItem = track;
            TrackList.ScrollIntoView(track);
        });
    }

    private void CancelAndDisposeArtCts()
    {
        try
        {
            var cts = Interlocked.Exchange(ref _artCts, null);
            cts?.Cancel();
            cts?.Dispose();
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error disposing art CTS: {ex.Message}");
        }
    }

    private void CancelAndDisposeLoadCts()
    {
        try
        {
            var cts = Interlocked.Exchange(ref _loadCts, null);
            cts?.Cancel();
            cts?.Dispose();
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error disposing load CTS: {ex.Message}");
        }
    }

    private void LikeButton_Click(object? sender, RoutedEventArgs e)
    {
        var target = _controller.CurrentPlayingTrack ?? (TrackList.SelectedItem as SLibIndexLiteTrack);
        if (target != null)
        {
            _controller.Recommender.RecordPositiveCue(target.Guid, _controller.PreviousPlayedTrack?.Artist,
                target.Artist, "User liked track");
            RecommendReasonText.Text = $"Liked '{target.Title}' by '{target.Artist}' 👍 (preference tuned)";
        }
    }

    private async void NotAFitButton_Click(object? sender, RoutedEventArgs e)
    {
        var target = _controller.CurrentPlayingTrack ?? (TrackList.SelectedItem as SLibIndexLiteTrack);
        if (target != null)
        {
            _controller.Recommender.RecordNegativeCue(target.Guid, _controller.PreviousPlayedTrack?.Artist,
                target.Artist, "User flagged not a fit");
            RecommendReasonText.Text =
                $"Not a fit for this session: steering away from '{target.Title}' by '{target.Artist}'...";
        }

        await PlaySmartNextAsync();
    }

    private async void ConnectButton_Click(object? sender, RoutedEventArgs e)
    {
        CancelAndDisposeLoadCts();
        var cts = new CancellationTokenSource();
        _loadCts = cts;
        ConnectButton.IsEnabled = false;

        try
        {
            await _controller.LoadLibraryAsync(ServerUrlBox.Text, cts.Token);
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Loading cancelled.";
        }
        catch (Exception exception)
        {
            StatusText.Text = $"Could not load the library: {exception.Message}";
        }
        finally
        {
            ConnectButton.IsEnabled = true;
        }
    }

    private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e) => FilterTracks();

    private void FilterTracks()
    {
        var query = SearchBox.Text?.Trim();
        IEnumerable<SLibIndexLiteTrack> matches = _controller.AllTracks;
        if (!string.IsNullOrEmpty(query))
        {
            matches = matches.Where(track =>
                Contains(track.Title, query) || Contains(track.Artist, query) ||
                Contains(track.Album, query) || Contains(track.FileName, query));
        }

        TrackList.SelectedItem = null;
        _visibleTracks.Clear();
        foreach (var track in matches)
            _visibleTracks.Add(track);
    }

    private static bool Contains(string? value, string query) =>
        value?.Contains(query, StringComparison.OrdinalIgnoreCase) == true;

    private async void TrackList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        CancelAndDisposeArtCts();

        if (TrackList.SelectedItem is not SLibIndexLiteTrack track)
        {
            SelectedTrackText.Text = "Select a track to see its artwork.";
            CoverImage.Source = null;
            return;
        }

        SelectedTrackText.Text = $"{track.Title}\n{track.Artist} — {track.Album}";
        if (string.IsNullOrWhiteSpace(track.ArtHash))
        {
            CoverImage.Source = null;
            return;
        }

        var cts = new CancellationTokenSource();
        _artCts = cts;
        var token = cts.Token;

        try
        {
            var apiBase = SLibIndexAutoPlayController.NormalizeApiBase(ServerUrlBox.Text);
            var url = $"{apiBase}/art/{Uri.EscapeDataString(track.ArtHash)}";
            using var response = await _controller.HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(token);
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream, token);
            memoryStream.Position = 0;

            _cover?.Dispose();
            _cover = new Bitmap(memoryStream);
            CoverImage.Source = _cover;
        }
        catch (OperationCanceledException)
        {
            // Expected on fast scrolling.
        }
        catch
        {
            CoverImage.Source = null;
        }
    }

    private async void TrackList_DoubleTapped(object? sender, TappedEventArgs e)
    {
        await PlaySelectedTrackAsync();
    }

    private async void PlayButton_Click(object? sender, RoutedEventArgs e)
    {
        await PlaySelectedTrackAsync();
    }

    private async void SmartNextButton_Click(object? sender, RoutedEventArgs e)
    {
        await PlaySmartNextAsync();
    }

    private async Task PlaySmartNextAsync()
    {
        var tracks = _controller.AllTracks;
        if (tracks.Count == 0)
        {
            StatusText.Text = "Please load the library first.";
            return;
        }

        var current = TrackList.SelectedItem as SLibIndexLiteTrack ?? _controller.CurrentPlayingTrack;
        StatusText.Text = "Selecting smart next song based on session & feedback…";

        var pick = await _controller.Recommender.PickNextAsync(tracks, current);
        if (pick?.Track != null)
        {
            TrackList.SelectedItem = pick.Track;
            TrackList.ScrollIntoView(pick.Track);
            RecommendReasonText.Text = $"AI Pick: {pick.Track.Title} by {pick.Track.Artist} ({pick.Reason})";
            await PlayTrackWithUiLockAsync(pick.Track, pick.Reason);
        }
        else
        {
            StatusText.Text = "Could not find a next track recommendation.";
        }
    }

    private async void RadioMixButton_Click(object? sender, RoutedEventArgs e)
    {
        var tracks = _controller.AllTracks;
        if (tracks.Count == 0)
        {
            StatusText.Text = "Please load the library first.";
            return;
        }

        var seed = TrackList.SelectedItem as SLibIndexLiteTrack ??
                   _controller.CurrentPlayingTrack ?? tracks[Random.Shared.Next(tracks.Count)];
        RadioMixButton.IsEnabled = false;
        StatusText.Text = $"Generating smart radio mix starting from {seed.Title}…";

        try
        {
            var mix = await _controller.Recommender.GenerateRadioMixAsync(tracks, seed, count: 10);
            if (mix.Count == 0)
            {
                StatusText.Text = "Could not generate radio mix.";
                return;
            }

            var apiBase = SLibIndexAutoPlayController.NormalizeApiBase(ServerUrlBox.Text);
            _controller.ServerUrl = apiBase;
            await _controller.QueueRadioMixAsync(mix);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error generating radio mix: {ex.Message}";
        }
        finally
        {
            RadioMixButton.IsEnabled = true;
        }
    }

    private Task PlaySelectedTrackAsync()
    {
        if (TrackList.SelectedItem is SLibIndexLiteTrack track)
        {
            return PlayTrackWithUiLockAsync(track);
        }

        return Task.CompletedTask;
    }

    private async Task PlayTrackWithUiLockAsync(SLibIndexLiteTrack track, string? reason = null)
    {
        PlayButton.IsEnabled = false;
        if (SmartNextButton != null) SmartNextButton.IsEnabled = false;
        if (RadioMixButton != null) RadioMixButton.IsEnabled = false;
        try
        {
            _controller.ServerUrl = SLibIndexAutoPlayController.NormalizeApiBase(ServerUrlBox.Text);
            await _controller.PlayTrackAsync(track, reason);
        }
        finally
        {
            PlayButton.IsEnabled = true;
            if (SmartNextButton != null) SmartNextButton.IsEnabled = true;
            if (RadioMixButton != null) RadioMixButton.IsEnabled = true;
        }
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _controller.StatusChanged -= OnControllerStatusChanged;
        _controller.RecommendReasonChanged -= OnControllerRecommendReasonChanged;
        _controller.TracksReloaded -= OnControllerTracksReloaded;
        _controller.AutoPlayTrackStarted -= OnAutoPlayTrackStarted;

        CancelAndDisposeLoadCts();
        CancelAndDisposeArtCts();
        _cover?.Dispose();
        _cover = null;
    }
}
