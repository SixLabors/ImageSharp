namespace GenericImage
{
    using GenericImage.PackedVectors;

    public class ImageRgba64 : IImageBase<Rgba64>
    {
        public ImageRgba64(int width, int height)
        {
            this.Width = width;
            this.Height = height;
            this.Pixels = new Rgba64[width * height];
        }

        public Rgba64[] Pixels { get; }

        public int Width { get; }

        public int Height { get; }

        public IPixelAccessor Lock()
        {
            return new PixelAccessorRgba64(this);
        }
    }
}
