// <copyright file="Skew.cs" company="James Jackson-South">
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
        /// Skews an image by the given angles in degrees, expanding the image to fit the skewed result.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image to skew.</param>
        /// <param name="degreesX">The angle in degrees to perform the rotation along the x-axis.</param>
        /// <param name="degreesY">The angle in degrees to perform the rotation along the y-axis.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/></returns>
        public static Image<TColor, TPacked> Skew<TColor, TPacked>(this Image<TColor, TPacked> source, float degreesX, float degreesY, ProgressEventHandler progressHandler = null)
            where TColor : IPackedVector<TPacked>
            where TPacked : struct
        {
            return Skew(source, degreesX, degreesY, true, progressHandler);
        }

        /// <summary>
        /// Skews an image by the given angles in degrees.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image to skew.</param>
        /// <param name="degreesX">The angle in degrees to perform the rotation along the x-axis.</param>
        /// <param name="degreesY">The angle in degrees to perform the rotation along the y-axis.</param>
        /// <param name="expand">Whether to expand the image to fit the skewed result.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/></returns>
        public static Image<TColor, TPacked> Skew<TColor, TPacked>(this Image<TColor, TPacked> source, float degreesX, float degreesY, bool expand, ProgressEventHandler progressHandler = null)
            where TColor : IPackedVector<TPacked>
            where TPacked : struct
        {
            SkewProcessor<TColor, TPacked> processor = new SkewProcessor<TColor, TPacked> { AngleX = degreesX, AngleY = degreesY, Expand = expand };
            processor.OnProgress += progressHandler;

            try
            {
                return source.Process(source.Width, source.Height, source.Bounds, source.Bounds, processor);
            }
            finally
            {
                processor.OnProgress -= progressHandler;
            }
        }
    }
}
