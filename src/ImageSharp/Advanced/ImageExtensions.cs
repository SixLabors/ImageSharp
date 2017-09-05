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
        public static Span<TPixel> GetPixelSpan<TPixel>(this ImageFrame<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
            => GetSpan(source);

        /// <summary>
        /// Gets the representation of the pixels as an area of contiguous memory in the given pixel format.
        /// </summary>
        /// <typeparam name="TPixel">The type of the pixel.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns>The <see cref="Span{TPixel}"/></returns>
        public static Span<TPixel> GetPixelSpan<TPixel>(this Image<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
            => GetSpan(source);

        /// <summary>
        /// Gets a <see cref="Span{TPixal}"/> representing the row 'y' beginning from the the first pixel on that row.
        /// </summary>
        /// <typeparam name="TPixel">The type of the pixel.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="row">The row.</param>
        /// <returns>The <see cref="Span{TPixel}"/></returns>
        public static Span<TPixel> GetPixelRowSpan<TPixel>(this ImageFrame<TPixel> source, int row)
            where TPixel : struct, IPixel<TPixel>
            => GetSpan(source, row);

        /// <summary>
        /// Gets a <see cref="Span{TPixal}"/> representing the row 'y' beginning from the the first pixel on that row.
        /// </summary>
        /// <typeparam name="TPixel">The type of the pixel.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="row">The row.</param>
        /// <returns>The <see cref="Span{TPixel}"/></returns>
        public static Span<TPixel> GetPixelRowSpan<TPixel>(this Image<TPixel> source, int row)
            where TPixel : struct, IPixel<TPixel>
            => GetSpan(source, row);

        /// <summary>
        /// Gets the span.
        /// </summary>
        /// <typeparam name="TPixel">The type of the pixel.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns>The span retuned from Pixel source</returns>
        private static Span<TPixel> GetSpan<TPixel>(IImageFrame<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
            => source.PixelBuffer.Span;

        /// <summary>
        /// Gets the span.
        /// </summary>
        /// <typeparam name="TPixel">The type of the pixel.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="row">The row.</param>
        /// <returns>
        /// The span retuned from Pixel source
        /// </returns>
        private static Span<TPixel> GetSpan<TPixel>(IImageFrame<TPixel> source, int row)
            where TPixel : struct, IPixel<TPixel>
            => source.PixelBuffer.Span.Slice(row * source.Width, source.Width);

        /// <summary>
        /// Gets the bounds of the image.
        /// </summary>
        /// <typeparam name="TPixel">The Pixel format.</typeparam>
        /// <param name="source">The source image</param>
        /// <returns>Returns the bounds of the image</returns>
        public static Configuration Configuration<TPixel>(this ImageFrame<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
            => source?.Parent?.ImageConfiguration ?? SixLabors.ImageSharp.Configuration.Default;

        /// <summary>
        /// Gets the bounds of the image.
        /// </summary>
        /// <typeparam name="TPixel">The Pixel format.</typeparam>
        /// <param name="source">The source image</param>
        /// <returns>Returns the bounds of the image</returns>
        public static Configuration Configuration<TPixel>(this Image<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
            => source?.ImageConfiguration ?? SixLabors.ImageSharp.Configuration.Default;
    }
}
