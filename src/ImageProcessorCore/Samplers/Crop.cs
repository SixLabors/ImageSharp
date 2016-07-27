// <copyright file="Crop.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>-------------------------------------------------------------------------------------------------------------------

namespace ImageProcessorCore
{
    using Processors;

    /// <summary>
    /// Extension methods for the <see cref="Image"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Crops an image to the given width and height.
        /// </summary>
        /// <typeparam name="T">The pixel format.</typeparam>
        /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
        /// <param name="source">The image to resize.</param>
        /// <param name="width">The target image width.</param>
        /// <param name="height">The target image height.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image{T,TP}"/></returns>
        public static Image<T, TP> Crop<T, TP>(this Image<T, TP> source, int width, int height, ProgressEventHandler progressHandler = null)
            where T : IPackedVector<TP>
            where TP : struct
        {
            return Crop(source, width, height, source.Bounds, progressHandler);
        }

        /// <summary>
        /// Crops an image to the given width and height with the given source rectangle.
        /// <remarks>
        /// If the source rectangle is smaller than the target dimensions then the
        /// area within the source is resized performing a zoomed crop.
        /// </remarks>
        /// </summary>
        /// <typeparam name="T">The pixel format.</typeparam>
        /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
        /// <param name="source">The image to crop.</param>
        /// <param name="width">The target image width.</param>
        /// <param name="height">The target image height.</param>
        /// <param name="sourceRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to draw.
        /// </param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/></returns>
        public static Image<T, TP> Crop<T, TP>(this Image<T, TP> source, int width, int height, Rectangle sourceRectangle, ProgressEventHandler progressHandler = null)
            where T : IPackedVector<TP>
            where TP : struct
        {
            Guard.MustBeGreaterThan(width, 0, nameof(width));
            Guard.MustBeGreaterThan(height, 0, nameof(height));

            if (sourceRectangle.Width < width || sourceRectangle.Height < height)
            {
                // If the source rectangle is smaller than the target perform a
                // cropped zoom.
                source = source.Resize(sourceRectangle.Width, sourceRectangle.Height);
            }

            CropProcessor<T, TP> processor = new CropProcessor<T, TP>();
            processor.OnProgress += progressHandler;

            try
            {
                return source.Process(width, height, sourceRectangle, new Rectangle(0, 0, width, height), processor);
            }
            finally
            {
                processor.OnProgress -= progressHandler;
            }
        }
    }
}
