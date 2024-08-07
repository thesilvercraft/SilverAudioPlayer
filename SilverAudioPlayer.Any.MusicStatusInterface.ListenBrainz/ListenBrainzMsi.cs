using System.Composition;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.Metadata;
using SilverCraft.Ekorensis;
using HttpClient = SilverAudioPlayer.Shared.HttpClient;
using Timer = System.Threading.Timer;

namespace SilverAudioPlayer.Any.MusicStatusInterface.ListenBrainz;

[Export(typeof(IMusicStatusInterface))]
public class ListenBrainzMsi :IMusicStatusInterface, IAmConfigurable, IAmOnceAgainAskingYouForYourMemory
{
    public void Dispose()
    {
        timer?.Dispose();
        l?.Dispose();
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
    MetaBrainz.ListenBrainz.ListenBrainz? l=null;
    public void StartIPC(IMusicStatusInterfaceListener listener)
    {
        if (ConfigObject.Value is not ListenBrainzConfig x ||             !Guid.TryParse( x.UserToken, out _) )
        {
            return;
        }
        l = new(HttpClient.Client, false);
        if(l.ValidateToken(x.UserToken).Valid!=true) return;
        l.UserToken = x.UserToken;
        IsStarted = true;
        listener.TrackChangedNotification += TrackChanged;
        _listener = listener;
    }

    private IMusicStatusInterfaceListener _listener;
    private DateTime Started;
    private IMetadata m;
    private double length = 0;
    private Timer? timer=null;
    private readonly object lockObj = new object();

    private void TrackChanged(object? sender, Song e)
    {
        lock (lockObj)
        {
            timer?.Dispose();

            Started = DateTime.Now;
            if (ConfigObject.Value is not ListenBrainzConfig x) return;
            m = e.Metadata;
            if (string.IsNullOrEmpty(m.Title) || string.IsNullOrEmpty(m.Artist)) return;
            _ = Task.Run(async () => l.SetNowPlayingAsync(m.Title, m.Artist, m.Album));

            length = x.ListenPolicy switch
            {
                Policy.Percentage => (double)(e.Metadata.Duration / 1000 * (x.ListenPolicyValue / 100d)),
                Policy.Seconds => x.ListenPolicyValue,
                _ => throw new ArgumentOutOfRangeException()
            };

            if (length != 0)
            {
                timer = new Timer(OnTimerOnElapsed, null, TimeSpan.FromSeconds(1), Timeout.InfiniteTimeSpan);
            }
        }
    }

    private void OnTimerOnElapsed(object? state)
    {
        lock (lockObj)
        {
            if (_listener.GetPosition() >= length)
            {
               _ = Task.Run(async () =>
                {
                    await timer.DisposeAsync();
                    if (string.IsNullOrEmpty(m.Title) || string.IsNullOrEmpty(m.Artist)) return;
                    _ = l.SubmitSingleListenAsync(Started, m.Title, m.Artist, m.Album);
                });
            }
            else
            {
                timer.Change(TimeSpan.FromSeconds(1), Timeout.InfiniteTimeSpan);
            }
        }
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
            Content =[
                new SimpleLabel()
                {
                    GetContent = ()=>"ListenBrainz token:"
                },
                new SimpleTextBox()
            {
                GetPlaceholder = ()=>"489f696d-86d0-4a9d-80e6-bb00e573231a",
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
                    x.UserToken=s;
                    ((ICanBeToldThatAPartOfMeIsChanged)x).PropertyChanged(x, new("Uploader"));
                }
            }
            ]
        },
        new SimpleRow()
        {
            Content =[
                new SimpleLabel()
                {
                    GetContent = ()=>"Policy"
                },
                new SimpleDropDown()
                {
                    GetOptions = () => Enum.GetNames<Policy>(),
                    GetPlaceholder = () => "Percentage",
                    GetSelection = () =>
                    {
                        if (ConfigObject.Value is ListenBrainzConfig x)
                        {
                            return x.ListenPolicy.ToString();
                        }
                        return Policy.Percentage.ToString();
                    },
                    SetSelection = (s) =>
                    {
                        if (ConfigObject.Value is not ListenBrainzConfig x) return;
                        x.ListenPolicy = Enum.Parse<Policy>(s);
                        ((ICanBeToldThatAPartOfMeIsChanged)x).PropertyChanged(x, new("ListenPolicy"));
                    }
                },
                new SimpleTextBox()
                {
                    GetPlaceholder = ()=>"30",
                    GetContent = () =>
                    {
                        if (ConfigObject.Value is ListenBrainzConfig x)
                        {
                            return x.ListenPolicyValue.ToString();
                        }
                        return "0";
                    },
                    SetContent = (s) =>
                    {
                        if (ConfigObject.Value is not ListenBrainzConfig x) return;
                        if (!uint.TryParse(s, out var z)) return;
                        x.ListenPolicyValue=z;
                        ((ICanBeToldThatAPartOfMeIsChanged)x).PropertyChanged(x, new("ListenPolicyValue"));

                    }
                }
             ]
        },
       
    };
    }
}