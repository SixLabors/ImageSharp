// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Advanced
{
    /// <summary>
    /// Extension methods over Image{TPixel}
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Gets the representation of the pixels as an area of contiguous memory in the given pixel format.
        /// </summary>
        /// <typeparam name="TPixel">The type of the pixel.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns>The <see cref="Span{TPixel}"/></returns>
        public static Span<TPixel> GetPixelSpan<TPixel>(this ImageBase<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
            => GetSpan(source);

        /// <summary>
        /// Gets a <see cref="Span{TPixal}"/> representing the row 'y' beginning from the the first pixel on that row.
        /// </summary>
        /// <typeparam name="TPixel">The type of the pixel.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="row">The row.</param>
        /// <returns>The <see cref="Span{TPixel}"/></returns>
        public static Span<TPixel> GetPixelRowSpan<TPixel>(this ImageBase<TPixel> source, int row)
            where TPixel : struct, IPixel<TPixel>
            => GetSpan(source).Slice(row * source.Width, source.Width);

        /// <summary>
        /// Gets the span.
        /// </summary>
        /// <typeparam name="TPixel">The type of the pixel.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns>The span retuned from Pixel source</returns>
        private static Span<TPixel> GetSpan<TPixel>(IPixelSource<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
            => source.Span;
    }
}
