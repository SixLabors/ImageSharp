// <copyright file="ImageSampleExtensions.cs" company="James South">
// Copyright © James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Samplers
{
    /// <summary>
    /// Extensions methods for <see cref="Image"/> to apply samplers to the image.
    /// </summary>
    public static class ImageSampleExtensions
    {
        /// <summary>
        /// Resizes an image to the given width and height.
        /// </summary>
        /// <param name="source">The image to resize.</param>
        /// <param name="width">The target image width.</param>
        /// <param name="height">The target image height.</param>
        /// <returns>The <see cref="Image"/></returns>
        public static Image Resize(this Image source, int width, int height)
        {
            return Resize(source, width, height, new RobidouxResampler());
        }

        /// <summary>
        /// Resizes an image to the given width and height with the given sampler.
        /// </summary>
        /// <param name="source">The image to resize.</param>
        /// <param name="width">The target image width.</param>
        /// <param name="height">The target image height.</param>
        /// <param name="sampler">The <see cref="IResampler"/> to perform the resampling.</param>
        /// <returns>The <see cref="Image"/></returns>
        public static Image Resize(this Image source, int width, int height, IResampler sampler)
        {
            return Resize(source, width, height, sampler, source.Bounds, new Rectangle(0, 0, width, height));
        }

        /// <summary>
        /// Resizes an image to the given width and height with the given sampler.
        /// </summary>
        /// <param name="source">The image to resize.</param>
        /// <param name="width">The target image width.</param>
        /// <param name="height">The target image height.</param>
        /// <param name="sampler">The <see cref="IResampler"/> to perform the resampling.</param>
        /// <param name="sourceRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to draw.
        /// </param>
        /// <param name="targetRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the location and size of the drawn image.
        /// The image is scaled to fit the rectangle.
        /// </param>
        /// <returns>The <see cref="Image"/></returns>
        public static Image Resize(this Image source, int width, int height, IResampler sampler, Rectangle sourceRectangle, Rectangle targetRectangle)
        {
            return source.Process(width, height, sourceRectangle, targetRectangle, new Resize(sampler));
        }
    }
}
