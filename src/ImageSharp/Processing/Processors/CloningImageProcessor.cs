// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Allows the application of processors to images.
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

                    this.BeforeApply(sourceFrame, clonedFrame, sourceRectangle, configuration);
                    this.OnApply(sourceFrame, clonedFrame, sourceRectangle, configuration);
                    this.AfterApply(sourceFrame, clonedFrame, sourceRectangle, configuration);
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
                throw new ImageProcessingException($"An error occured when processing the image using {this.GetType().Name}. See the inner exception for more detail.", ex);
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

                source.SwapPixelsBuffers(cloned);
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
        protected virtual void BeforeApply(ImageFrame<TPixel> source, ImageFrame<TPixel> destination, Rectangle sourceRectangle, Configuration configuration)
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
        protected abstract void OnApply(ImageFrame<TPixel> source, ImageFrame<TPixel> destination, Rectangle sourceRectangle, Configuration configuration);

        /// <summary>
        /// This method is called after the process is applied to prepare the processor.
        /// </summary>
        /// <param name="source">The source image. Cannot be null.</param>
        /// <param name="destination">The cloned/destination image. Cannot be null.</param>
        /// <param name="sourceRectangle">The <see cref="Rectangle" /> structure that specifies the portion of the image object to draw.</param>
        /// <param name="configuration">The configuration.</param>
        protected virtual void AfterApply(ImageFrame<TPixel> source, ImageFrame<TPixel> destination, Rectangle sourceRectangle, Configuration configuration)
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