namespace SilverAudioPlayer.Shared.Metadata;

public interface IAdditionalMetadataGatherer
{
    public IMetadata? GatherAdditionalMetadata(IMetadataCombo original);
}