// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization
{
    /// <summary>
    /// Contains extension methods for <see cref="IQuantizedFrame{TPixel}"/>.
    /// </summary>
    public static class QuantizedFrameExtensions
    {
        /// <summary>
        /// Gets the representation of the pixels as a <see cref="Span{T}"/> of contiguous memory
        /// at row <paramref name="rowIndex"/> beginning from the the first pixel on that row.
        /// </summary>
        /// <param name="frame">The <see cref="IQuantizedFrame{TPixel}"/>.</param>
        /// <param name="rowIndex">The row.</param>
        /// <typeparam name="TPixel">The pixel type.</typeparam>
        /// <returns>The pixel row as a <see cref="ReadOnlySpan{T}"/>.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static ReadOnlySpan<byte> GetRowSpan<TPixel>(this IQuantizedFrame<TPixel> frame, int rowIndex)
            where TPixel : struct, IPixel<TPixel>
            => frame.GetPixelSpan().Slice(rowIndex * frame.Width, frame.Width);
    }
}