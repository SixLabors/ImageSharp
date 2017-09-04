// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.MetaData;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Encapsulates the basic properties and methods required to manipulate images.
    /// </summary>
    /// <typeparam name="TPixel">The type of the pixel.</typeparam>
    internal interface IImageFrame<TPixel> : IImageFrame
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Gets the parent.
        /// </summary>
        Image<TPixel> Parent { get; }

        /// <summary>
        /// Gets the pixel buffer.
        /// </summary>
        Buffer2D<TPixel> PixelBuffer { get; }
    }

    /// <summary>
    /// Encapsulates the basic properties and methods required to manipulate images.
    /// </summary>
    public interface IImageFrame : IDisposable
    {
        /// <summary>
        /// Gets the meta data of the image.
        /// </summary>
        ImageFrameMetaData MetaData { get; }

        /// <summary>
        /// Gets the width.
        /// </summary>
        int Width { get; }

        /// <summary>
        /// Gets the height.
        /// </summary>
        int Height { get; }
    }
}