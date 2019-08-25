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
        /// <summary>
        /// Initializes a new instance of the <see cref="ImageProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
        protected ImageProcessor(Image<TPixel> source, Rectangle sourceRectangle)
        {
            this.Source = source;
            this.SourceRectangle = sourceRectangle;
            this.Configuration = this.Source.GetConfiguration();
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
        /// Gets the <see cref="ImageSharp.Configuration"/> instance to use when performing operations.
        /// </summary>
        protected Configuration Configuration { get; }

        /// <inheritdoc/>
        public void Apply()
        {
            try
            {
                this.BeforeImageApply();

                foreach (ImageFrame<TPixel> sourceFrame in this.Source.Frames)
                {
                    this.Apply(sourceFrame);
                }

                this.AfterImageApply();
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
        /// Applies the processor to just a single ImageBase.
        /// </summary>
        /// <param name="source">the source image.</param>
        public void Apply(ImageFrame<TPixel> source)
        {
            try
            {
                this.BeforeFrameApply(source);
                this.OnFrameApply(source);
                this.AfterFrameApply(source);
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
        protected virtual void BeforeImageApply()
        {
        }

        /// <summary>
        /// This method is called before the process is applied to prepare the processor.
        /// </summary>
        /// <param name="source">The source image. Cannot be null.</param>
        protected virtual void BeforeFrameApply(ImageFrame<TPixel> source)
        {
        }

        /// <summary>
        /// Applies the process to the specified portion of the specified <see cref="ImageFrame{TPixel}" /> at the specified location
        /// and with the specified size.
        /// </summary>
        /// <param name="source">The source image. Cannot be null.</param>
        protected abstract void OnFrameApply(ImageFrame<TPixel> source);

        /// <summary>
        /// This method is called after the process is applied to prepare the processor.
        /// </summary>
        /// <param name="source">The source image. Cannot be null.</param>
        protected virtual void AfterFrameApply(ImageFrame<TPixel> source)
        {
        }

        /// <summary>
        /// This method is called after the process is applied to prepare the processor.
        /// </summary>
        protected virtual void AfterImageApply()
        {
        }
    }
}
