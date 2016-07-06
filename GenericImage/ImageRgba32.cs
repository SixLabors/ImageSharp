namespace GenericImage
{
    using GenericImage.PackedVectors;

    public class ImageRgba32 : IImageBase<Rgba32>
    {
        public ImageRgba32(int width, int height)
        {
            this.Width = width;
            this.Height = height;
            this.Pixels = new Rgba32[width * height];
        }

        public Rgba32[] Pixels { get; }

        public int Width { get; }

        public int Height { get; }

        public IPixelAccessor Lock()
        {
            return new PixelAccessorRgba32(this);
        }
    }
}
