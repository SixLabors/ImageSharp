namespace Pfim
{
    public class MipMapOffset
    {
        public MipMapOffset(int width, int height, int stride, int dataOffset, int dataLen)
        {
            Stride = stride;
            Width = width;
            Height = height;
            DataOffset = dataOffset;
            DataLen = dataLen;
        }

        public int Stride { get; }

        public int Width { get; }

        public int Height { get; }

        public int DataOffset { get; }

        public int DataLen { get; }

     
    }
}
