// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Extension methods for the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Rotates an image by the given angle in degrees, expanding the image to fit the rotated result.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to rotate.</param>
        /// <param name="degrees">The angle in degrees to perform the rotation.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        public static IImageProcessingContext<TPixel> Rotate<TPixel>(this IImageProcessingContext<TPixel> source, float degrees)
            where TPixel : struct, IPixel<TPixel>
        {
            return Rotate(source, degrees, true);
        }

        /// <summary>
        /// Rotates and flips an image by the given instructions.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to rotate.</param>
        /// <param name="rotateType">The <see cref="RotateType"/> to perform the rotation.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        public static IImageProcessingContext<TPixel> Rotate<TPixel>(this IImageProcessingContext<TPixel> source, RotateType rotateType)
            where TPixel : struct, IPixel<TPixel>
        => Rotate(source, (float)rotateType, false);

        /// <summary>
        /// Rotates an image by the given angle in degrees.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image to rotate.</param>
        /// <param name="degrees">The angle in degrees to perform the rotation.</param>
        /// <param name="expand">Whether to expand the image to fit the rotated result.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        public static IImageProcessingContext<TPixel> Rotate<TPixel>(this IImageProcessingContext<TPixel> source, float degrees, bool expand)
            where TPixel : struct, IPixel<TPixel>
        => source.ApplyProcessor(new RotateProcessor<TPixel> { Angle = degrees, Expand = expand });
    }
}
