using System;
using System.Collections.Generic;
using System.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Serilog;
using SilverAudioPlayer.Shared;
using SilverCraft.Ekorensis;
using SLibIndex;

namespace SilverAudioPlayer.Any.PlayStreamProvider.SLibIndex;

[Export(typeof(IPlayStreamProvider))]
public class SLibIndexPlayStreamProvider : IPlayStreamProvider, IConfigurableUiPlugin
{
    private string _baseUrl = "";
    private IPlayStreamProviderListener? _listener;
    private List<IndexedTrack> _tracks = new();
    private readonly System.Net.Http.HttpClient _httpClient = new();
    private IConfigurableWindow? _currentWindow;

    public string Name => "SLibIndex Provider";
    public string Description => "Provides music from an SLibIndex server";
    public WrappedStream? Icon => null;
    public Version? Version => typeof(SLibIndexPlayStreamProvider).Assembly.GetName().Version;
    public string Licenses => "GPL-3.0-only";
    public List<Tuple<Uri, URLType>>? Links => null;

    public void Use(IPlayStreamProviderListener env)
    {
        _listener = env;
        
    }

    public void ConfigureUi(IConfigurableListener env)
    {
        _currentWindow = env.TryGetWindow(GetConfigElements());
        _currentWindow?.SetTitle("SLibIndex Configuration");
        _currentWindow?.Show();
    }

    private IEnumerable<IConfigurableElement> GetConfigElements()
    {
        if (string.IsNullOrEmpty(_baseUrl) || _tracks.Count == 0)
        {
            var urlTextBox = new SimpleTextBox
            {
                GetPlaceholder = () => "Server URL (e.g. http://192.168.1.10:5000)",
                GetContent = () => _baseUrl,
                SetContent = (v) => _baseUrl = v.TrimEnd('/')
            };

            var connectButton = new SimpleButton
            {
                GetContent = () => "Connect",
                Clicked = async () =>
                {
                    await RefreshTracks();
                    _currentWindow?.SetElements(GetConfigElements());
                }
            };

            return new List<IConfigurableElement> { urlTextBox, connectButton };
        }

        var gallery = new SimpleGallery
        {
            GetItems = () => _tracks.Select(t => {
                var item = new SimpleGalleryItem
                {
                    Title = t.Title,
                    Subtitle = t.Artist
                };
                _ = LoadImageForItem(item, t);
                return item;
            }),
            OnDoubleTapped = (item) =>
            {
                var track = _tracks.FirstOrDefault(t => t.Title == item.Title && t.Artist == item.Subtitle);
                if (track != null)
                {
                    var streamUrl = $"{_baseUrl}/tracks/download/{track.GUID}";
                    _listener?.LoadSong(new WrappedHttpStream(streamUrl), track);
                }
            }
        };

        return new List<IConfigurableElement> { gallery };
    }

    private async Task LoadImageForItem(SimpleGalleryItem item, IndexedTrack track)
    {
        if (track.Art == null)
            return;
        item.Image = $"{_baseUrl}/tracks/art/{track.GUID}";
    }

    private async Task RefreshTracks()
    {
        if (string.IsNullOrEmpty(_baseUrl)) return;
        try
        {
            var url = _baseUrl.EndsWith("/tracks") ? _baseUrl : $"{_baseUrl}/tracks";
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                using var stream = await response.Content.ReadAsStreamAsync();
                _tracks = IndexedTrack.LoadFromStream(stream, false, new JsonSerializerOptions(){PropertyNameCaseInsensitive = true});
              
            }
        }
        catch
        {
            // Log error
        }
    }
}
