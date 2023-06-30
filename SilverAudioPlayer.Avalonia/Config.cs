using System.ComponentModel;
using System.Xml.Serialization;
using SilverAudioPlayer.Shared;
using SilverConfig;

namespace SilverAudioPlayer.Avalonia;

public class Config : INotifyPropertyChanged, ICanBeToldThatAPartOfMeIsChanged
{
    [XmlIgnore] public bool _AllowedRead = true;

    [Comment("Does the player not loop (None), loop (One) song, or loop the entire (Queue)")]
    public RepeatState LoopType { get; set; } = RepeatState.None;

    [Comment("1-100 number, liniarity not guaranteed.")]
    public byte Volume { get; set; } = 70;
    [Comment("Folder suggested to OS when the open file dialog is chosen, can be 'Music','Desktop','Downloads','Pictures','Videos'  (user's special folders), any other valid folder path or nothing, if its nothing the os wont be given a suggested starting point.")]

    public string DialogStartLoc { get; set; } = "Music";

    public bool DisableAlbumArtBlur { get; set; } = false;
    public float AlbumArtTransparency { get; set; } = 0.25f;
    [XmlIgnore] public bool AllowedToRead => _AllowedRead;
    public SerializableDictionary<string, string> PreferedPlayers { get; set; } = new();

    void ICanBeToldThatAPartOfMeIsChanged.PropertyChanged(object e, PropertyChangedEventArgs a)
    {
        PropertyChanged?.Invoke(e, a);
    }

   

    public event PropertyChangedEventHandler? PropertyChanged;
}