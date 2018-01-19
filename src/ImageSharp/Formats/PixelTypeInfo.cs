namespace SixLabors.ImageSharp.Formats
{
    /// <summary>
    /// Stores the raw image pixel type information.
    /// </summary>
    public class PixelTypeInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PixelTypeInfo"/> class.
        /// </summary>
        /// <param name="bitsPerPixel">Color depth, in number of bits per pixel.</param>
        internal PixelTypeInfo(int bitsPerPixel)
        {
            this.BitsPerPixel = bitsPerPixel;
        }

        /// <summary>
        /// Gets color depth, in number of bits per pixel.
        /// </summary>
        public int BitsPerPixel { get; }
    }
}
