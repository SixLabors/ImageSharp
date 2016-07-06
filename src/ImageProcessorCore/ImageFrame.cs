namespace ImageProcessorCore
{
    public class ImageFrame : GenericImageFrame<Bgra32>
    {
        public ImageFrame(ImageFrame frame)
            : base(frame)
        {
        }

        /// <inhertdoc />
        public override IPixelAccessor Lock()
        {
            return new PixelAccessor(this);
        }
    }
}
