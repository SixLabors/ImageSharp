namespace ImageSharp.Formats.Jpeg.Port
{
    /// <summary>
    /// Represents a jpeg frame
    /// </summary>
    internal class JpegFrame
    {
        /// <summary>
        /// Gets or sets a value indicating whether the fame is extended
        /// </summary>
        public bool Extended { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the fame is progressive
        /// </summary>
        public bool Progressive { get; set; }

        /// <summary>
        /// Gets or sets the precision
        /// </summary>
        public byte Precision { get; set; }

        /// <summary>
        /// Gets or sets the number of scanlines within the frame
        /// </summary>
        public short Scanlines { get; set; }

        /// <summary>
        /// Gets or sets the number of samples per scanline
        /// </summary>
        public short SamplesPerLine { get; set; }
    }
}
