namespace SilverAudioPlayer.Shared.StandardRequests;

public record LightweightConfigRequest
{
    public string Name { get; set; } = string.Empty;
}