// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors
{
    /// <summary>
    /// Allows the application of processors to images.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal abstract class ImageProcessor<TPixel> : IImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <inheritdoc/>
        public void Apply(Image<TPixel> source, Rectangle sourceRectangle)
        {
            try
            {
                Configuration config = source.GetConfiguration();
                this.BeforeImageApply(source, sourceRectangle);

                foreach (ImageFrame<TPixel> sourceFrame in source.Frames)
                {
                    this.Apply(sourceFrame, sourceRectangle, config);
                }

                this.AfterImageApply(source, sourceRectangle);
            }
#if DEBUG
            catch (Exception)
            {
                throw;
#else
            catch (Exception ex)
            {
                throw new ImageProcessingException($"An error occurred when processing the image using {this.GetType().Name}. See the inner exception for more detail.", ex);
#endif
            }
        }

        /// <summary>
        /// Applies the processor to just a single ImageBase
        /// </summary>
        /// <param name="source">the source image</param>
        /// <param name="sourceRectangle">the target</param>
        /// <param name="configuration">The configuration.</param>
        public void Apply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            try
            {
                this.BeforeFrameApply(source, sourceRectangle, configuration);
                this.OnFrameApply(source, sourceRectangle, configuration);
                this.AfterFrameApply(source, sourceRectangle, configuration);
            }
#if DEBUG
            catch (Exception)
            {
                throw;
#else
            catch (Exception ex)
            {
                throw new ImageProcessingException($"An error occurred when processing the image using {this.GetType().Name}. See the inner exception for more detail.", ex);
#endif
            }
        }

        /// <summary>
        /// This method is called before the process is applied to prepare the processor.
        /// </summary>
        /// <param name="source">The source image. Cannot be null.</param>
        /// <param name="sourceRectangle">The <see cref="Rectangle" /> structure that specifies the portion of the image object to draw.</param>
        protected virtual void BeforeImageApply(Image<TPixel> source, Rectangle sourceRectangle)
        {
        }

        /// <summary>
        /// This method is called before the process is applied to prepare the processor.
        /// </summary>
        /// <param name="source">The source image. Cannot be null.</param>
        /// <param name="sourceRectangle">The <see cref="Rectangle" /> structure that specifies the portion of the image object to draw.</param>
        /// <param name="configuration">The configuration.</param>
        protected virtual void BeforeFrameApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
        }

        /// <summary>
        /// Applies the process to the specified portion of the specified <see cref="ImageFrame{TPixel}" /> at the specified location
        /// and with the specified size.
        /// </summary>
        /// <param name="source">The source image. Cannot be null.</param>
        /// <param name="sourceRectangle">The <see cref="Rectangle" /> structure that specifies the portion of the image object to draw.</param>
        /// <param name="configuration">The configuration.</param>
        protected abstract void OnFrameApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration);

        /// <summary>
        /// This method is called after the process is applied to prepare the processor.
        /// </summary>
        /// <param name="source">The source image. Cannot be null.</param>
        /// <param name="sourceRectangle">The <see cref="Rectangle" /> structure that specifies the portion of the image object to draw.</param>
        /// <param name="configuration">The configuration.</param>
        protected virtual void AfterFrameApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
        }

        /// <summary>
        /// This method is called after the process is applied to prepare the processor.
        /// </summary>
        /// <param name="source">The source image. Cannot be null.</param>
        /// <param name="sourceRectangle">The <see cref="Rectangle" /> structure that specifies the portion of the image object to draw.</param>
        protected virtual void AfterImageApply(Image<TPixel> source, Rectangle sourceRectangle)
        {
        }
    }
}