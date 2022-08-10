// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
    /// <summary>
    /// Represents decompressed, unprocessed jpeg data with spectral space <see cref="IJpegComponent" />-s.
    /// </summary>
    internal interface IRawJpegData : IDisposable
    {
        /// <summary>
        /// Gets the color space
        /// </summary>
        JpegColorSpace ColorSpace { get; }

        /// <summary>
        /// Gets the components.
        /// </summary>
        JpegComponent[] Components { get; }

        /// <summary>
        /// Gets the quantization tables, in natural order.
        /// </summary>
        Block8x8F[] QuantizationTables { get; }
    }
}
