// <copyright file="ImageFilterExtensions.cs" company="James South">
// Copyright © James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Filters
{
    using System;

    /// <summary>
    /// Exstension methods for performing filtering methods again an image.
    /// </summary>
    public static class ImageFilterExtensions
    {
        /// <summary>
        /// Applies the collection of filters to the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="filters">Any filters to apply to the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Filter(this Image source, params IImageFilter[] filters) => Filter(source, source.Bounds, filters);

        /// <summary>
        /// Applies the collection of filters to the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The rectangle defining the bounds of the pixels the image filter with adjust.</param>
        /// <param name="filters">Any filters to apply to the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Filter(this Image source, Rectangle rectangle, params IImageFilter[] filters)
        {
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (IImageFilter filter in filters)
            {
                source = PerformAction(source, true, (sourceImage, targetImage) => filter.Apply(targetImage, sourceImage, rectangle));
            }

            return source;
        }

        /// <summary>
        /// Alters the contrast component of the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="amount">The new contrast of the image. Must be between -100 and 100.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Contrast(this Image source, int amount) => source.Filter(new Contrast(amount));

        /// <summary>
        /// Performs the given action on the source image.
        /// </summary>
        /// <param name="source">The image to perform the action against.</param>
        /// <param name="clone">Whether to clone the image.</param>
        /// <param name="action">The <see cref="Action"/> to perform against the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        private static Image PerformAction(Image source, bool clone, Action<ImageBase, ImageBase> action)
        {
            Image transformedImage = clone ? new Image(source) : new Image(source.Width, source.Height);
            action(source, transformedImage);

            for (int i = 0; i < source.Frames.Count; i++)
            {
                ImageFrame frame = source.Frames[i];
                ImageFrame tranformedFrame = new ImageFrame(frame);
                action(frame, tranformedFrame);

                if (!clone)
                {
                    transformedImage.Frames.Add(tranformedFrame);
                }
                else
                {
                    transformedImage.Frames[i] = tranformedFrame;
                }
            }

            return transformedImage;
        }
    }
}
