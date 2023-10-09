namespace SilverAudioPlayer.Shared;

public interface IWakeLockProvider : ICodeInformation
{
    public void WakeLock();
    public void UnWakeLock();
}