using SilverAudioPlayer.Shared;
using SilverConfig;
using System.ComponentModel;
using System.Xml.Serialization;

namespace SilverAudioPlayer.Windows.MusicStatusInterface.FortniteCarRadio
{
    public class FortniteCarRadioConfig : INotifyPropertyChanged, ICanBeToldThatAPartOfMeIsChanged
    {
        void ICanBeToldThatAPartOfMeIsChanged.PropertyChanged(object e, PropertyChangedEventArgs a)
        {
            PropertyChanged?.Invoke(e, a);
        }
        [Comment("Volume default")]
        public byte VolDef { get; set; } = 40;
        [Comment("Volume when in vechicle")]
        public byte VolVeh { get; set; } = 100;

        [XmlIgnore] public bool _AllowedRead = true;

        [XmlIgnore] public bool AllowedToRead => _AllowedRead;

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}