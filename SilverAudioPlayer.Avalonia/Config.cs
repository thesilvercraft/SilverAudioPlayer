using SilverAudioPlayer.Shared;
using SilverConfig;
using SilverConfig.CobaltExtensions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace SilverAudioPlayer.Avalonia
{
    public class Config: INotifyPropertyChanged, ILetNotify
    {
        [Comment("Does the player not loop (None), loop (One) song, or loop the entire (Queue)")]
        public RepeatState LoopType { get; set; } = RepeatState.None;
        [Comment("1-100 number, liniarity not guaranteed.")]
        public byte Volume { get; set; } = 70;

        [XmlIgnore]
        public bool AllowedToRead => _AllowedRead;
        [XmlIgnore]

        public bool _AllowedRead = true;

        public event PropertyChangedEventHandler? PropertyChanged;

        public void Invoke(object e, PropertyChangedEventArgs a)
        {
            PropertyChanged?.Invoke(e, a);
        }
    }
}
