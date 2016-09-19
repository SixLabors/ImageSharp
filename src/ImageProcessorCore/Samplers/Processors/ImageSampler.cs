// <copyright file="ImageSampler.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    using System;

    /// <summary>
    /// Encapsulates methods to alter the pixels of an image. The processor creates a copy of the original image to operate on.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public abstract class ImageSampler<TColor, TPacked> : ImageProcessor<TColor, TPacked>, IImageSampler<TColor, TPacked>
        where TColor : IPackedVector<TPacked>
        where TPacked : struct
    {
        /// <inheritdoc/>
        public void Apply(ImageBase<TColor, TPacked> target, ImageBase<TColor, TPacked> source, Rectangle sourceRectangle)
        {
            try
            {
                this.OnApply(target, source, target.Bounds, sourceRectangle);

                this.Apply(target, source, target.Bounds, sourceRectangle, sourceRectangle.Y, sourceRectangle.Bottom);

                this.AfterApply(target, source, target.Bounds, sourceRectangle);
            }
            catch (Exception ex)
            {
                throw new ImageProcessingException($"An error occured when processing the image using {this.GetType().Name}. See the inner exception for more detail.", ex);
            }
        }

        /// <inheritdoc/>
        public void Apply(ImageBase<TColor, TPacked> target, ImageBase<TColor, TPacked> source, int width, int height, Rectangle targetRectangle = default(Rectangle), Rectangle sourceRectangle = default(Rectangle))
        {
            try
            {
                TColor[] pixels = new TColor[width * height];
                target.SetPixels(width, height, pixels);

                // Ensure we always have bounds.
                if (sourceRectangle == Rectangle.Empty)
                {
                    sourceRectangle = source.Bounds;
                }

                if (targetRectangle == Rectangle.Empty)
                {
                    targetRectangle = target.Bounds;
                }

                this.OnApply(target, source, targetRectangle, sourceRectangle);

                this.Apply(target, source, targetRectangle, sourceRectangle, targetRectangle.Y, targetRectangle.Bottom);

                this.AfterApply(target, source, target.Bounds, sourceRectangle);
            }
            catch (Exception ex)
            {
                throw new ImageProcessingException($"An error occured when processing the image using {this.GetType().Name}. See the inner exception for more detail.", ex);
            }
        }

        /// <summary>
        /// Applies the process to the specified portion of the specified <see cref="ImageBase{TColor, TPacked}"/> at the specified location
        /// and with the specified size.
        /// </summary>
        /// <param name="target">Target image to apply the process to.</param>
        /// <param name="source">The source image. Cannot be null.</param>
        /// <param name="targetRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the location and size of the drawn image.
        /// The image is scaled to fit the rectangle.
        /// </param>
        /// <param name="sourceRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to draw.
        /// </param>
        /// <param name="startY">The index of the row within the source image to start processing.</param>
        /// <param name="endY">The index of the row within the source image to end processing.</param>
        /// <remarks>
        /// The method keeps the source image unchanged and returns the the result of image process as new image.
        /// </remarks>
        public abstract void Apply(ImageBase<TColor, TPacked> target, ImageBase<TColor, TPacked> source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY);

        /// <summary>
        /// This method is called before the process is applied to prepare the processor.
        /// </summary>
        /// <param name="target">Target image to apply the process to.</param>
        /// <param name="source">The source image. Cannot be null.</param>
        /// <param name="targetRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the location and size of the drawn image.
        /// The image is scaled to fit the rectangle.
        /// </param>
        /// <param name="sourceRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to draw.
        /// </param>
        protected virtual void OnApply(ImageBase<TColor, TPacked> target, ImageBase<TColor, TPacked> source, Rectangle targetRectangle, Rectangle sourceRectangle)
        {
        }

        /// <summary>
        /// This method is called after the process is applied to prepare the processor.
        /// </summary>
        /// <param name="target">Target image to apply the process to.</param>
        /// <param name="source">The source image. Cannot be null.</param>
        /// <param name="targetRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the location and size of the drawn image.
        /// The image is scaled to fit the rectangle.
        /// </param>
        /// <param name="sourceRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to draw.
        /// </param>
        protected virtual void AfterApply(ImageBase<TColor, TPacked> target, ImageBase<TColor, TPacked> source, Rectangle targetRectangle, Rectangle sourceRectangle)
        {
        }
    }
}