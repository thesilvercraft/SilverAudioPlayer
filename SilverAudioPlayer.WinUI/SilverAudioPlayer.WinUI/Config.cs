using System.ComponentModel;
using System.Xml.Serialization;
using SilverAudioPlayer.Shared;
using SilverConfig;
using SilverConfig.CobaltExtensions;

namespace SilverAudioPlayer.WinUI;

public class Config : INotifyPropertyChanged, ILetNotify
{
    [XmlIgnore] public bool _AllowedRead = true;

    [Comment("Does the player not loop (None), loop (One) song, or loop the entire (Queue)")]
    public RepeatState LoopType { get; set; } = RepeatState.None;

    [Comment("1-100 number, liniarity not guaranteed.")]
    public byte Volume { get; set; } = 70;

    [XmlIgnore] public bool AllowedToRead => _AllowedRead;

    public void Invoke(object e, PropertyChangedEventArgs a)
    {
        PropertyChanged?.Invoke(e, a);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}