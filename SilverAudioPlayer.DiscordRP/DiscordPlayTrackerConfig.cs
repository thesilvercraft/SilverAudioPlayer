using System.ComponentModel;
using System.Xml.Serialization;
using SilverAudioPlayer.Shared;
using SilverConfig;

namespace SilverAudioPlayer.Any.MusicStatusInterface.DiscordRP;

public class DiscordPlayTrackerConfig : INotifyPropertyChanged, ICanBeToldThatAPartOfMeIsChanged
{
    void ICanBeToldThatAPartOfMeIsChanged.PropertyChanged(object e, PropertyChangedEventArgs a)
    {
        PropertyChanged?.Invoke(e, a);
    }

    [Comment("The uploader to use")] public Uploader Uploader { get; set; } = Uploader.None;
    public bool CustomButtonEnabled  { get; set; } = false;
    public string CustomButtonLabel { get; set; } = "ListenBrainz";
    public string CustomButtonUrl { get; set; } = "https://listenbrainz.org/user/silverdiamond/";
    

    [XmlIgnore] public bool AllowedToRead { get; set; } = true;

    public event PropertyChangedEventHandler? PropertyChanged;
}