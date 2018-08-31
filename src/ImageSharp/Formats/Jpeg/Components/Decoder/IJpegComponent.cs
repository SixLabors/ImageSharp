// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Memory;
using SixLabors.Memory;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
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
        /// Gets the number of blocks in this component as <see cref="Size"/>
        /// </summary>
        Size SizeInBlocks { get; }

        /// <summary>
        /// Gets the horizontal and the vertical sampling factor as <see cref="Size"/>
        /// </summary>
        Size SamplingFactors { get; }

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
        /// We need to apply IDCT and dequantiazition to transform them into color-space blocks.
        /// </summary>
        Buffer2D<Block8x8> SpectralBlocks { get; }

        /// <summary>
        /// Gets a reference to the <see cref="Block8x8"/> at the given row and column index from <see cref="SpectralBlocks"/>
        /// </summary>
        /// <param name="column">The column</param>
        /// <param name="row">The row</param>
        /// <returns>The <see cref="Block8x8"/></returns>
        ref Block8x8 GetBlockReference(int column, int row);
    }
}