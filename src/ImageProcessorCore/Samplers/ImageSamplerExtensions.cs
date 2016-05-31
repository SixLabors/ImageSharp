// <copyright file="ImageSamplerExtensions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Samplers
{
    /// <summary>
    /// Extensions methods for <see cref="Image"/> to apply samplers to the image.
    /// </summary>
    public static class ImageSamplerExtensions
    {
        /// <summary>
        /// Crops an image to the given width and height.
        /// </summary>
        /// <param name="source">The image to resize.</param>
        /// <param name="width">The target image width.</param>
        /// <param name="height">The target image height.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/></returns>
        public static Image Crop(this Image source, int width, int height, ProgressEventHandler progressHandler = null)
        {
            return Crop(source, width, height, source.Bounds, progressHandler);
        }

        /// <summary>
        /// Crops an image to the given width and height with the given source rectangle.
        /// <remarks>
        /// If the source rectangle is smaller than the target dimensions then the
        /// area within the source is resized performing a zoomed crop.
        /// </remarks>
        /// </summary>
        /// <param name="source">The image to crop.</param>
        /// <param name="width">The target image width.</param>
        /// <param name="height">The target image height.</param>
        /// <param name="sourceRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to draw.
        /// </param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/></returns>
        public static Image Crop(this Image source, int width, int height, Rectangle sourceRectangle, ProgressEventHandler progressHandler = null)
        {
            Guard.MustBeGreaterThan(width, 0, nameof(width));
            Guard.MustBeGreaterThan(height, 0, nameof(height));

            if (sourceRectangle.Width < width || sourceRectangle.Height < height)
            {
                // If the source rectangle is smaller than the target perform a
                // cropped zoom.
                source = source.Resize(sourceRectangle.Width, sourceRectangle.Height);
            }

            Crop processor = new Crop();
            processor.OnProgress += progressHandler;

            try
            {
                return source.Process(width, height, sourceRectangle, new Rectangle(0, 0, width, height), processor);
            }
            finally
            {
                processor.OnProgress -= progressHandler;
            }
        }

        /// <summary>
        /// Crops an image to the area of greatest entropy.
        /// </summary>
        /// <param name="source">The image to crop.</param>
        /// <param name="threshold">The threshold for entropic density.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/></returns>
        public static Image EntropyCrop(this Image source, float threshold = .5f, ProgressEventHandler progressHandler = null)
        {
            EntropyCrop processor = new EntropyCrop(threshold);
            processor.OnProgress += progressHandler;

            try
            {
                return source.Process(source.Width, source.Height, source.Bounds, Rectangle.Empty, processor);
            }
            finally
            {
                processor.OnProgress -= progressHandler;
            }
        }

        /// <summary>
        /// Evenly pads an image to fit the new dimensions.
        /// </summary>
        /// <param name="source">The source image to pad.</param>
        /// <param name="width">The new width.</param>
        /// <param name="height">The new height.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/>.</returns>
        public static Image Pad(this Image source, int width, int height, ProgressEventHandler progressHandler = null)
        {
            ResizeOptions options = new ResizeOptions
            {
                Size = new Size(width, height),
                Mode = ResizeMode.BoxPad,
                Sampler = new NearestNeighborResampler()
            };

            return Resize(source, options, progressHandler);
        }

        /// <summary>
        /// Resizes an image in accordance with the given <see cref="ResizeOptions"/>.
        /// </summary>
        /// <param name="source">The image to resize.</param>
        /// <param name="options">The resize options.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/></returns>
        /// <remarks>Passing zero for one of height or width within the resize options will automatically preserve the aspect ratio of the original image</remarks>
        public static Image Resize(this Image source, ResizeOptions options, ProgressEventHandler progressHandler = null)
        {
            // Ensure size is populated across both dimensions.
            if (options.Size.Width == 0 && options.Size.Height > 0)
            {
                options.Size = new Size(source.Width * options.Size.Height / source.Height, options.Size.Height);
            }

            if (options.Size.Height == 0 && options.Size.Width > 0)
            {
                options.Size = new Size(options.Size.Width, source.Height * options.Size.Width / source.Width);
            }

            Rectangle targetRectangle = ResizeHelper.CalculateTargetLocationAndBounds(source, options);

            return Resize(source, options.Size.Width, options.Size.Height, options.Sampler, source.Bounds, targetRectangle, options.Compand, progressHandler);
        }

        /// <summary>
        /// Resizes an image to the given width and height.
        /// </summary>
        /// <param name="source">The image to resize.</param>
        /// <param name="width">The target image width.</param>
        /// <param name="height">The target image height.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/></returns>
        /// <remarks>Passing zero for one of height or width will automatically preserve the aspect ratio of the original image</remarks>
        public static Image Resize(this Image source, int width, int height, ProgressEventHandler progressHandler = null)
        {
            return Resize(source, width, height, new BicubicResampler(), false, progressHandler);
        }

        /// <summary>
        /// Resizes an image to the given width and height.
        /// </summary>
        /// <param name="source">The image to resize.</param>
        /// <param name="width">The target image width.</param>
        /// <param name="height">The target image height.</param>
        /// <param name="compand">Whether to compress and expand the image color-space to gamma correct the image during processing.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/></returns>
        /// <remarks>Passing zero for one of height or width will automatically preserve the aspect ratio of the original image</remarks>
        public static Image Resize(this Image source, int width, int height, bool compand, ProgressEventHandler progressHandler = null)
        {
            return Resize(source, width, height, new BicubicResampler(), compand, progressHandler);
        }

        /// <summary>
        /// Resizes an image to the given width and height with the given sampler.
        /// </summary>
        /// <param name="source">The image to resize.</param>
        /// <param name="width">The target image width.</param>
        /// <param name="height">The target image height.</param>
        /// <param name="sampler">The <see cref="IResampler"/> to perform the resampling.</param>
        /// <param name="compand">Whether to compress and expand the image color-space to gamma correct the image during processing.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/></returns>
        /// <remarks>Passing zero for one of height or width will automatically preserve the aspect ratio of the original image</remarks>
        public static Image Resize(this Image source, int width, int height, IResampler sampler, bool compand, ProgressEventHandler progressHandler = null)
        {
            return Resize(source, width, height, sampler, source.Bounds, new Rectangle(0, 0, width, height), compand, progressHandler);
        }

        /// <summary>
        /// Resizes an image to the given width and height with the given sampler and
        /// source rectangle.
        /// </summary>
        /// <param name="source">The image to resize.</param>
        /// <param name="width">The target image width.</param>
        /// <param name="height">The target image height.</param>
        /// <param name="sampler">The <see cref="IResampler"/> to perform the resampling.</param>
        /// <param name="sourceRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to draw.
        /// </param>
        /// <param name="targetRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the target image object to draw to.
        /// </param>
        /// <param name="compand">Whether to compress and expand the image color-space to gamma correct the image during processing.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/></returns>
        /// <remarks>Passing zero for one of height or width will automatically preserve the aspect ratio of the original image</remarks>
        public static Image Resize(this Image source, int width, int height, IResampler sampler, Rectangle sourceRectangle, Rectangle targetRectangle, bool compand = false, ProgressEventHandler progressHandler = null)
        {
            if (width == 0 && height > 0)
            {
                width = source.Width * height / source.Height;
            }

            if (height == 0 && width > 0)
            {
                height = source.Height * width / source.Width;
            }

            Guard.MustBeGreaterThan(width, 0, nameof(width));
            Guard.MustBeGreaterThan(height, 0, nameof(height));

            Resize processor = new Resize(sampler) { Compand = compand };
            processor.OnProgress += progressHandler;

            try
            {
                return source.Process(width, height, sourceRectangle, targetRectangle, processor);
            }
            finally
            {
                processor.OnProgress -= progressHandler;
            }
        }

        /// <summary>
        /// Rotates an image by the given angle in degrees, expanding the image to fit the rotated result.
        /// </summary>
        /// <param name="source">The image to rotate.</param>
        /// <param name="degrees">The angle in degrees to perform the rotation.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/></returns>
        public static Image Rotate(this Image source, float degrees, ProgressEventHandler progressHandler = null)
        {
            return Rotate(source, degrees, Point.Empty, true, progressHandler);
        }

        /// <summary>
        /// Rotates an image by the given angle in degrees around the given center point.
        /// </summary>
        /// <param name="source">The image to rotate.</param>
        /// <param name="degrees">The angle in degrees to perform the rotation.</param>
        /// <param name="center">The center point at which to rotate the image.</param>
        /// <param name="expand">Whether to expand the image to fit the rotated result.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/></returns>
        public static Image Rotate(this Image source, float degrees, Point center, bool expand, ProgressEventHandler progressHandler = null)
        {
            Rotate processor = new Rotate { Angle = degrees, Center = center, Expand = expand };
            processor.OnProgress += progressHandler;

            try
            {
                return source.Process(source.Width, source.Height, source.Bounds, source.Bounds, processor);
            }
            finally
            {
                processor.OnProgress -= progressHandler;
            }
        }

        /// <summary>
        /// Rotates and flips an image by the given instructions.
        /// </summary>
        /// <param name="source">The image to rotate, flip, or both.</param>
        /// <param name="rotateType">The <see cref="RotateType"/> to perform the rotation.</param>
        /// <param name="flipType">The <see cref="FlipType"/> to perform the flip.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/></returns>
        public static Image RotateFlip(this Image source, RotateType rotateType, FlipType flipType, ProgressEventHandler progressHandler = null)
        {
            RotateFlip processor = new RotateFlip(rotateType, flipType);
            processor.OnProgress += progressHandler;

            try
            {
                return source.Process(source.Width, source.Height, source.Bounds, source.Bounds, processor);
            }
            finally
            {
                processor.OnProgress -= progressHandler;
            }
        }

        /// <summary>
        /// Skews an image by the given angles in degrees.
        /// </summary>
        /// <param name="source">The image to skew.</param>
        /// <param name="degreesX">The angle in degrees to perform the rotation along the x-axis.</param>
        /// <param name="degreesY">The angle in degrees to perform the rotation along the y-axis.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/></returns>
        public static Image Skew(this Image source, float degreesX, float degreesY, ProgressEventHandler progressHandler = null)
        {
            return Skew(source, degreesX, degreesY, Point.Empty, progressHandler);
        }

        /// <summary>
        /// Skews an image by the given angles in degrees around the given center point.
        /// </summary>
        /// <param name="source">The image to skew.</param>
        /// <param name="degreesX">The angle in degrees to perform the rotation along the x-axis.</param>
        /// <param name="degreesY">The angle in degrees to perform the rotation along the y-axis.</param>
        /// <param name="center">The center point at which to skew the image.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image"/></returns>
        public static Image Skew(this Image source, float degreesX, float degreesY, Point center, ProgressEventHandler progressHandler = null)
        {
            Skew processor = new Skew { AngleX = degreesX, AngleY = degreesY, Center = center };
            processor.OnProgress += progressHandler;

            try
            {
                return source.Process(source.Width, source.Height, source.Bounds, source.Bounds, processor);
            }
            finally
            {
                processor.OnProgress -= progressHandler;
            }
        }
    }
}
