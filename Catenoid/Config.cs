using System.ComponentModel;
using System.Xml.Serialization;
using SilverAudioPlayer.Shared;
using SilverConfig;

namespace Catenoid;

public class Config : INotifyPropertyChanged, ICanBeToldThatAPartOfMeIsChanged
{

    [Comment("Does the player not loop (None), loop (One) song, or loop the entire (Queue)")]
    public RepeatState LoopType { get; set; } = RepeatState.None;

    [Comment("1-100 number, linearity not guaranteed.")]
    public byte Volume { get; set; } = 70;
    [Comment("Folder suggested to OS when the open file dialog is chosen, can be 'Music','Desktop','Downloads','Pictures','Videos' (user's special folders), any other valid folder path or nothing, if its nothing the os wont be given a suggested starting point.")]

    public string DialogStartLoc { get; set; } = "";

    public bool DisableAlbumArtBlur { get; set; } = false;
    public float AlbumArtTransparency { get; set; } = 0.25f;
    [Comment("Does the audio player save the tracks in a playlist upon closing?")]
    public bool AutoSave {get;set;}=false;
    [XmlIgnore] public bool AllowedToRead{get;set;}=true;
    public SerializableDictionary<string, string> PreferredPlayers { get; set; } = [];
    [Comment("How much and Where should the player log? (Unknown, None (no player core logs, parts of the player may ignore this and implement their own logging even if they really shouldn't), LogWindow (log in memory, show in a window, on crash save said memory's last 50k characters), System (log to stdout) )")]
    public ConfigStyle UserLogPreferences {get;set;} = ConfigStyle.Unknown;
    void ICanBeToldThatAPartOfMeIsChanged.PropertyChanged(object e, PropertyChangedEventArgs a)
    {
        PropertyChanged?.Invoke(e, a);
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}

public enum ConfigStyle
{
    /// <summary>
    /// The user hasn't clicked on anything in the config prompt yet
    /// </summary>
    Unknown,
    /// <summary>
    /// absolutely no logs, unless debugger is attached on debug build
    /// </summary>
    None,
    /// <summary>
    /// log in memory, show in window
    /// </summary>
    LogWindow,
    /// <summary>
    /// log to system
    /// </summary>
    System
}
