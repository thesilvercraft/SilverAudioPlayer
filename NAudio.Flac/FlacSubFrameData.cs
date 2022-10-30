namespace NAudio.Flac
{
    internal unsafe class FlacSubFrameData
    {
        public FlacPartitionedRiceContent Content = new FlacPartitionedRiceContent();
        public int* DestinationBuffer;
        public int* ResidualBuffer;
    }
}