namespace SilverAudioPlayer.Shared;

public interface IExportCommands
{
    public IEnumerable<ExportedCommand> GetCommands();
    void RaiseCommand(ExportedCommand command);
}