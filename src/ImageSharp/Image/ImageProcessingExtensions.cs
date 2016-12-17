// <copyright file="ImageProcessingExtensions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using Processors;

    /// <summary>
    /// Extension methods for the <see cref="Image{TColor, TPacked}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Applies the processor to the image.
        /// <remarks>This method does not resize the target image.</remarks>
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="processor">The processor to apply to the image.</param>
        /// <returns>The <see cref="Image{TColor, TPacked}"/>.</returns>
        internal static Image<TColor, TPacked> Process<TColor, TPacked>(this Image<TColor, TPacked> source, IImageFilteringProcessor<TColor, TPacked> processor)
            where TColor : struct, IPackedPixel<TPacked>
            where TPacked : struct, IEquatable<TPacked>
        {
            return Process(source, source.Bounds, processor);
        }

        /// <summary>
        /// Applies the processor to the image.
        /// <remarks>This method does not resize the target image.</remarks>
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="sourceRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to draw.
        /// </param>
        /// <param name="processor">The processors to apply to the image.</param>
        /// <returns>The <see cref="Image{TColor, TPacked}"/>.</returns>
        internal static Image<TColor, TPacked> Process<TColor, TPacked>(this Image<TColor, TPacked> source, Rectangle sourceRectangle, IImageFilteringProcessor<TColor, TPacked> processor)
            where TColor : struct, IPackedPixel<TPacked>
            where TPacked : struct, IEquatable<TPacked>
        {
            return PerformAction(source, (sourceImage) => processor.Apply(sourceImage, sourceRectangle));
        }

        /// <summary>
        /// Performs the given action on the source image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>long, float.</example></typeparam>
        /// <param name="source">The image to perform the action against.</param>
        /// <param name="action">The <see cref="Action"/> to perform against the image.</param>
        /// <returns>The <see cref="Image{TColor, TPacked}"/>.</returns>
        private static Image<TColor, TPacked> PerformAction<TColor, TPacked>(Image<TColor, TPacked> source, Action<ImageBase<TColor, TPacked>> action)
            where TColor : struct, IPackedPixel<TPacked>
            where TPacked : struct, IEquatable<TPacked>
        {
            action(source);

            foreach (ImageFrame<TColor, TPacked> sourceFrame in source.Frames)
            {
                action(sourceFrame);
            }

            return source;
        }
    }
}
