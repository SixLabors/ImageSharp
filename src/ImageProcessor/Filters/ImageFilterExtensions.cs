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
        /// Combines the given image together with the current one by blending their pixels.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="image">The image to blend with the currently processing image.</param>
        /// <param name="percent">The opacity of the image image to blend. Must be between 0 and 100.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Blend(this Image source, ImageBase image, int percent = 50)
        {
            return source.Process(source.Bounds, new Blend(image, percent));
        }

        /// <summary>
        /// Combines the given image together with the current one by blending their pixels.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="image">The image to blend with the currently processing image.</param>
        /// <param name="percent">The opacity of the image image to blend. Must be between 0 and 100.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Blend(this Image source, ImageBase image, int percent, Rectangle rectangle)
        {
            return source.Process(rectangle, new Blend(image, percent));
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
        /// Alters the brightness component of the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="amount">The new brightness of the image. Must be between -100 and 100.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Brightness(this Image source, int amount)
        {
            return Brightness(source, amount, source.Bounds);
        }

        /// <summary>
        /// Alters the brightness component of the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="amount">The new brightness of the image. Must be between -100 and 100.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Brightness(this Image source, int amount, Rectangle rectangle)
        {
            return source.Process(rectangle, new Brightness(amount));
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
        /// Detects any edges within the image. Uses the <see cref="Sobel"/> filter
        /// operating in greyscale mode.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image DetectEdges(this Image source)
        {
            return DetectEdges(source, source.Bounds, new Sobel { Greyscale = true });
        }

        /// <summary>
        /// Detects any edges within the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="filter">The filter for detecting edges.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image DetectEdges(this Image source, IEdgeDetectorFilter filter)
        {
            return DetectEdges(source, source.Bounds, filter);
        }

        /// <summary>
        /// Detects any edges within the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <param name="filter">The filter for detecting edges.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image DetectEdges(this Image source, Rectangle rectangle, IEdgeDetectorFilter filter)
        {
            return source.Process(rectangle, filter);
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
        /// Alters the colors of the image recreating an old Kodachrome camera effect.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Kodachrome(this Image source)
        {
            return Kodachrome(source, source.Bounds);
        }

        /// <summary>
        /// Alters the colors of the image recreating an old Kodachrome camera effect.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Kodachrome(this Image source, Rectangle rectangle)
        {
            return source.Process(rectangle, new Kodachrome());
        }

        /// <summary>
        /// Alters the colors of the image recreating an old Lomograph camera effect.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Lomograph(this Image source)
        {
            return Lomograph(source, source.Bounds);
        }

        /// <summary>
        /// Alters the colors of the image recreating an old Lomograph camera effect.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Lomograph(this Image source, Rectangle rectangle)
        {
            return source.Process(rectangle, new Lomograph());
        }

        /// <summary>
        /// Alters the colors of the image recreating an old Polaroid camera effect.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Polaroid(this Image source)
        {
            return Polaroid(source, source.Bounds);
        }

        /// <summary>
        /// Alters the colors of the image recreating an old Polaroid camera effect.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Polaroid(this Image source, Rectangle rectangle)
        {
            return source.Process(rectangle, new Polaroid());
        }

        /// <summary>
        /// Alters the saturation component of the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="amount">The new saturation of the image. Must be between -100 and 100.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Saturation(this Image source, int amount)
        {
            return Saturation(source, amount, source.Bounds);
        }

        /// <summary>
        /// Alters the saturation component of the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="amount">The new saturation of the image. Must be between -100 and 100.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Saturation(this Image source, int amount, Rectangle rectangle)
        {
            return source.Process(rectangle, new Saturation(amount));
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
    }
}
