using SilverAudioPlayer.Shared;
using SilverConfig;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
    [XmlIgnore] public bool _AllowedRead = true;

    [XmlIgnore] public bool AllowedToRead => _AllowedRead;

    public event PropertyChangedEventHandler? PropertyChanged;
}
