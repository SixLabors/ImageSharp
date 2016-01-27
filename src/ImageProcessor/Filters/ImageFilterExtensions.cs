// <copyright file="ImageFilterExtensions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
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
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Alpha(this Image source, int percent, ProgressEventHandler progressHandler = null)
        {
            return Alpha(source, percent, source.Bounds, progressHandler);
        }

        /// <summary>
        /// Alters the alpha component of the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="percent">The new opacity of the image. Must be between 0 and 100.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Alpha(this Image source, int percent, Rectangle rectangle, ProgressEventHandler progressHandler = null)
        {
            Alpha processor = new Alpha(percent);
            processor.OnProgress += progressHandler;

            try
            {
                return source.Process(rectangle, processor);
            }
            finally
            {
                processor.OnProgress -= progressHandler;
            }
        }

        /// <summary>
        /// Combines the given image together with the current one by blending their pixels.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color to set as the background.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image BackgroundColor(this Image source, Color color, ProgressEventHandler progressHandler = null)
        {
            BackgroundColor processor = new BackgroundColor(color);
            processor.OnProgress += progressHandler;

            try
            {
                return source.Process(source.Bounds, processor);
            }
            finally
            {
                processor.OnProgress -= progressHandler;
            }
        }

        /// <summary>
        /// Combines the given image together with the current one by blending their pixels.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="image">The image to blend with the currently processing image.</param>
        /// <param name="percent">The opacity of the image image to blend. Must be between 0 and 100.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Blend(this Image source, ImageBase image, int percent = 50, ProgressEventHandler progressHandler = null)
        {
            return Blend(source, image, percent, source.Bounds, progressHandler);
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
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Blend(this Image source, ImageBase image, int percent, Rectangle rectangle, ProgressEventHandler progressHandler = null)
        {
            Blend processor = new Blend(image, percent);
            processor.OnProgress += progressHandler;

            try
            {
                return source.Process(rectangle, processor);
            }
            finally
            {
                processor.OnProgress -= progressHandler;
            }
        }

        /// <summary>
        /// Applies black and white toning to the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image BlackWhite(this Image source, ProgressEventHandler progressHandler = null)
        {
            return BlackWhite(source, source.Bounds, progressHandler);
        }

        /// <summary>
        /// Applies black and white toning to the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image BlackWhite(this Image source, Rectangle rectangle, ProgressEventHandler progressHandler = null)
        {
            BlackWhite processor = new BlackWhite();
            processor.OnProgress += progressHandler;

            try
            {
                return source.Process(rectangle, processor);
            }
            finally
            {
                processor.OnProgress -= progressHandler;
            }
        }

        /// <summary>
        /// Applies a box blur to the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="radius">The 'radius' value representing the size of the area to sample.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image BoxBlur(this Image source, int radius = 7, ProgressEventHandler progressHandler = null)
        {
            return BoxBlur(source, radius, source.Bounds, progressHandler);
        }

        /// <summary>
        /// Applies a box blur to the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="radius">The 'radius' value representing the size of the area to sample.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image BoxBlur(this Image source, int radius, Rectangle rectangle, ProgressEventHandler progressHandler = null)
        {
            BoxBlur processor = new BoxBlur(radius);
            processor.OnProgress += progressHandler;

            try
            {
                return source.Process(rectangle, processor);
            }
            finally
            {
                processor.OnProgress -= progressHandler;
            }
        }

        /// <summary>
        /// Alters the brightness component of the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="amount">The new brightness of the image. Must be between -100 and 100.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Brightness(this Image source, int amount, ProgressEventHandler progressHandler = null)
        {
            return Brightness(source, amount, source.Bounds, progressHandler);
        }

        /// <summary>
        /// Alters the brightness component of the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="amount">The new brightness of the image. Must be between -100 and 100.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Brightness(this Image source, int amount, Rectangle rectangle, ProgressEventHandler progressHandler = null)
        {
            Brightness processor = new Brightness(amount);
            processor.OnProgress += progressHandler;

            try
            {
                return source.Process(rectangle, processor);
            }
            finally
            {
                processor.OnProgress -= progressHandler;
            }
        }

        /// <summary>
        /// Alters the contrast component of the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="amount">The new contrast of the image. Must be between -100 and 100.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Contrast(this Image source, int amount, ProgressEventHandler progressHandler = null)
        {
            return Contrast(source, amount, source.Bounds, progressHandler);
        }

        /// <summary>
        /// Alters the contrast component of the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="amount">The new contrast of the image. Must be between -100 and 100.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Contrast(this Image source, int amount, Rectangle rectangle, ProgressEventHandler progressHandler = null)
        {
            Contrast processor = new Contrast(amount);
            processor.OnProgress += progressHandler;

            try
            {
                return source.Process(rectangle, processor);
            }
            finally
            {
                processor.OnProgress -= progressHandler;
            }
        }

        /// <summary>
        /// Detects any edges within the image. Uses the <see cref="Sobel"/> filter
        /// operating in greyscale mode.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image DetectEdges(this Image source, ProgressEventHandler progressHandler = null)
        {
            return DetectEdges(source, source.Bounds, new Sobel { Greyscale = true }, progressHandler);
        }

        /// <summary>
        /// Detects any edges within the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="filter">The filter for detecting edges.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image DetectEdges(this Image source, IEdgeDetectorFilter filter, ProgressEventHandler progressHandler = null)
        {
            return DetectEdges(source, source.Bounds, filter, progressHandler);
        }

        /// <summary>
        /// Detects any edges within the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <param name="filter">The filter for detecting edges.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image DetectEdges(this Image source, Rectangle rectangle, IEdgeDetectorFilter filter, ProgressEventHandler progressHandler = null)
        {
            filter.OnProgress += progressHandler;

            try
            {
                return source.Process(rectangle, filter);
            }
            finally
            {
                filter.OnProgress -= progressHandler;
            }
        }

        /// <summary>
        /// Applies greyscale toning to the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="mode">The formula to apply to perform the operation.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Greyscale(this Image source, GreyscaleMode mode = GreyscaleMode.Bt709, ProgressEventHandler progressHandler = null)
        {
            return Greyscale(source, source.Bounds, mode, progressHandler);
        }

        /// <summary>
        /// Applies greyscale toning to the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <param name="mode">The formula to apply to perform the operation.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Greyscale(this Image source, Rectangle rectangle, GreyscaleMode mode = GreyscaleMode.Bt709, ProgressEventHandler progressHandler = null)
        {
            IImageProcessor processor = mode == GreyscaleMode.Bt709
                ? (IImageProcessor)new GreyscaleBt709()
                : new GreyscaleBt601();

            processor.OnProgress += progressHandler;

            try
            {
                return source.Process(rectangle, processor);
            }
            finally
            {
                processor.OnProgress -= progressHandler;
            }
        }

        /// <summary>
        /// Applies a Guassian blur to the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="sigma">The 'sigma' value representing the weight of the blur.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image GuassianBlur(this Image source, float sigma = 3f, ProgressEventHandler progressHandler = null)
        {
            return GuassianBlur(source, sigma, source.Bounds, progressHandler);
        }

        /// <summary>
        /// Applies a Guassian blur to the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="sigma">The 'sigma' value representing the weight of the blur.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image GuassianBlur(this Image source, float sigma, Rectangle rectangle, ProgressEventHandler progressHandler = null)
        {
            GuassianBlur processor = new GuassianBlur(sigma);
            processor.OnProgress += progressHandler;

            try
            {
                return source.Process(rectangle, processor);
            }
            finally
            {
                processor.OnProgress -= progressHandler;
            }
        }

        /// <summary>
        /// Applies a Guassian sharpening filter to the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="sigma">The 'sigma' value representing the weight of the blur.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image GuassianSharpen(this Image source, float sigma = 3f, ProgressEventHandler progressHandler = null)
        {
            return GuassianSharpen(source, sigma, source.Bounds, progressHandler);
        }

        /// <summary>
        /// Applies a Guassian sharpening filter to the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="sigma">The 'sigma' value representing the weight of the blur.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image GuassianSharpen(this Image source, float sigma, Rectangle rectangle, ProgressEventHandler progressHandler = null)
        {
            GuassianSharpen processor = new GuassianSharpen(sigma);
            processor.OnProgress += progressHandler;

            try
            {
                return source.Process(rectangle, processor);
            }
            finally
            {
                processor.OnProgress -= progressHandler;
            }
        }

        /// <summary>
        /// Alters the hue component of the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="degrees">The angle in degrees to adjust the image.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Hue(this Image source, float degrees, ProgressEventHandler progressHandler = null)
        {
            return Hue(source, degrees, source.Bounds, progressHandler);
        }

        /// <summary>
        /// Alters the hue component of the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="degrees">The angle in degrees to adjust the image.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Hue(this Image source, float degrees, Rectangle rectangle, ProgressEventHandler progressHandler = null)
        {
            Hue processor = new Hue(degrees);
            processor.OnProgress += progressHandler;

            try
            {
                return source.Process(rectangle, processor);
            }
            finally
            {
                processor.OnProgress -= progressHandler;
            }
        }

        /// <summary>
        /// Inverts the colors of the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Invert(this Image source, ProgressEventHandler progressHandler = null)
        {
            return Invert(source, source.Bounds, progressHandler);
        }

        /// <summary>
        /// Inverts the colors of the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Invert(this Image source, Rectangle rectangle, ProgressEventHandler progressHandler = null)
        {
            Invert processor = new Invert();
            processor.OnProgress += progressHandler;

            try
            {
                return source.Process(rectangle, processor);
            }
            finally
            {
                processor.OnProgress -= progressHandler;
            }
        }

        /// <summary>
        /// Alters the colors of the image recreating an old Kodachrome camera effect.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Kodachrome(this Image source, ProgressEventHandler progressHandler = null)
        {
            return Kodachrome(source, source.Bounds, progressHandler);
        }

        /// <summary>
        /// Alters the colors of the image recreating an old Kodachrome camera effect.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Kodachrome(this Image source, Rectangle rectangle, ProgressEventHandler progressHandler = null)
        {
            Kodachrome processor = new Kodachrome();
            processor.OnProgress += progressHandler;

            try
            {
                return source.Process(rectangle, processor);
            }
            finally
            {
                processor.OnProgress -= progressHandler;
            }
        }

        /// <summary>
        /// Alters the colors of the image recreating an old Lomograph camera effect.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Lomograph(this Image source, ProgressEventHandler progressHandler = null)
        {
            return Lomograph(source, source.Bounds, progressHandler);
        }

        /// <summary>
        /// Alters the colors of the image recreating an old Lomograph camera effect.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Lomograph(this Image source, Rectangle rectangle, ProgressEventHandler progressHandler = null)
        {
            Lomograph processor = new Lomograph();
            processor.OnProgress += progressHandler;

            try
            {
                return source.Process(rectangle, processor);
            }
            finally
            {
                processor.OnProgress -= progressHandler;
            }
        }

        /// <summary>
        /// Alters the colors of the image recreating an old Polaroid camera effect.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Polaroid(this Image source, ProgressEventHandler progressHandler = null)
        {
            return Polaroid(source, source.Bounds, progressHandler);
        }

        /// <summary>
        /// Alters the colors of the image recreating an old Polaroid camera effect.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Polaroid(this Image source, Rectangle rectangle, ProgressEventHandler progressHandler = null)
        {
            Polaroid processor = new Polaroid();
            processor.OnProgress += progressHandler;

            try
            {
                return source.Process(rectangle, processor);
            }
            finally
            {
                processor.OnProgress -= progressHandler;
            }
        }

        /// <summary>
        /// Pixelates and image with the given pixel size.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="size">The size of the pixels.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Pixelate(this Image source, int size = 4, ProgressEventHandler progressHandler = null)
        {
            return Pixelate(source, size, source.Bounds, progressHandler);
        }

        /// <summary>
        /// Pixelates and image with the given pixel size.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="size">The size of the pixels.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Pixelate(this Image source, int size, Rectangle rectangle, ProgressEventHandler progressHandler = null)
        {
            Pixelate processor = new Pixelate(size);
            processor.OnProgress += progressHandler;

            try
            {
                return source.Process(rectangle, processor);
            }
            finally
            {
                processor.OnProgress -= progressHandler;
            }
        }

        /// <summary>
        /// Alters the saturation component of the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="amount">The new saturation of the image. Must be between -100 and 100.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Saturation(this Image source, int amount, ProgressEventHandler progressHandler = null)
        {
            return Saturation(source, amount, source.Bounds, progressHandler);
        }

        /// <summary>
        /// Alters the saturation component of the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="amount">The new saturation of the image. Must be between -100 and 100.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Saturation(this Image source, int amount, Rectangle rectangle, ProgressEventHandler progressHandler = null)
        {
            Saturation processor = new Saturation(amount);
            processor.OnProgress += progressHandler;

            try
            {
                return source.Process(rectangle, processor);
            }
            finally
            {
                processor.OnProgress -= progressHandler;
            }
        }

        /// <summary>
        /// Applies sepia toning to the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Sepia(this Image source, ProgressEventHandler progressHandler = null)
        {
            return Sepia(source, source.Bounds, progressHandler);
        }

        /// <summary>
        /// Applies sepia toning to the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Sepia(this Image source, Rectangle rectangle, ProgressEventHandler progressHandler = null)
        {
            Sepia processor = new Sepia();
            processor.OnProgress += progressHandler;

            try
            {
                return source.Process(rectangle, processor);
            }
            finally
            {
                processor.OnProgress -= progressHandler;
            }
        }
    }
}
