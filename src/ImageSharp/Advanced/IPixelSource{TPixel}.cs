// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Advanced
{
    /// <summary>
    /// Allows access to the pixels as an area of contiguous memory in the given pixel format.
    /// </summary>
    /// <typeparam name="TPixel">The type of the pixel.</typeparam>
    internal interface IPixelSource<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Gets the representation of the pixels as an area of contiguous memory in the given pixel format.
        /// </summary>
        Span<TPixel> Span { get; }
    }
}
