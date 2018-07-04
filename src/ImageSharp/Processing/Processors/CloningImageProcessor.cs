// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors
{
    /// <summary>
    /// Allows the application of processing algorithms to a clone of the original image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal abstract class CloningImageProcessor<TPixel> : ICloningImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <inheritdoc/>
        public Image<TPixel> CloneAndApply(Image<TPixel> source, Rectangle sourceRectangle)
        {
            try
            {
                Image<TPixel> clone = this.CreateDestination(source, sourceRectangle);

                if (clone.Frames.Count != source.Frames.Count)
                {
                    throw new ImageProcessingException($"An error occurred when processing the image using {this.GetType().Name}. The processor changed the number of frames.");
                }

                Configuration configuration = source.GetConfiguration();
                this.BeforeImageApply(source, clone, sourceRectangle);

                for (int i = 0; i < source.Frames.Count; i++)
                {
                    ImageFrame<TPixel> sourceFrame = source.Frames[i];
                    ImageFrame<TPixel> clonedFrame = clone.Frames[i];

                    this.BeforeFrameApply(sourceFrame, clonedFrame, sourceRectangle, configuration);
                    this.OnFrameApply(sourceFrame, clonedFrame, sourceRectangle, configuration);
                    this.AfterFrameApply(sourceFrame, clonedFrame, sourceRectangle, configuration);
                }

                this.AfterImageApply(source, clone, sourceRectangle);

                return clone;
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

        /// <inheritdoc/>
        public void Apply(Image<TPixel> source, Rectangle sourceRectangle)
        {
            using (Image<TPixel> cloned = this.CloneAndApply(source, sourceRectangle))
            {
                // we now need to move the pixel data/size data from one image base to another
                if (cloned.Frames.Count != source.Frames.Count)
                {
                    throw new ImageProcessingException($"An error occurred when processing the image using {this.GetType().Name}. The processor changed the number of frames.");
                }

                source.SwapOrCopyPixelsBuffersFrom(cloned);
            }
        }

        /// <summary>
        /// Generates a deep clone of the source image that operations should be applied to.
        /// </summary>
        /// <param name="source">The source image. Cannot be null.</param>
        /// <param name="sourceRectangle">The source rectangle.</param>
        /// <returns>The cloned image.</returns>
        protected virtual Image<TPixel> CreateDestination(Image<TPixel> source, Rectangle sourceRectangle)
        {
            return source.Clone();
        }

        /// <summary>
        /// This method is called before the process is applied to prepare the processor.
        /// </summary>
        /// <param name="source">The source image. Cannot be null.</param>
        /// <param name="destination">The cloned/destination image. Cannot be null.</param>
        /// <param name="sourceRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to draw.
        /// </param>
        protected virtual void BeforeImageApply(Image<TPixel> source, Image<TPixel> destination, Rectangle sourceRectangle)
        {
        }

        /// <summary>
        /// This method is called before the process is applied to prepare the processor.
        /// </summary>
        /// <param name="source">The source image. Cannot be null.</param>
        /// <param name="destination">The cloned/destination image. Cannot be null.</param>
        /// <param name="sourceRectangle">The <see cref="Rectangle" /> structure that specifies the portion of the image object to draw.</param>
        /// <param name="configuration">The configuration.</param>
        protected virtual void BeforeFrameApply(ImageFrame<TPixel> source, ImageFrame<TPixel> destination, Rectangle sourceRectangle, Configuration configuration)
        {
        }

        /// <summary>
        /// Applies the process to the specified portion of the specified <see cref="ImageFrame{TPixel}" /> at the specified location
        /// and with the specified size.
        /// </summary>
        /// <param name="source">The source image. Cannot be null.</param>
        /// <param name="destination">The cloned/destination image. Cannot be null.</param>
        /// <param name="sourceRectangle">The <see cref="Rectangle" /> structure that specifies the portion of the image object to draw.</param>
        /// <param name="configuration">The configuration.</param>
        protected abstract void OnFrameApply(ImageFrame<TPixel> source, ImageFrame<TPixel> destination, Rectangle sourceRectangle, Configuration configuration);

        /// <summary>
        /// This method is called after the process is applied to prepare the processor.
        /// </summary>
        /// <param name="source">The source image. Cannot be null.</param>
        /// <param name="destination">The cloned/destination image. Cannot be null.</param>
        /// <param name="sourceRectangle">The <see cref="Rectangle" /> structure that specifies the portion of the image object to draw.</param>
        /// <param name="configuration">The configuration.</param>
        protected virtual void AfterFrameApply(ImageFrame<TPixel> source, ImageFrame<TPixel> destination, Rectangle sourceRectangle, Configuration configuration)
        {
        }

        /// <summary>
        /// This method is called after the process is applied to prepare the processor.
        /// </summary>
        /// <param name="source">The source image. Cannot be null.</param>
        /// <param name="destination">The cloned/destination image. Cannot be null.</param>
        /// <param name="sourceRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to draw.
        /// </param>
        protected virtual void AfterImageApply(Image<TPixel> source, Image<TPixel> destination, Rectangle sourceRectangle)
        {
        }
    }
}