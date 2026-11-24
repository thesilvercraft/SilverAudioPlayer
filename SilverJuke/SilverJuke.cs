using System.ComponentModel;
using System.Composition;
using System.Net;
using System.Net.Sockets;
using System.Xml.Serialization;
using GenHTTP.Api.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Serilog;
using SilverAudioPlayer.Shared;
using SilverConfig;
using SilverCraft.Ekorensis;

namespace SilverAudioPlayer.Any.SilverJuke;

public class JukeConfig : INotifyPropertyChanged, ICanBeToldThatAPartOfMeIsChanged
{
    [Comment("Should the web interface allow loading URLs")]
    public bool AllowLoading { get; set; } = true;
    public bool ReadOnly { get; set; } = false;
    public bool UseSwagger { get; set; } = false;
    public bool UseSwaggerUI { get; set; } = false;

    [Comment("Should the web interface show a basic html/js interface")]
    public bool UseSlopUI { get; set; } = true;

    [Comment("Use ASP.NET Core Kestrel instead of the lightweight GenHTTP server (requires restart)")]
    public bool UseKestrel { get; set; } = false;

    [XmlIgnore] public int Port { get; set; } = 36169;

    void ICanBeToldThatAPartOfMeIsChanged.PropertyChanged(object e, PropertyChangedEventArgs a)
    {
        PropertyChanged?.Invoke(e, a);
    }

    [XmlIgnore] public bool AllowedToRead { get; set; } = true;

    public event PropertyChangedEventHandler? PropertyChanged;
}

[Export(typeof(IMusicStatusInterface))]
public class SilverJuke : IMusicStatusInterface, IAmOnceAgainAskingYouForYourMemory, IAmConfigurable
{
    public ObjectToRemember ConfigObject =
        new(Guid.Parse("e5552a99-dfee-4eaa-b846-5c5a4180ae32"), new JukeConfig());
    public IEnumerable<ObjectToRemember> ObjectsToRememberForMe => [ConfigObject];

    public IEnumerable<IConfigurableElement> GetElements()
    {
        return
        [
            new SimpleCheckBox
            {
                GetContent = () => "Allow loading URLs",
                GetChecked = () => ConfigObject.Value is not JukeConfig x || x.AllowLoading,
                Checked = c =>
                {
                    if (ConfigObject.Value is not JukeConfig x) return;
                    x.AllowLoading = c;
                    ((ICanBeToldThatAPartOfMeIsChanged)x).PropertyChanged(x, new("AllowLoading"));
                }
            },
            new SimpleCheckBox
            {
                GetContent = () => "Read-only mode",
                GetChecked = () => ConfigObject.Value is JukeConfig { ReadOnly: true },
                Checked = c =>
                {
                    if (ConfigObject.Value is not JukeConfig x) return;
                    x.ReadOnly = c;
                    ((ICanBeToldThatAPartOfMeIsChanged)x).PropertyChanged(x, new("ReadOnly"));
                }
            },
            new SimpleCheckBox
            {
                GetContent = () => "Enable Web UI",
                GetChecked = () => ConfigObject.Value is not JukeConfig x || x.UseSlopUI,
                Checked = c =>
                {
                    if (ConfigObject.Value is not JukeConfig x) return;
                    x.UseSlopUI = c;
                    ((ICanBeToldThatAPartOfMeIsChanged)x).PropertyChanged(x, new("UseSlopUI"));
                }
            },
            new SimpleCheckBox
            {
                GetContent = () => "Enable Swagger OpenAPI",
                GetChecked = () => ConfigObject.Value is JukeConfig { UseSwagger: true },
                Checked = c =>
                {
                    if (ConfigObject.Value is not JukeConfig x) return;
                    x.UseSwagger = c;
                    ((ICanBeToldThatAPartOfMeIsChanged)x).PropertyChanged(x, new("UseSwagger"));
                }
            },
            new SimpleCheckBox
            {
                GetContent = () => "Enable Swagger UI",
                GetChecked = () => ConfigObject.Value is JukeConfig { UseSwaggerUI: true },
                Checked = c =>
                {
                    if (ConfigObject.Value is not JukeConfig x) return;
                    x.UseSwaggerUI = c;
                    ((ICanBeToldThatAPartOfMeIsChanged)x).PropertyChanged(x, new("UseSwaggerUI"));
                }
            },
            new SimpleCheckBox
            {
                GetContent = () => "Use Kestrel instead of GenHTTP (requires restart)",
                GetChecked = () => ConfigObject.Value is JukeConfig { UseKestrel: true },
                Checked = c =>
                {
                    if (ConfigObject.Value is not JukeConfig x) return;
                    x.UseKestrel = c;
                    ((ICanBeToldThatAPartOfMeIsChanged)x).PropertyChanged(x, new("UseKestrel"));
                }
            }
        ];
    }

    public string Name => "SilverJuke";

    public string Description =>
        "A SilverJuke server. Provides a simple http interface to control the audio player. Access it at http://localhost:36169";

    public WrappedStream? Icon => new WrappedEmbeddedResourceStream(typeof(SilverJuke).Assembly,
        "SilverAudioPlayer.Any.SilverJuke.juke.svg");
    public Version? Version => typeof(SilverJuke).Assembly.GetName().Version;

    public string Licenses => "SilverJuke is licensed under the GPL 3.0 license.\n";

    private readonly List<Tuple<Uri, URLType>> _links = [];
    public List<Tuple<Uri, URLType>>? Links => _links;

    public bool IsStarted => _IsStarted;

    private bool _IsStarted;
    private readonly Lock _serverLock = new();
    private WebApplication? _kestrelApp;
    private IServerHost? _genHttpHost;
    private JukeConfig? jukeConfig;

    internal static bool IsLocalOrLan(IPAddress? ip)
    {
        if (ip == null) return false;
        if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal) return true;
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();

        if (IPAddress.IsLoopback(ip))
            return true;

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = ip.GetAddressBytes();
            if (bytes[0] == 127) return true;
            if (bytes[0] == 10) return true;
            if (bytes[0] == 192 && bytes[1] == 168) return true;
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
        }

        return false;
    }

    public void StartIPC(IMusicStatusInterfaceListener listener)
    {
        lock (_serverLock)
        {
            if (_IsStarted) return;
            jukeConfig = ConfigObject.Value as JukeConfig ?? new JukeConfig();
            _links.Clear();
            _IsStarted = true;

            if (jukeConfig.UseKestrel)
            {
                _kestrelApp = SilverJukeKestrelHost.Start(jukeConfig, listener, _links, jukeConfig.Port);
            }
            else
            {
                _genHttpHost = SilverJukeGenHttpHost.Start(jukeConfig, listener, _links, jukeConfig.Port);
            }
        }
    }

    public void StopIPC(IMusicStatusInterfaceListener listener)
    {
        StopServer();
    }

    private void StopServer()
    {
        WebApplication? kestrelToStop;
        IServerHost? genHttpToStop;
        lock (_serverLock)
        {
            _IsStarted = false;
            kestrelToStop = _kestrelApp;
            genHttpToStop = _genHttpHost;
            _kestrelApp = null;
            _genHttpHost = null;
        }

        if (genHttpToStop is not null)
        {
            try
            {
                SilverJukeGenHttpHost.Stop(genHttpToStop);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error stopping GenHTTP SilverJuke server");
            }
        }

        if (kestrelToStop is not null)
        {
            try
            {
                SilverJukeKestrelHost.Stop(kestrelToStop);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error stopping Kestrel SilverJuke server");
            }
        }
    }

    public void Dispose()
    {
        StopServer();
        GC.SuppressFinalize(this);
    }
}