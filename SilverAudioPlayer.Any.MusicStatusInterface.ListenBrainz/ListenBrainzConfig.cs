using System.ComponentModel;
using System.Xml.Serialization;
using SilverAudioPlayer.Shared;
using SilverConfig;

namespace SilverAudioPlayer.Any.MusicStatusInterface.ListenBrainz;

public class ListenBrainzConfig : INotifyPropertyChanged, ICanBeToldThatAPartOfMeIsChanged
{
    void ICanBeToldThatAPartOfMeIsChanged.PropertyChanged(object e, PropertyChangedEventArgs a) => PropertyChanged?.Invoke(e, a);

    [Comment("The uploader to use")] public string UserToken { get; set; } = "YourTokenHere";
    [Comment("When to scrobble? Immediately, after y% (Percentage), or after y Seconds (in that track), change takes effect on next song")]
    public Policy ListenPolicy { get; set; } = Policy.Percentage;
    [Comment("When to scrobble (the y in the following answers) ?  after y% , or after y Seconds, change takes effect on next song")]
    public uint ListenPolicyValue { get; set; } = 30;
    [XmlIgnore] public bool AllowedToRead { get; set; } = true;

    public event PropertyChangedEventHandler? PropertyChanged;
}