namespace GenericImage
{
    using GenericImage.PackedVectors;

    public class ImageRgba32 : ImageBase<Rgba32>
    {
        /// <inheritdocs/>
        public override IPixelAccessor Lock()
        {
            return new PixelAccessorRgba32(this);
        }
    }
}
