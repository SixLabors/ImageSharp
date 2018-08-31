// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Memory;

namespace SixLabors.ImageSharp.Advanced
{
    /// <summary>
    /// Encapsulates the basic properties and methods required to manipulate images.
    /// </summary>
    /// <typeparam name="TPixel">The type of the pixel.</typeparam>
    internal interface IPixelSource<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Gets the pixel buffer.
        /// </summary>
        Buffer2D<TPixel> PixelBuffer { get; }
    }
}