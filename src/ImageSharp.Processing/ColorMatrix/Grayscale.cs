// <copyright file="Grayscale.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;

    using Processing;
    using Processing.Processors;

    /// <summary>
    /// Extension methods for the <see cref="Image{TColor}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Applies Grayscale toning to the image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="mode">The formula to apply to perform the operation.</param>
        /// <returns>The <see cref="Image{TColor}"/>.</returns>
        public static Image<TColor> Grayscale<TColor>(this Image<TColor> source, GrayscaleMode mode = GrayscaleMode.Bt709)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return Grayscale(source, source.Bounds, mode);
        }

        /// <summary>
        /// Applies Grayscale toning to the image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <param name="mode">The formula to apply to perform the operation.</param>
        /// <returns>The <see cref="Image{TColor}"/>.</returns>
        public static Image<TColor> Grayscale<TColor>(this Image<TColor> source, Rectangle rectangle, GrayscaleMode mode = GrayscaleMode.Bt709)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            IImageProcessor<TColor> processor = mode == GrayscaleMode.Bt709
                ? (IImageProcessor<TColor>)new GrayscaleBt709Processor<TColor>()
                : new GrayscaleBt601Processor<TColor>();

            return source.Apply(rectangle, processor);
        }
    }
}
