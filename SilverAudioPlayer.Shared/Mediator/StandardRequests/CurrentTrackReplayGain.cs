namespace SilverAudioPlayer.Shared.StandardRequests;

public record RequestReplayGainInfo : IQuery<ReplayGainInfo?>
{
    public static readonly RequestReplayGainInfo Instance = new();
}
public record ReplayGainInfo
{
    public bool Success;
    /// <summary>
    /// In decibels
    /// </summary>
    public float? Peak;
    /// <summary>
    /// In decibels
    /// </summary>
    public float? Gain;
    public bool Album;
}