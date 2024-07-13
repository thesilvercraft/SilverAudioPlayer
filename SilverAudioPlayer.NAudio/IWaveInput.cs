#if CSCore
using SilverCraft.CSCore;
using NAudio.Wave;

namespace SilverAudioPlayer.Any.PlayProvider.NAudio;

public interface IWaveInput : IWaveSource, IWaveProvider;
#endif