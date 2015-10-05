// <copyright file="IImageProcessor.cs" company="James South">
// Copyright © James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor
{
    /// <summary>
    /// Encapsulates methods to alter the pixels of an image.
    /// </summary>
    public interface IImageProcessor
    {
        /// <summary>
        /// Apply a process to an image to alter the pixels at the area of the specified rectangle.
        /// </summary>
        /// <param name="target">Target image to apply the process to.</param>
        /// <param name="source">The source image. Cannot be null.</param>
        /// <param name="rectangle">
        /// The rectangle, which defines the area of the image where the process should be applied to.
        /// </param>
        /// <remarks>
        /// The method keeps the source image unchanged and returns the
        /// the result of image processing filter as new image.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="target"/> is null or <paramref name="source"/> is null.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// <paramref name="rectangle"/> doesnt fit the dimension of the image.
        /// </exception>
        void Apply(ImageBase target, ImageBase source, Rectangle rectangle);
    }
}
