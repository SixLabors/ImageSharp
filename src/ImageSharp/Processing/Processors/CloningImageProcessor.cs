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
        /// <summary>
        /// Initializes a new instance of the <see cref="CloningImageProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="source">The target <see cref="Image{T}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The target area to process for the current processor instance.</param>
        protected CloningImageProcessor(Image<TPixel> source, Rectangle sourceRectangle)
        {
            this.Source = source;
            this.SourceRectangle = sourceRectangle;
            this.Configuration = this.Source.GetConfiguration();
        }

        /// <summary>
        /// Gets the target <see cref="Image{T}"/> for the current processor instance.
        /// </summary>
        protected Image<TPixel> Source { get; }

        /// <summary>
        /// Gets the target area to process for the current processor instance.
        /// </summary>
        protected Rectangle SourceRectangle { get; }

        /// <summary>
        /// Gets the <see cref="ImageSharp.Configuration"/> instance to use when performing operations.
        /// </summary>
        protected Configuration Configuration { get; }

        /// <inheritdoc/>
        public Image<TPixel> CloneAndApply()
        {
            try
            {
                Image<TPixel> clone = this.CreateDestination();

                if (clone.Frames.Count != this.Source.Frames.Count)
                {
                    throw new ImageProcessingException($"An error occurred when processing the image using {this.GetType().Name}. The processor changed the number of frames.");
                }

                Configuration configuration = this.Source.GetConfiguration();
                this.BeforeImageApply(clone);

                for (int i = 0; i < this.Source.Frames.Count; i++)
                {
                    ImageFrame<TPixel> sourceFrame = this.Source.Frames[i];
                    ImageFrame<TPixel> clonedFrame = clone.Frames[i];

                    this.BeforeFrameApply(sourceFrame, clonedFrame);
                    this.OnFrameApply(sourceFrame, clonedFrame);
                    this.AfterFrameApply(sourceFrame, clonedFrame);
                }

                this.AfterImageApply(clone);

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
        public void Apply()
        {
            using (Image<TPixel> cloned = this.CloneAndApply())
            {
                // we now need to move the pixel data/size data from one image base to another
                if (cloned.Frames.Count != this.Source.Frames.Count)
                {
                    throw new ImageProcessingException($"An error occurred when processing the image using {this.GetType().Name}. The processor changed the number of frames.");
                }

                this.Source.SwapOrCopyPixelsBuffersFrom(cloned);
            }
        }

        /// <summary>
        /// Generates a deep clone of the source image that operations should be applied to.
        /// </summary>
        /// <returns>The cloned image.</returns>
        protected virtual Image<TPixel> CreateDestination() => this.Source.Clone();

        /// <summary>
        /// This method is called before the process is applied to prepare the processor.
        /// </summary>
        /// <param name="destination">The cloned/destination image. Cannot be null.</param>
        /// The <see cref="SourceRectangle"/> structure that specifies the portion of the image object to draw.
        /// </param>
        protected virtual void BeforeImageApply(Image<TPixel> destination)
        {
        }

        /// <summary>
        /// This method is called before the process is applied to prepare the processor.
        /// </summary>
        /// <param name="source">The source image. Cannot be null.</param>
        /// <param name="destination">The cloned/destination image. Cannot be null.</param>
        protected virtual void BeforeFrameApply(ImageFrame<TPixel> source, ImageFrame<TPixel> destination)
        {
        }

        /// <summary>
        /// Applies the process to the specified portion of the specified <see cref="ImageFrame{TPixel}" /> at the specified location
        /// and with the specified size.
        /// </summary>
        /// <param name="source">The source image. Cannot be null.</param>
        /// <param name="destination">The cloned/destination image. Cannot be null.</param>
        protected abstract void OnFrameApply(ImageFrame<TPixel> source, ImageFrame<TPixel> destination);

        /// <summary>
        /// This method is called after the process is applied to prepare the processor.
        /// </summary>
        /// <param name="source">The source image. Cannot be null.</param>
        /// <param name="destination">The cloned/destination image. Cannot be null.</param>
        protected virtual void AfterFrameApply(ImageFrame<TPixel> source, ImageFrame<TPixel> destination)
        {
        }

        /// <summary>
        /// This method is called after the process is applied to prepare the processor.
        /// </summary>
        /// <param name="destination">The cloned/destination image. Cannot be null.</param>
        /// The <see cref="SourceRectangle"/> structure that specifies the portion of the image object to draw.
        /// </param>
        protected virtual void AfterImageApply(Image<TPixel> destination)
        {
        }
    }
}
