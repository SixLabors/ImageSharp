// <copyright file="Rotate.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    using Processors;

    /// <summary>
    /// Extension methods for the <see cref="Image{TColor, TPacked}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Rotates an image by the given angle in degrees, expanding the image to fit the rotated result.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image to rotate.</param>
        /// <param name="degrees">The angle in degrees to perform the rotation.</param>
        /// <returns>The <see cref="Image"/></returns>
        public static Image<TColor, TPacked> Rotate<TColor, TPacked>(this Image<TColor, TPacked> source, float degrees)
            where TColor : struct, IPackedPixel<TPacked>
            where TPacked : struct
        {
            return Rotate(source, degrees, true);
        }

        /// <summary>
        /// Rotates and flips an image by the given instructions.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image to rotate.</param>
        /// <param name="rotateType">The <see cref="RotateType"/> to perform the rotation.</param>
        /// <returns>The <see cref="Image"/></returns>
        public static Image<TColor, TPacked> Rotate<TColor, TPacked>(this Image<TColor, TPacked> source, RotateType rotateType)
            where TColor : struct, IPackedPixel<TPacked>
            where TPacked : struct
        {
            return Rotate(source, (float)rotateType, false);
        }

        /// <summary>
        /// Rotates an image by the given angle in degrees.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image to rotate.</param>
        /// <param name="degrees">The angle in degrees to perform the rotation.</param>
        /// <param name="expand">Whether to expand the image to fit the rotated result.</param>
        /// <returns>The <see cref="Image"/></returns>
        public static Image<TColor, TPacked> Rotate<TColor, TPacked>(this Image<TColor, TPacked> source, float degrees, bool expand)
            where TColor : struct, IPackedPixel<TPacked>
            where TPacked : struct
        {
            RotateProcessor<TColor, TPacked> processor = new RotateProcessor<TColor, TPacked> { Angle = degrees, Expand = expand };
            return source.Process(source.Width, source.Height, source.Bounds, source.Bounds, processor);
        }
    }
}
