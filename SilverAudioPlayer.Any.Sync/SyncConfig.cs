using System.ComponentModel;
using System.Xml.Serialization;
using SilverAudioPlayer.Shared;
using SilverConfig;

namespace SilverAudioPlayer.Any.Sync;

public class SyncConfig : INotifyPropertyChanged, ICanBeToldThatAPartOfMeIsChanged
{
    [Comment("used verbatim when calling FFmpeg")]

    public string PreferredFileExtension { get; set; } = ".mp3";
    [Comment("used verbatim when calling FFmpeg")]
    public string FFmpegOptions { get; set; } = "-i \"{in}\" -b:a 320k \"{out}\"";

    [Comment("used verbatim when calling FFmpeg")]
    public string PathToStore { get; set; } = "{{Album}}";
    [Comment("used verbatim when calling FFmpeg")]
    public string NameFormatting { get; set; } = "{DiscNumber}.{TrackNumber}.{Title}{Extension}";
    void ICanBeToldThatAPartOfMeIsChanged.PropertyChanged(object e, PropertyChangedEventArgs a)
    {
        PropertyChanged?.Invoke(e, a);
    }
    [XmlIgnore] public bool AllowedToRead { get; set; } = true;

    public event PropertyChangedEventHandler? PropertyChanged;
}