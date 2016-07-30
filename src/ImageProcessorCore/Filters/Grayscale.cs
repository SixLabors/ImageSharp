// <copyright file="Grayscale.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    using Processors;

    /// <summary>
    /// Extension methods for the <see cref="Image{T,TP}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Applies Grayscale toning to the image.
        /// </summary>
        /// <typeparam name="T">The pixel format.</typeparam>
        /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="mode">The formula to apply to perform the operation.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image{T,TP}"/>.</returns>
        public static Image<T, TP> Grayscale<T, TP>(this Image<T, TP> source, GrayscaleMode mode = GrayscaleMode.Bt709, ProgressEventHandler progressHandler = null)
            where T : IPackedVector<TP>
            where TP : struct
        {
            return Grayscale(source, source.Bounds, mode, progressHandler);
        }

        /// <summary>
        /// Applies Grayscale toning to the image.
        /// </summary>
        /// <typeparam name="T">The pixel format.</typeparam>
        /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <param name="mode">The formula to apply to perform the operation.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image{T,TP}"/>.</returns>
        public static Image<T, TP> Grayscale<T, TP>(this Image<T, TP> source, Rectangle rectangle, GrayscaleMode mode = GrayscaleMode.Bt709, ProgressEventHandler progressHandler = null)
            where T : IPackedVector<TP>
            where TP : struct
        {
            IImageProcessor<T, TP> processor = mode == GrayscaleMode.Bt709
                ? (IImageProcessor<T, TP>)new GrayscaleBt709Processor<T, TP>()
                : new GrayscaleBt601Processor<T, TP>();

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
