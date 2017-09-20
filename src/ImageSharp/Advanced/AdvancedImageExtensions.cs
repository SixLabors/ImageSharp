// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Advanced
{
    /// <summary>
    /// Extension methods over Image{TPixel}
    /// </summary>
    public static class AdvancedImageExtensions
    {
        /// <summary>
        /// Gets the configuration for the image.
        /// </summary>
        /// <typeparam name="TPixel">The Pixel format.</typeparam>
        /// <param name="source">The source image</param>
        /// <returns>Returns the configuration.</returns>
        public static Configuration GetConfiguration<TPixel>(this Image<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
            => GetConfiguration((IConfigurable)source);

        /// <summary>
        /// Returns a reference to the 0th element of the Pixel buffer,
        /// allowing direct manipulation of pixel data through unsafe operations.
        /// The pixel buffer is a contigous memory area containing Width*Height TPixel elements layed out in row-major order.
        /// </summary>
        /// <typeparam name="TPixel">The Pixel format.</typeparam>
        /// <param name="source">The source image frame</param>
        /// <returns>A pinnable reference the first root of the pixel buffer.</returns>
        public static ref TPixel DangerousGetPinnableReferenceToPixelBuffer<TPixel>(this ImageFrame<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
         => ref DangerousGetPinnableReferenceToPixelBuffer((IPixelSource<TPixel>)source);

        /// <summary>
        /// Returns a reference to the 0th element of the Pixel buffer,
        /// allowing direct manipulation of pixel data through unsafe operations.
        /// The pixel buffer is a contigous memory area containing Width*Height TPixel elements layed out in row-major order.
        /// </summary>
        /// <typeparam name="TPixel">The Pixel format.</typeparam>
        /// <param name="source">The source image</param>
        /// <returns>A pinnable reference the first root of the pixel buffer.</returns>
        public static ref TPixel DangerousGetPinnableReferenceToPixelBuffer<TPixel>(this Image<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
         => ref source.Frames.RootFrame.DangerousGetPinnableReferenceToPixelBuffer();

        /// <summary>
        /// Gets the representation of the pixels as an area of contiguous memory in the given pixel format.
        /// </summary>
        /// <typeparam name="TPixel">The type of the pixel.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns>The <see cref="Span{TPixel}"/></returns>
        internal static Span<TPixel> GetPixelSpan<TPixel>(this ImageFrame<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
            => GetSpan(source);

        /// <summary>
        /// Gets the representation of the pixels as an area of contiguous memory at row 'y' beginning from the the first pixel on that row.
        /// </summary>
        /// <typeparam name="TPixel">The type of the pixel.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="row">The row.</param>
        /// <returns>The <see cref="Span{TPixel}"/></returns>
        internal static Span<TPixel> GetPixelRowSpan<TPixel>(this ImageFrame<TPixel> source, int row)
            where TPixel : struct, IPixel<TPixel>
            => GetSpan(source, row);

        /// <summary>
        /// Gets the representation of the pixels as an area of contiguous memory in the given pixel format.
        /// </summary>
        /// <typeparam name="TPixel">The type of the pixel.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns>The <see cref="Span{TPixel}"/></returns>
        internal static Span<TPixel> GetPixelSpan<TPixel>(this Image<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
            => source.Frames.RootFrame.GetPixelSpan();

        /// <summary>
        /// Gets the representation of the pixels as an area of contiguous memory at row 'y' beginning from the the first pixel on that row.
        /// </summary>
        /// <typeparam name="TPixel">The type of the pixel.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="row">The row.</param>
        /// <returns>The <see cref="Span{TPixel}"/></returns>
        internal static Span<TPixel> GetPixelRowSpan<TPixel>(this Image<TPixel> source, int row)
            where TPixel : struct, IPixel<TPixel>
            => source.Frames.RootFrame.GetPixelRowSpan(row);

        /// <summary>
        /// Gets the span to the backing buffer.
        /// </summary>
        /// <typeparam name="TPixel">The type of the pixel.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns>The span retuned from Pixel source</returns>
        private static Span<TPixel> GetSpan<TPixel>(IPixelSource<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
            => source.PixelBuffer.Span;

        /// <summary>
        /// Gets the span to the backing buffer at the given row.
        /// </summary>
        /// <typeparam name="TPixel">The type of the pixel.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="row">The row.</param>
        /// <returns>
        /// The span retuned from Pixel source
        /// </returns>
        private static Span<TPixel> GetSpan<TPixel>(IPixelSource<TPixel> source, int row)
            where TPixel : struct, IPixel<TPixel>
            => GetSpan(source.PixelBuffer, row);

        /// <summary>
        /// Gets the span to the backing buffer at the given row.
        /// </summary>
        /// <typeparam name="TPixel">The type of the pixel.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="row">The row.</param>
        /// <returns>
        /// The span retuned from Pixel source
        /// </returns>
        private static Span<TPixel> GetSpan<TPixel>(Buffer2D<TPixel> source, int row)
            where TPixel : struct, IPixel<TPixel>
            => source.Span.Slice(row * source.Width, source.Width);

        /// <summary>
        /// Gets the configuration.
        /// </summary>
        /// <param name="source">The source image</param>
        /// <returns>Returns the bounds of the image</returns>
        private static Configuration GetConfiguration(IConfigurable source)
            => source?.Configuration ?? Configuration.Default;

        /// <summary>
        /// Returns a reference to the 0th element of the Pixel buffer.
        /// Such a reference can be used for pinning but must never be dereferenced.
        /// </summary>
        /// <param name="source">The source image frame</param>
        /// <returns>A reference to the element.</returns>
        private static ref TPixel DangerousGetPinnableReferenceToPixelBuffer<TPixel>(IPixelSource<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
            => ref source.PixelBuffer.Span.DangerousGetPinnableReference();
    }
}
