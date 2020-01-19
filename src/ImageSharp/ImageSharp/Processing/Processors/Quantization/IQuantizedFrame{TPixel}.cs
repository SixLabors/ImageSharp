// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization
{
    /// <summary>
    /// Defines an abstraction to represent a quantized image frame where the pixels indexed by a color palette.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    public interface IQuantizedFrame<TPixel> : IDisposable
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Gets the width of this <see cref="QuantizedFrame{TPixel}"/>.
        /// </summary>
        int Width { get; }

        /// <summary>
        /// Gets the height of this <see cref="QuantizedFrame{TPixel}"/>.
        /// </summary>
        int Height { get; }

        /// <summary>
        /// Gets the color palette of this <see cref="QuantizedFrame{TPixel}"/>.
        /// </summary>
        ReadOnlyMemory<TPixel> Palette { get; }

        /// <summary>
        /// Gets the pixels of this <see cref="QuantizedFrame{TPixel}"/>.
        /// </summary>
        /// <returns>The <see cref="Span{T}"/>The pixel span.</returns>
        ReadOnlySpan<byte> GetPixelSpan();
    }
}