using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jpeg.Common
{
    /// <summary>
    /// Common interface to represent raw Jpeg components.
    /// </summary>
    internal interface IJpegComponent
    {
        /// <summary>
        /// Gets the component's position in the components array.
        /// </summary>
        int Index { get; }

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

        /// <summary>
        /// Gets the index of the quantization table for this block.
        /// </summary>
        int QuantizationTableIndex { get; }

        /// <summary>
        /// Gets the <see cref="Buffer2D{Block8x8}"/> storing the "raw" frequency-domain decoded blocks.
        /// We need to apply IDCT, dequantiazition and unzigging to transform them into color-space blocks.
        /// </summary>
        Buffer2D<Block8x8> SpectralBlocks { get; }
    }
}