// <copyright file="Rotate.cs" company="James Jackson-South">
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
        /// Rotates an image by the given angle in degrees, expanding the image to fit the rotated result.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="source">The image to rotate.</param>
        /// <param name="degrees">The angle in degrees to perform the rotation.</param>
        /// <returns>The <see cref="Image"/></returns>
        public static Image<TColor> Rotate<TColor>(this Image<TColor> source, float degrees)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return Rotate(source, degrees, true);
        }

        /// <summary>
        /// Rotates and flips an image by the given instructions.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="source">The image to rotate.</param>
        /// <param name="rotateType">The <see cref="RotateType"/> to perform the rotation.</param>
        /// <returns>The <see cref="Image"/></returns>
        public static Image<TColor> Rotate<TColor>(this Image<TColor> source, RotateType rotateType)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return Rotate(source, (float)rotateType, false);
        }

        /// <summary>
        /// Rotates an image by the given angle in degrees.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="source">The image to rotate.</param>
        /// <param name="degrees">The angle in degrees to perform the rotation.</param>
        /// <param name="expand">Whether to expand the image to fit the rotated result.</param>
        /// <returns>The <see cref="Image"/></returns>
        public static Image<TColor> Rotate<TColor>(this Image<TColor> source, float degrees, bool expand)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            RotateProcessor<TColor> processor = new RotateProcessor<TColor> { Angle = degrees, Expand = expand };
            return source.Apply(source.Bounds, processor);
        }
    }
}
