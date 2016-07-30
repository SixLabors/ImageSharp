// <copyright file="Pixelate.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    using Processors;
    using System;

    /// <summary>
    /// Extension methods for the <see cref="Image{T,TP}"/> type.
    /// </summary>
    /// <typeparam name="T">The pixel format.</typeparam>
    /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Pixelates and image with the given pixel size.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="size">The size of the pixels.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image{T,TP}"/>.</returns>
        public static Image<T, TP> Pixelate<T, TP>(this Image<T, TP> source, int size = 4, ProgressEventHandler progressHandler = null)
            where T : IPackedVector<TP>
            where TP : struct
        {
            return Pixelate(source, size, source.Bounds, progressHandler);
        }

        /// <summary>
        /// Pixelates and image with the given pixel size.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="size">The size of the pixels.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image{T,TP}"/>.</returns>
        public static Image<T, TP> Pixelate<T, TP>(this Image<T, TP> source, int size, Rectangle rectangle, ProgressEventHandler progressHandler = null)
            where T : IPackedVector<TP>
            where TP : struct
        {
            if (size <= 0 || size > source.Height || size > source.Width)
            {
                throw new ArgumentOutOfRangeException(nameof(size));
            }

            PixelateProcessor<T, TP> processor = new PixelateProcessor<T, TP>(size);
            processor.OnProgress += progressHandler;

            try
            {
                return source.Process(rectangle, processor);
            }
            finally
            {
                processor.OnProgress -= progressHandler;
            }
        }
    }
}
