using System.Composition;
using Serilog;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.Metadata;
using SilverCraft.Ekorensis;

namespace SilverAudioPlayer.Any.MusicStatusInterface.ListenBrainz;

[Export(typeof(IMusicStatusInterface))]
file class ListenBrainzMsi : IMusicStatusInterface, IAmConfigurable, IAmOnceAgainAskingYouForYourMemory
{
    public ListenBrainzMsi(ILogger logger,HttpClient client)
    {
        _logger = logger;
        _client = client;
    }

    private HttpClient _client;

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public ObjectToRemember ConfigObject =
        new(Guid.Parse("489f696d-86d0-4a9d-80e6-bb00e573231a"), new ListenBrainzConfig());

    public IEnumerable<ObjectToRemember> ObjectsToRememberForMe => new[] { ConfigObject };


    public string Name => "Listenbrainz listener";
    public string Description => "Track music playback on listenbrainz.org";
    public WrappedStream? Icon { get; }
    public Version? Version => typeof(ListenBrainzMsi).Assembly.GetName().Version;
    public string Licenses { get; }
    public List<Tuple<Uri, URLType>>? Links { get; }
    ListenBrainzClient? l = null;
    ILogger _logger;

    public void StartIPC(IMusicStatusInterfaceListener listener)
    {
        if (ConfigObject.Value is not ListenBrainzConfig x || !Guid.TryParse(x.UserToken, out _))
        {
            return;
        }

        try
        {
          //  if ( l.ValidateTokenAsync(x.UserToken).GetAwaiter().GetResult().Valid != true) return;
        }
        catch (Exception e)
        {
            _logger?.Error($"ListenBrainzMsi start error {e}", e);
            return;
        }

        l = new ListenBrainzClient(_client, x.UserToken);
        IsStarted = true;
        listener.TrackChangedNotification += TrackChanged;
        _listener = listener;
    }
    private IMusicStatusInterfaceListener _listener;
    private DateTime Started;
    private IMetadata m;
    private double length = 0;

    private void TrackChanged(object? sender, Song e)
    {
        Started = DateTime.Now;
        if (ConfigObject.Value is not ListenBrainzConfig x) return;
        if (e.Metadata?.Duration == null) return;
        m = e.Metadata;
        if (string.IsNullOrEmpty(m.Title) || string.IsNullOrEmpty(m.Artist) || string.IsNullOrEmpty(m.Album)) return;
        _ = Task.Run(async () => await l.SetNowPlayingAsync(m.Artist, m.Title,m.Album));
        length = x.ListenPolicy switch
        {
            Policy.Percentage => (double)(e.Metadata.Duration / 1000 * (x.ListenPolicyValue / 100d)),
            Policy.Seconds => x.ListenPolicyValue,
            _ => throw new ArgumentOutOfRangeException()
        };
        _listener.AddTimedEvent(length, TimedEvent);
    }

    private bool TimedEvent(Song arg1, double pos)
    {
        if (!(pos - length < 0.2)) return true;
        if (l == null) return false;
        if (string.IsNullOrWhiteSpace(m.Title) || string.IsNullOrWhiteSpace(m.Artist) || string.IsNullOrWhiteSpace(m.Album)) return false;
        var additionalInfo = new Dictionary<string, object?>();
        if (m.Entries != null && m.Entries.Count != 0)
        {
            if (m.Entries.TryGetValue("MUSICBRAINZ_TRACKID", out var trackGuid))
            {
                additionalInfo.Add("recording_mbid", trackGuid);
            }

            if (m.Entries.TryGetValue("MUSICBRAINZ_ALBUMID", out var albumGuid))
            {
                additionalInfo.Add("release_mbid", albumGuid);
            }
            /* if (m.Entries.TryGetValue("MUSICBRAINZ_ARTISTID", out var artistGuid))
                 {
                     listen.Track.AdditionalInfo.Add("artist_mbids", new []{artistGuid}); //incorrect format
                 }  */
        }
        // TODO: recheck if it stutters playback
        Task.Run(async () => { await l.SubmitListenAsync(m.Artist, m.Title, m.Album, DateTime.Now, additionalInfo); });
        return true;
    }

    public void StopIPC(IMusicStatusInterfaceListener listener)
    {
        IsStarted = false;
        listener.TrackChangedNotification -= TrackChanged;
        _listener = null;
    }

    public bool IsStarted { get; set; }

    public IEnumerable<IConfigurableElement> GetElements()
    {
        return new List<IConfigurableElement>
        {
            new SimpleRow()
            {
                Content =
                [
                    new SimpleLabel
                    {
                        GetContent = () => "ListenBrainz token:"
                    },
                    new SimpleTextBox
                    {
                        GetPlaceholder = () => "489f696d-86d0-4a9d-80e6-bb00e573231a",
                        GetContent = () =>
                        {
                            if (ConfigObject.Value is ListenBrainzConfig x)
                            {
                                return x.UserToken;
                            }
                            return "ERROR";
                        },
                        SetContent = (s) =>
                        {
                            if (ConfigObject.Value is not ListenBrainzConfig x) return;
                            x.UserToken = s;
                            ((ICanBeToldThatAPartOfMeIsChanged)x).PropertyChanged(x, new("Uploader"));
                        }
                    }
                ]
            },
            new SimpleRow
            {
                Content =
                [
                    new SimpleLabel
                    {
                        GetContent = () => "Policy"
                    },
                    new SimpleDropDown
                    {
                        GetOptions = Enum.GetNames<Policy>,
                        GetPlaceholder = () => "Percentage",
                        GetSelection = () =>
                        {
                            if (ConfigObject.Value is ListenBrainzConfig x)
                            {
                                return x.ListenPolicy.ToString();
                            }
                            return nameof(Policy.Percentage);
                        },
                        SetSelection = (s) =>
                        {
                            if (ConfigObject.Value is not ListenBrainzConfig x) return;
                            x.ListenPolicy = Enum.Parse<Policy>(s);
                            ((ICanBeToldThatAPartOfMeIsChanged)x).PropertyChanged(x, new("ListenPolicy"));
                        }
                    },
                    new SimpleTextBox
                    {
                        GetPlaceholder = () => "30",
                        GetContent = () =>
                        {
                            if (ConfigObject.Value is ListenBrainzConfig x)
                            {
                                return x.ListenPolicyValue.ToString();
                            }
                            return "0";
                        },
                        SetContent = s =>
                        {
                            if (ConfigObject.Value is not ListenBrainzConfig x) return;
                            if (!uint.TryParse(s, out var z)) return;
                            x.ListenPolicyValue = z;
                            ((ICanBeToldThatAPartOfMeIsChanged)x).PropertyChanged(x, new("ListenPolicyValue"));
                        }
                    }
                ]
            },
        };
    }
}