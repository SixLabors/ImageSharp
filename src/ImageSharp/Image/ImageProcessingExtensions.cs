// <copyright file="ImageProcessingExtensions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using Processors;

    /// <summary>
    /// Extension methods for the <see cref="Image{TColor}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Applies the processor to the image.
        /// <remarks>This method does not resize the target image.</remarks>
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="processor">The processor to apply to the image.</param>
        /// <returns>The <see cref="Image{TColor}"/>.</returns>
        internal static Image<TColor> Process<TColor>(this Image<TColor> source, IImageProcessor<TColor> processor)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return Process(source, source.Bounds, processor);
        }

        /// <summary>
        /// Applies the processor to the image.
        /// <remarks>This method does not resize the target image.</remarks>
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="sourceRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to draw.
        /// </param>
        /// <param name="processor">The processors to apply to the image.</param>
        /// <returns>The <see cref="Image{TColor}"/>.</returns>
        internal static Image<TColor> Process<TColor>(this Image<TColor> source, Rectangle sourceRectangle, IImageProcessor<TColor> processor)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return PerformAction(source, (sourceImage) => processor.Apply(sourceImage, sourceRectangle));
        }

        /// <summary>
        /// Performs the given action on the source image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="source">The image to perform the action against.</param>
        /// <param name="action">The <see cref="Action"/> to perform against the image.</param>
        /// <returns>The <see cref="Image{TColor}"/>.</returns>
        private static Image<TColor> PerformAction<TColor>(Image<TColor> source, Action<ImageBase<TColor>> action)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            action(source);

            foreach (ImageFrame<TColor> sourceFrame in source.Frames)
            {
                action(sourceFrame);
            }

            return source;
        }
    }
}
