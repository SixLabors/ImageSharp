// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
    /// <summary>
    /// Common interface to represent raw Jpeg components.
    /// </summary>
    internal interface IJpegComponent
    {
        /// <summary>
        /// Gets the component id.
        /// </summary>
        byte Id { get; }

        /// <summary>
        /// Gets the component's position in the components array.
        /// </summary>
        int Index { get; }

        /// <summary>
        /// Gets the number of blocks in this component as <see cref="Size"/>
        /// </summary>
        Size SizeInBlocks { get; }

        /// <summary>
        /// Gets the horizontal and the vertical sampling factor as <see cref="Size"/>
        /// </summary>
        Size SamplingFactors { get; }

        /// <summary>
        /// Gets the horizontal sampling factor.
        /// </summary>
        int HorizontalSamplingFactor { get; }

        /// <summary>
        /// Gets the vertical sampling factor.
        /// </summary>
        int VerticalSamplingFactor { get; }

        /// <summary>
        /// Gets the divisors needed to apply when calculating colors.
        /// <see>
        ///     <cref>https://en.wikipedia.org/wiki/Chroma_subsampling</cref>
        /// </see>
        /// In case of 4:2:0 subsampling the values are: Luma.SubSamplingDivisors = (1,1) Chroma.SubSamplingDivisors = (2,2)
        /// </summary>
        Size SubSamplingDivisors { get; }

        /// <summary>
        /// Gets the index of the quantization table for this block.
        /// </summary>
        int QuantizationTableIndex { get; }

        /// <summary>
        /// Gets the <see cref="Buffer2D{Block8x8}"/> storing the "raw" frequency-domain decoded + unzigged blocks.
        /// We need to apply IDCT and dequantization to transform them into color-space blocks.
        /// </summary>
        Buffer2D<Block8x8> SpectralBlocks { get; }

        /// <summary>
        /// Gets or sets DC coefficient predictor.
        /// </summary>
        int DcPredictor { get; set; }

        /// <summary>
        /// Gets or sets the index for the DC table.
        /// </summary>
        int DcTableId { get; set; }

        /// <summary>
        /// Gets or sets the index for the AC table.
        /// </summary>
        int AcTableId { get; set; }

        /// <summary>
        /// Initializes component for future buffers initialization.
        /// </summary>
        /// <param name="maxSubFactorH">Maximal horizontal subsampling factor among all the components.</param>
        /// <param name="maxSubFactorV">Maximal vertical subsampling factor among all the components.</param>
        void Init(int maxSubFactorH, int maxSubFactorV);

        /// <summary>
        /// Allocates the spectral blocks.
        /// </summary>
        /// <param name="fullScan">if set to true, use the full height of a block, otherwise use the vertical sampling factor.</param>
        void AllocateSpectral(bool fullScan);

        /// <summary>
        /// Releases resources.
        /// </summary>
        void Dispose();
    }
}
