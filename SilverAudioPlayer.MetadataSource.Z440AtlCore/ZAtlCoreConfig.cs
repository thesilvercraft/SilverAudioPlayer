using SilverAudioPlayer.Shared;
using SilverConfig;
using System.ComponentModel;
using System.Xml.Serialization;

namespace SilverAudioPlayer.Any.MetadataSource.Z440AtlCore;

public class ZAtlCoreConfig : INotifyPropertyChanged, ICanBeToldThatAPartOfMeIsChanged
{
    void ICanBeToldThatAPartOfMeIsChanged.PropertyChanged(object e, PropertyChangedEventArgs a)
    {
        PropertyChanged?.Invoke(e, a);
    }
    [Comment("Is ZAtlCore allowed to read midi metadata")]
    public bool ReadMidiMetadata { get; set; } = false;
    [XmlIgnore] public bool AllowedToRead {get;set;}= true;

    public event PropertyChangedEventHandler? PropertyChanged;
}
