// <copyright file="Lomograph.cs" company="James Jackson-South">
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
        /// Alters the colors of the image recreating an old Lomograph camera effect.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image{TColor, TPacked}"/>.</returns>
        public static Image<TColor, TPacked> Lomograph<TColor, TPacked>(this Image<TColor, TPacked> source, ProgressEventHandler progressHandler = null)
            where TColor : IPackedVector<TPacked>
            where TPacked : struct
        {
            return Lomograph(source, source.Bounds, progressHandler);
        }

        /// <summary>
        /// Alters the colors of the image recreating an old Lomograph camera effect.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image{TColor, TPacked}"/>.</returns>
        public static Image<TColor, TPacked> Lomograph<TColor, TPacked>(this Image<TColor, TPacked> source, Rectangle rectangle, ProgressEventHandler progressHandler = null)
            where TColor : IPackedVector<TPacked>
            where TPacked : struct
        {
            LomographProcessor<TColor, TPacked> processor = new LomographProcessor<TColor, TPacked>();
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
