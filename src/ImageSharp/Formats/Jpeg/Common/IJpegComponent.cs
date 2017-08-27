namespace SixLabors.ImageSharp.Formats.Jpeg.Common
{
    internal interface IJpegComponent
    {
        /// <summary>
        /// Gets the number of blocks per line
        /// </summary>
        int WidthInBlocks { get; }

        /// <summary>
        /// Gets the number of blocks per column
        /// </summary>
        int HeightInBlocks { get; }

        /// <summary>
        /// Gets the horizontal sampling factor.
        /// </summary>
        int HorizontalSamplingFactor { get; }

        /// <summary>
        /// Gets the vertical sampling factor.
        /// </summary>
        int VerticalSamplingFactor { get; }
    }
}