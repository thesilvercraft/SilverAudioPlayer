using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Avalonia.Controls;
using SilverAudioPlayer.Shared;
using SilverConfig;
using Swordfish.NET.Collections;

namespace Catenoid;

public class Config :INotifyPropertyChanged,ICanBeToldThatAPartOfMeIsChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    void ICanBeToldThatAPartOfMeIsChanged.PropertyChanged(object e, PropertyChangedEventArgs a)
    {
        PropertyChanged?.Invoke(e, a);
    }
    [Comment("Does the player not loop (None), loop (One) song, or loop the entire (Queue)")]
    public RepeatState LoopType { get; set; } = RepeatState.None;

    [Comment("1-100 number, linearity not guaranteed.")]
    public byte Volume { get=> _volume; set=>this.ThrottleSet(ref _volume, value); }
    [XmlIgnore] 
    private byte _volume =70;
    [Comment("Folder suggested to OS when the open file dialog is chosen, can be 'Music','Desktop','Downloads','Pictures','Videos' (user's special folders), any other valid folder path or nothing, if its nothing the os wont be given a suggested starting point.")]

    public string DialogStartLoc { get; set; } = "";

    public bool DisableAlbumArtBlur { get; set; } = false;
    public float AlbumArtTransparency { get; set; } = 0.25f;
    [Comment("Does the audio player save the tracks in a playlist upon closing?")]
    public bool AutoSave { get=> _autoSave; set=>this.ThrottleSet(ref _autoSave, value); }

    private bool _autoSave=false;
    [Comment("At which interval does the audio player save the current tracks and position in seconds? (0=disable)")]
    public double AutoSaveInterval { get=> _autoSaveInterval; set=>this.ThrottleSet(ref _autoSaveInterval, value); }

    private double _autoSaveInterval = 0;
    public bool SaveLoadLoc{get;set;}=true;
    public int[] Pos{get;set;}=[0,0];
    public double[] Size{get;set;}=[0,0];
    [Comment("How many lyrics should be shown from the current lyric, -1 to disable autoscroll, -2 for auto")]
    public int LyricsAdvance { get; set; } = -2;

    public double LyricsFontSize { get; set; } = 40;
    public WindowState State{get;set;}=WindowState.Maximized;
    [XmlIgnore] public bool AllowedToRead{get;set;}=true;
    public SerializableDictionary<string, string> PreferredPlayers { get; set; } = [];
    [Comment("How much and Where should the player log? (None (no player core logs, parts of the player may ignore this and implement their own logging even if they really shouldn't), LogWindow (log in memory, show in a window, on crash save said memory's last 50k characters), System (log to stdout) )")]
    public ConfigStyle UserLogPreferences {get;set;} = ConfigStyle.System;

    public HashSet<string> BlockedPlugins { get; set; } = [];

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
public static class ThrottleConfigExtensions
{
    private static readonly Dictionary<long, Task> KeyPairs=new();
    public static TRet ThrottleSet<TObj, TRet>(this TObj config, ref TRet backing, TRet value, [CallerMemberName] string? name = null) where TObj : INotifyPropertyChanged,ICanBeToldThatAPartOfMeIsChanged
    {
        ArgumentNullException.ThrowIfNull(name);
        if (EqualityComparer<TRet>.Default.Equals(backing, value))
        {
            return value;
        }
        backing = value;
        var c=config.GetHashCode()<<name.GetHashCode();
        if(!KeyPairs.ContainsKey(c) ||KeyPairs[c].Status is TaskStatus.Canceled or TaskStatus.RanToCompletion or TaskStatus.Faulted )
        {
            KeyPairs[c] = Task.Run(async() =>
            {
                await Task.Delay(100);
                config.PropertyChanged(config, new PropertyChangedEventArgs(name));
            });
        }
        return value;
    }
}