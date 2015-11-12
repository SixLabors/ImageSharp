// <copyright file="IImageProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
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
        /// Applies the process to the specified portion of the specified <see cref="ImageBase"/>.
        /// </summary>
        /// <param name="target">Target image to apply the process to.</param>
        /// <param name="source">The source image. Cannot be null.</param>
        /// <param name="sourceRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to draw.
        /// </param>
        /// <remarks>
        /// The method keeps the source image unchanged and returns the
        /// the result of image processing filter as new image.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="target"/> is null or <paramref name="source"/> is null.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// <paramref name="sourceRectangle"/> doesnt fit the dimension of the image.
        /// </exception>
        void Apply(ImageBase target, ImageBase source, Rectangle sourceRectangle);

        /// <summary>
        /// Applies the process to the specified portion of the specified <see cref="ImageBase"/> at the specified
        /// location and with the specified size.
        /// </summary>
        /// <param name="target">Target image to apply the process to.</param>
        /// <param name="source">The source image. Cannot be null.</param>
        /// <param name="width">The target width.</param>
        /// <param name="height">The target height.</param>
        /// <param name="targetRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the location and size of the drawn image.
        /// The image is scaled to fit the rectangle.
        /// </param>
        /// <param name="sourceRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to draw.
        /// </param>
        /// <remarks>
        /// The method keeps the source image unchanged and returns the
        /// the result of image process as new image.
        /// </remarks>
        void Apply(ImageBase target, ImageBase source, int width, int height, Rectangle targetRectangle, Rectangle sourceRectangle);
    }
}
