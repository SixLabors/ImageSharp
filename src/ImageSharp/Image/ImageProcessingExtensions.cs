// <copyright file="ImageProcessingExtensions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using Processing;

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
        public static Image<TColor> Apply<TColor>(this Image<TColor> source, IImageProcessor<TColor> processor)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return Apply(source, source.Bounds, processor);
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
        public static Image<TColor> Apply<TColor>(this Image<TColor> source, Rectangle sourceRectangle, IImageProcessor<TColor> processor)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            processor.Apply(source, sourceRectangle);

            foreach (ImageFrame<TColor> sourceFrame in source.Frames)
            {
                processor.Apply(sourceFrame, sourceRectangle);
            }

            return source;
        }
    }
}