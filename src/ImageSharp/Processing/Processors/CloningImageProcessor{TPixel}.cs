// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors
{
    /// <summary>
    /// The base class for all pixel specific cloning image processors.
    /// Allows the application of processing algorithms to the image.
    /// The image is cloned before operating upon and the buffers swapped upon completion.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    public abstract class CloningImageProcessor<TPixel> : ICloningImageProcessor<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CloningImageProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
        protected CloningImageProcessor(Configuration configuration, Image<TPixel> source, Rectangle sourceRectangle)
        {
            this.Configuration = configuration;
            this.Source = source;
            this.SourceRectangle = sourceRectangle;
        }

        /// <summary>
        /// Gets The source <see cref="Image{TPixel}"/> for the current processor instance.
        /// </summary>
        protected Image<TPixel> Source { get; }

        /// <summary>
        /// Gets The source area to process for the current processor instance.
        /// </summary>
        protected Rectangle SourceRectangle { get; }

        /// <summary>
        /// Gets the <see cref="Configuration"/> instance to use when performing operations.
        /// </summary>
        protected Configuration Configuration { get; }

        /// <inheritdoc/>
        Image<TPixel> ICloningImageProcessor<TPixel>.CloneAndExecute()
        {
            try
            {
                Image<TPixel> clone = this.CreateTarget();
                this.CheckFrameCount(this.Source, clone);

                Configuration configuration = this.Configuration;
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
        void IImageProcessor<TPixel>.Execute()
        {
            // Create an interim clone of the source image to operate on.
            // Doing this allows for the application of transforms that will alter
            // the dimensions of the image.
            Image<TPixel> clone = default;
            try
            {
                clone = ((ICloningImageProcessor<TPixel>)this).CloneAndExecute();

                // We now need to move the pixel data/size data from the clone to the source.
                this.CheckFrameCount(this.Source, clone);
                this.Source.SwapOrCopyPixelsBuffersFrom(clone);
            }
            finally
            {
                // Dispose of the clone now that we have swapped the pixel/size data.
                clone?.Dispose();
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Gets the size of the destination image.
        /// </summary>
        /// <returns>The <see cref="Size"/>.</returns>
        protected abstract Size GetDestinationSize();

        /// <summary>
        /// This method is called before the process is applied to prepare the processor.
        /// </summary>
        /// <param name="destination">The cloned/destination image. Cannot be null.</param>
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
        protected virtual void AfterImageApply(Image<TPixel> destination)
        {
        }

        /// <summary>
        /// Disposes the object and frees resources for the Garbage Collector.
        /// </summary>
        /// <param name="disposing">Whether to dispose managed and unmanaged objects.</param>
        protected virtual void Dispose(bool disposing)
        {
        }

        private Image<TPixel> CreateTarget()
        {
            Image<TPixel> source = this.Source;
            Size destinationSize = this.GetDestinationSize();

            // We will always be creating the clone even for mutate because we may need to resize the canvas.
            var destinationFrames = new ImageFrame<TPixel>[source.Frames.Count];
            for (int i = 0; i < destinationFrames.Length; i++)
            {
                destinationFrames[i] = new ImageFrame<TPixel>(
                    this.Configuration,
                    destinationSize.Width,
                    destinationSize.Height,
                    source.Frames[i].Metadata.DeepClone());
            }

            // Use the overload to prevent an extra frame being added.
            return new Image<TPixel>(this.Configuration, source.Metadata.DeepClone(), destinationFrames);
        }

        private void CheckFrameCount(Image<TPixel> a, Image<TPixel> b)
        {
            if (a.Frames.Count != b.Frames.Count)
            {
                throw new ImageProcessingException($"An error occurred when processing the image using {this.GetType().Name}. The processor changed the number of frames.");
            }
        }
    }
}
