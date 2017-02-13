// <copyright file="Dither.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;

    using ImageSharp.Dithering;
    using ImageSharp.Processing.Processors;

    /// <summary>
    /// Extension methods for the <see cref="Image"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Alters the alpha component of the image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="diffuser">The diffusion algorithm to apply.</param>
        /// <param name="threshold">The threshold to apply binarization of the image. Must be between 0 and 1.</param>
        /// <returns>The <see cref="Image{TColor}"/>.</returns>
        public static Image<TColor> Dither<TColor>(this Image<TColor> source, IErrorDiffuser diffuser, float threshold)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return Dither(source, diffuser, threshold, source.Bounds);
        }

        /// <summary>
        /// Alters the alpha component of the image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="diffuser">The diffusion algorithm to apply.</param>
        /// <param name="threshold">The threshold to apply binarization of the image. Must be between 0 and 1.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image<TColor> Dither<TColor>(this Image<TColor> source, IErrorDiffuser diffuser, float threshold, Rectangle rectangle)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            source.ApplyProcessor(new ErrorDiffusionDitherProcessor<TColor>(diffuser, threshold), rectangle);
            return source;
        }
    }
}
