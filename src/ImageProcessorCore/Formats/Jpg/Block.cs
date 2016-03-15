namespace ImageProcessorCore.Formats
{
    internal class Block
    {
        public const int blockSize = 64;
        private int[] _data;

        public Block()
        {
            _data = new int[blockSize];
        }

        public int this[int idx]
        {
            get { return _data[idx]; }
            set { _data[idx] = value; }
        }
    }
}
