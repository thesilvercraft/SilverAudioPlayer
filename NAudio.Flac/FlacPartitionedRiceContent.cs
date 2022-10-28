namespace NAudio.Flac
{
    internal class FlacPartitionedRiceContent
    {
        private int _capByOrder = -1;
        public int[] Parameters;
        public int[] RawBits;

        public void UpdateSize(int partitionOrder)
        {
            if (_capByOrder < partitionOrder)
            {
                var size = 1 << partitionOrder;
                Parameters = new int[size];
                RawBits = new int[size];

                _capByOrder = partitionOrder;
            }
        }
    }
}