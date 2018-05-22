// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;

using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
    /// <inheritdoc />
    /// <summary>
    /// Represents decompressed, unprocessed jpeg data with spectral space <see cref="IJpegComponent" />-s.
    /// </summary>
    internal interface IRawJpegData : IDisposable
    {
        /// <summary>
        /// Gets the image size in pixels.
        /// </summary>
        Size ImageSizeInPixels { get; }

        /// <summary>
        /// Gets the number of coponents.
        /// </summary>
        int ComponentCount { get; }

        /// <summary>
        /// Gets the color space
        /// </summary>
        JpegColorSpace ColorSpace { get; }

        /// <summary>
        /// Gets the components.
        /// </summary>
        IEnumerable<IJpegComponent> Components { get; }

        /// <summary>
        /// Gets the quantization tables, in zigzag order.
        /// </summary>
        Block8x8F[] QuantizationTables { get; }
    }
}