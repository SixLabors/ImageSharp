namespace ImageProcessorCore
{
    using System;
    using System.Diagnostics;
    using System.Linq;

    /// <summary>
    /// Encapsulates an image, which consists of the pixel data for a graphics image and its attributes.
    /// </summary>
    /// <remarks>
    /// The image data is always stored in <see cref="Bgra32"/> format, where the blue, green, red, and
    /// alpha values are 8 bit unsigned bytes.
    /// </remarks>
    [DebuggerDisplay("Image: {Width}x{Height}")]
    public class Image : GenericImage<Bgra32>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Image"/> class
        /// by making a copy from another image.
        /// </summary>
        /// <param name="other">The other image, where the clone should be made from.</param>
        /// <exception cref="ArgumentNullException"><paramref name="other"/> is null.</exception>
        public Image(Image other)
        {
            // TODO: Check this. Not sure why I was getting a cast warning.
            foreach (ImageFrame frame in other.Frames.Cast<ImageFrame>())
            {
                if (frame != null)
                {
                    this.Frames.Add(new ImageFrame(frame));
                }
            }

            this.RepeatCount = other.RepeatCount;
            this.HorizontalResolution = other.HorizontalResolution;
            this.VerticalResolution = other.VerticalResolution;
            this.CurrentImageFormat = other.CurrentImageFormat;
        }

        /// <inheritdoc/>
        public override IPixelAccessor Lock()
        {
            return new PixelAccessor(this);
        }
    }
}
