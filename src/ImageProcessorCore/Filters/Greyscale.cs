// <copyright file="Greyscale.cs" company="James Jackson-South">
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
        /// Applies greyscale toning to the image.
        /// </summary>
        /// <typeparam name="T">The pixel format.</typeparam>
        /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="mode">The formula to apply to perform the operation.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image{T,TP}"/>.</returns>
        public static Image<T, TP> Greyscale<T, TP>(this Image<T, TP> source, GreyscaleMode mode = GreyscaleMode.Bt709, ProgressEventHandler progressHandler = null)
            where T : IPackedVector<TP>
            where TP : struct
        {
            return Greyscale(source, source.Bounds, mode, progressHandler);
        }

        /// <summary>
        /// Applies greyscale toning to the image.
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
        public static Image<T, TP> Greyscale<T, TP>(this Image<T, TP> source, Rectangle rectangle, GreyscaleMode mode = GreyscaleMode.Bt709, ProgressEventHandler progressHandler = null)
            where T : IPackedVector<TP>
            where TP : struct
        {
            IImageProcessor<T, TP> processor = mode == GreyscaleMode.Bt709
                ? (IImageProcessor<T, TP>)new GreyscaleBt709Processor<T, TP>()
                : new GreyscaleBt601Processor<T, TP>();

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
