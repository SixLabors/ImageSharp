// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Helpers
{
    /// <summary>
    /// Extension methods over Image{TPixel}
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Gets the bounds of the image.
        /// </summary>
        /// <typeparam name="TPixel">The Pixel format.</typeparam>
        /// <param name="source">The source image</param>
        /// <returns>Returns the bounds of the image</returns>
        public static Rectangle Bounds<TPixel>(this Image<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
            => new Rectangle(0, 0, source.Width, source.Height);

        /// <summary>
        /// Gets the bounds of the image.
        /// </summary>
        /// <typeparam name="TPixel">The Pixel format.</typeparam>
        /// <param name="source">The source image</param>
        /// <returns>Returns the bounds of the image</returns>
        public static Rectangle Bounds<TPixel>(this ImageFrame<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
            => new Rectangle(0, 0, source.Width, source.Height);

        /// <summary>
        /// Gets the size of the image.
        /// </summary>
        /// <typeparam name="TPixel">The Pixel format.</typeparam>
        /// <param name="source">The source image</param>
        /// <returns>Returns the bounds of the image</returns>
        public static Size Size<TPixel>(this Image<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
            => new Size(source.Width, source.Height);

        /// <summary>
        /// Gets the size of the image.
        /// </summary>
        /// <typeparam name="TPixel">The Pixel format.</typeparam>
        /// <param name="source">The source image</param>
        /// <returns>Returns the bounds of the image</returns>
        public static Size Size<TPixel>(this ImageFrame<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
            => new Size(source.Width, source.Height);
    }
}
