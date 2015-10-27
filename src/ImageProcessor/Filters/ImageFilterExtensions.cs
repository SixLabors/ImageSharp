// <copyright file="ImageFilterExtensions.cs" company="James South">
// Copyright © James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Filters
{
    /// <summary>
    /// Extensions methods for <see cref="Image"/> to apply filters to the image.
    /// </summary>
    public static class ImageFilterExtensions
    {
        /// <summary>
        /// Alters the contrast component of the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="amount">The new contrast of the image. Must be between -100 and 100.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Contrast(this Image source, int amount)
        {
            return Contrast(source, amount, source.Bounds);
        }

        /// <summary>
        /// Alters the contrast component of the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="amount">The new contrast of the image. Must be between -100 and 100.</param>
        /// <param name="sourceRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to draw.
        /// </param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Contrast(this Image source, int amount, Rectangle sourceRectangle)
        {
            return source.Process(sourceRectangle, new Contrast(amount));
        }

        /// <summary>
        /// Alters the alpha component of the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="percent">The new opacity of the image. Must be between 0 and 100.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Alpha(this Image source, int percent)
        {
            return Alpha(source, percent, source.Bounds);
        }

        /// <summary>
        /// Alters the alpha component of the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="percent">The new opacity of the image. Must be between 0 and 100.</param>
        /// <param name="sourceRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to draw.
        /// </param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Alpha(this Image source, int percent, Rectangle sourceRectangle)
        {
            return source.Process(sourceRectangle, new Alpha(percent));
        }

        /// <summary>
        /// Alters the alpha component of the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Invert(this Image source)
        {
            return Invert(source, source.Bounds);
        }

        /// <summary>
        /// Alters the alpha component of the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="sourceRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to draw.
        /// </param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Invert(this Image source, Rectangle sourceRectangle)
        {
            return source.Process(sourceRectangle, new Invert());
        }
    }
}
