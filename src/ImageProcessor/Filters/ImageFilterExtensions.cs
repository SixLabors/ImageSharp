// <copyright file="ImageFilterExtensions.cs" company="James South">
// Copyright (c) James South and contributors.
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
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Alpha(this Image source, int percent, Rectangle rectangle)
        {
            return source.Process(rectangle, new Alpha(percent));
        }

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
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Contrast(this Image source, int amount, Rectangle rectangle)
        {
            return source.Process(rectangle, new Contrast(amount));
        }

        /// <summary>
        /// Inverts the colors of the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Invert(this Image source)
        {
            return Invert(source, source.Bounds);
        }

        /// <summary>
        /// Inverts the colors of the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Invert(this Image source, Rectangle rectangle)
        {
            return source.Process(rectangle, new Invert());
        }

        /// <summary>
        /// Applies sepia toning to the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Sepia(this Image source)
        {
            return Sepia(source, source.Bounds);
        }

        /// <summary>
        /// Applies sepia toning to the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Sepia(this Image source, Rectangle rectangle)
        {
            return source.Process(rectangle, new Sepia());
        }

        /// <summary>
        /// Applies black and white toning to the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image BlackWhite(this Image source)
        {
            return BlackWhite(source, source.Bounds);
        }

        /// <summary>
        /// Applies black and white toning to the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image BlackWhite(this Image source, Rectangle rectangle)
        {
            return source.Process(rectangle, new BlackWhite());
        }

        /// <summary>
        /// Applies greyscale toning to the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="mode">The formula to apply to perform the operation.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Greyscale(this Image source, GreyscaleMode mode = GreyscaleMode.Bt709)
        {
            return Greyscale(source, source.Bounds, mode);
        }

        /// <summary>
        /// Applies greyscale toning to the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <param name="mode">The formula to apply to perform the operation.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Greyscale(this Image source, Rectangle rectangle, GreyscaleMode mode = GreyscaleMode.Bt709)
        {
            return mode == GreyscaleMode.Bt709
                ? source.Process(rectangle, new GreyscaleBt709())
                : source.Process(rectangle, new GreyscaleBt601());
        }
    }
}
