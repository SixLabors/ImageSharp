// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
    /// <inheritdoc />
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
        IJpegComponent[] Components { get; }

        /// <summary>
        /// Gets the quantization tables, in zigzag order.
        /// </summary>
        Block8x8F[] QuantizationTables { get; }
    }
}
