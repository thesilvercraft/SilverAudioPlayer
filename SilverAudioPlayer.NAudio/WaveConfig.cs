using System.ComponentModel;
using System.Xml.Serialization;
using SilverAudioPlayer.Shared;
using SilverConfig;

namespace SilverAudioPlayer.NAudio;

public class WaveConfig : INotifyPropertyChanged, ICanBeToldThatAPartOfMeIsChanged
{
    [Comment("Should the wave module try reading the album replaygain first or should it try the track replaygain first")]

    public bool PreferAlbumReplayGain { get; set; } = true;
    public bool EnableReplayGain { get; set; } = true;
    [Comment("When gathering replaygain information use the peak if avaliable to calculate whether the amplification does not clip (aka doesnt go above 1)")]
    public bool TrySafeReplayGain { get; set; } = true;
    [Comment("The preamp to use for tracks without replaygain information (in dB), replaygain must be enabled, TrySafeReplayGain WONT SAVE YOU")]
    public float PreampNoReplayGain { get; set; } = 0;
    [Comment("The preamp to use for tracks with replaygain information (in dB), replaygain must be enabled")]
    public float PreampReplayGain { get; set; } = 0;
    [Comment("PortAudio output device name (or 'Default')")]
    public string PortAudioDevice { get; set; } = "Default";
    [Comment("Target PortAudio stream output latency in milliseconds (higher values improve stability when sharing the device)")]
    public int PortAudioLatencyMs { get; set; } = 50;
    [Comment("Size of the background decode ring buffer in milliseconds")]
    public int DecodeBufferMs { get; set; } = 100;
    void ICanBeToldThatAPartOfMeIsChanged.PropertyChanged(object e, PropertyChangedEventArgs a)
    {
        PropertyChanged?.Invoke(e, a);
    }
    [XmlIgnore] public bool AllowedToRead { get; set; } = true;

    public event PropertyChangedEventHandler? PropertyChanged;
}