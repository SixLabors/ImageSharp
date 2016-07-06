namespace ImageProcessorCore
{
    /// <summary>
    /// Represents a single frame in a animation.
    /// </summary>
    /// <remarks>
    /// The image data is always stored in <see cref="Bgra32"/> format, where the blue, green, red, and
    /// alpha values are 8 bit unsigned bytes.
    /// </remarks>
    public class ImageFrame : ImageBase<Bgra32>, IImageFrame<Bgra32>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFrame"/> class. 
        /// </summary>
        /// <param name="frame">
        /// The frame to create the frame from.
        /// </param>
        public ImageFrame(ImageFrame frame)
            : base(frame)
        {
        }

        /// <inheritdoc />
        public override IPixelAccessor Lock()
        {
            return new PixelAccessor(this);
        }
    }
}
