namespace GenericImage
{
    public class ImageRgba64 : IImageBase<ulong>
    {
        public ImageRgba64(int width, int height)
        {
            this.Width = width;
            this.Height = height;
            this.Pixels = new ulong[width * height * 4];
        }

        public ulong[] Pixels { get; }

        public int Width { get; }

        public int Height { get; }

        public IPixelAccessor Lock()
        {
            return new PixelAccessorRgba64(this);
        }
    }
}
