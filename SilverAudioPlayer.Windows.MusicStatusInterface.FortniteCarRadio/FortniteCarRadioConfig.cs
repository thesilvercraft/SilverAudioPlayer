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
        [Comment("Volume when in vehicle")]
        public byte VolVeh { get; set; } = 100;

        [XmlIgnore] public bool AllowedToRead { get; set; } = true;


        public event PropertyChangedEventHandler? PropertyChanged;
    }
}