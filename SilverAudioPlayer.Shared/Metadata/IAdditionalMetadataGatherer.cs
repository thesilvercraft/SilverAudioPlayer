namespace SilverAudioPlayer.Shared.Metadata;

public interface IAdditionalMetadataGatherer : ICodeInformation
{
    public IMetadata? GatherAdditionalMetadata(IMetadataCombo original);
}