// <copyright file="BinaryThreshold.cs" company="James Jackson-South">
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
        /// Applies binerization to the image splitting the pixels at the given threshold.
        /// </summary>
        /// <typeparam name="T">The pixel format.</typeparam>
        /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="threshold">The threshold to apply binerization of the image. Must be between 0 and 1.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image{T,TP}"/>.</returns>
        public static Image<T, TP> BinaryThreshold<T, TP>(this Image<T, TP> source, float threshold, ProgressEventHandler progressHandler = null)
            where T : IPackedVector<TP>
            where TP : struct
        {
            return BinaryThreshold(source, threshold, source.Bounds, progressHandler);
        }

        /// <summary>
        /// Applies binerization to the image splitting the pixels at the given threshold.
        /// </summary>
        /// <typeparam name="T">The pixel format.</typeparam>
        /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="threshold">The threshold to apply binerization of the image. Must be between 0 and 1.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image{T,TP}"/>.</returns>
        public static Image<T, TP> BinaryThreshold<T, TP>(this Image<T, TP> source, float threshold, Rectangle rectangle, ProgressEventHandler progressHandler = null)
            where T : IPackedVector<TP>
            where TP : struct
        {
            BinaryThresholdProcessor<T, TP> processor = new BinaryThresholdProcessor<T, TP>(threshold);
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
