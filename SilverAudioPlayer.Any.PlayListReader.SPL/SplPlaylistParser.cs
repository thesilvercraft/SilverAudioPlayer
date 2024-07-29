using System.ComponentModel;
using System.Composition;
using System.Diagnostics;
using System.Xml.Serialization;
using SilverAudioPlayer.Shared;
using SilverAudioPlayer.Shared.SilverPlaylist;
using SilverConfig;
using SilverCraft.Ekorensis;
using SilverMagicBytes;

namespace SilverAudioPlayer.Any.PlayListReader.SPL;

[Export(typeof(IPlaylistReader))]
public class SplPlaylistParser : IPlaylistReader, IAmOnceAgainAskingYouForYourMemory, IConfigurableUiPlugin
{
    public ObjectToRemember ConfigObject =
       new(Guid.Parse("dff9078c-7cb2-49b7-8f4a-515cc4256c3c"), new SplPlaylistParserConfig());
    public IEnumerable<ObjectToRemember> ObjectsToRememberForMe => new ObjectToRemember[] { ConfigObject };

    public string Name => "SilverPlaylist reader";
    public WrappedStream? Icon => new WrappedEmbeddedResourceStream(typeof(SplPlaylistParser).Assembly,
        "SilverAudioPlayer.Any.PlayListReader.SPL.icon.svg");
    public string Description => "A simple module that deserializes brotli encoded silverplaylist files";
    public Version? Version => typeof(SplPlaylistParser).Assembly.GetName().Version;
    public string Licenses => "GPL3";
    public List<Tuple<Uri, URLType>>? Links { get; } = [];
    public IReadOnlyList<MimeType> SupportedMimes { get; } = [KnownMimes.SplMime];
    bool GetAllowedBlankOutPos()
    {
        if (ConfigObject.Value is SplPlaylistParserConfig x)
        {
            return x.ShouldBlankOutPos;
        }
        return false;
    }
     bool GetAllowedBlankOutInd()
    {
        if (ConfigObject.Value is SplPlaylistParserConfig x)
        {
            return x.ShouldBlankOutIndex;
        }
        return false;
    }
  public void ConfigureUi(IConfigurableListener env)
    {
        var window = env.TryGetWindow(
        [
            new SimpleCheckBox
            {
                GetContent = () => "Ignore current track index", Checked = c =>
                {
                    if (ConfigObject.Value is not SplPlaylistParserConfig x) return;
                    x.ShouldBlankOutIndex = c;
                    ((ICanBeToldThatAPartOfMeIsChanged)x).PropertyChanged(x, new("ShouldBlankOutIndex"));
                },
                GetChecked = GetAllowedBlankOutInd
            },
            new SimpleCheckBox
            {
                GetContent = () => "Ignore current track position", Checked = c =>
                {
                    if (ConfigObject.Value is not SplPlaylistParserConfig x) return;
                    x.ShouldBlankOutPos = c;
                    ((ICanBeToldThatAPartOfMeIsChanged)x).PropertyChanged(x, new("ShouldBlankOutPos"));
                },
                GetChecked = GetAllowedBlankOutPos
            },
        ]);
        window?.SetTitle("Configuring SilverPlaylist reader");
        window?.Show();
    }

    public bool TryParse(WrappedStream stream, out IPlaylist? playlist)
    {
        try
        {
            using var fs = stream.GetStream();
            var splylist = SilverPlaylistHelper.DeserializeBrotli(fs);
            if (GetAllowedBlankOutPos())
            {
                splylist.CurrentIndexPosition = null;
            }
            if(GetAllowedBlankOutInd())
            {
                splylist.CurrentIndex = null;
            }
            if (splylist != null)
            {
                playlist = new SilverPlayListI(splylist);
                return true;
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
        }

        playlist = null;
        return false;

    }
}
public class SplPlaylistParserConfig : INotifyPropertyChanged, ICanBeToldThatAPartOfMeIsChanged
{
    void ICanBeToldThatAPartOfMeIsChanged.PropertyChanged(object e, PropertyChangedEventArgs a)
    {
        PropertyChanged?.Invoke(e, a);
    }
    [Comment("Should it ignore current track and position (stop's automatic playback of songs after loading a playlist)")]
    public bool ShouldBlankOutIndex { get; set; } = false;
    public bool ShouldBlankOutPos { get; set; } = false;

    [XmlIgnore] public bool AllowedToRead { get; set; } = true;

    public event PropertyChangedEventHandler? PropertyChanged;
}
