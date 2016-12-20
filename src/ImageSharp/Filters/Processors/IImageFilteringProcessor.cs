// <copyright file="IImageFilteringProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processors
{
    using System;

    /// <summary>
    /// Encapsulates methods to alter the pixels of an image. The processor operates on the original source pixels.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    public interface IImageFilteringProcessor<TColor> : IImageProcessor
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        /// <summary>
        /// Applies the process to the specified portion of the specified <see cref="ImageBase{TColor}"/>.
        /// </summary>
        /// <param name="source">The source image. Cannot be null.</param>
        /// <param name="sourceRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to draw.
        /// </param>
        /// <remarks>
        /// The method keeps the source image unchanged and returns the
        /// the result of image processing filter as new image.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="source"/> is null.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// <paramref name="sourceRectangle"/> doesnt fit the dimension of the image.
        /// </exception>
        void Apply(ImageBase<TColor> source, Rectangle sourceRectangle);
    }
}
