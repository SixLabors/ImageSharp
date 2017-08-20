// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

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
                    throw new ImageProcessingException($"An error occured when processing the image using {this.GetType().Name}. The processor changed the number of frames.");
                }

                this.BeforeImageApply(source, clone, sourceRectangle);

                this.BeforeApply(source, clone, sourceRectangle);
                this.OnApply(source, clone, sourceRectangle);
                this.AfterApply(source, clone, sourceRectangle);

                for (int i = 0; i < source.Frames.Count; i++)
                {
                    ImageFrame<TPixel> sourceFrame = source.Frames[i];
                    ImageFrame<TPixel> clonedFrame = clone.Frames[i];

                    this.BeforeApply(sourceFrame, clonedFrame, sourceRectangle);

                    this.OnApply(sourceFrame, clonedFrame, sourceRectangle);
                    this.AfterApply(sourceFrame, clonedFrame, sourceRectangle);
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
                    throw new ImageProcessingException($"An error occured when processing the image using {this.GetType().Name}. The processor changed the number of frames.");
                }

                source.SwapPixelsData(cloned);
                for (int i = 0; i < source.Frames.Count; i++)
                {
                    source.Frames[i].SwapPixelsData(cloned.Frames[i]);
                }
            }
        }

        /// <summary>
        /// Generates a deep clone of the source image that operatinos should be applied to.
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
        /// <param name="sourceRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to draw.
        /// </param>
        protected virtual void BeforeApply(ImageBase<TPixel> source, ImageBase<TPixel> destination, Rectangle sourceRectangle)
        {
        }

        /// <summary>
        /// Applies the process to the specified portion of the specified <see cref="ImageBase{TPixel}"/> at the specified location
        /// and with the specified size.
        /// </summary>
        /// <param name="source">The source image. Cannot be null.</param>
        /// <param name="destination">The cloned/destination image. Cannot be null.</param>
        /// <param name="sourceRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to draw.
        /// </param>
        protected abstract void OnApply(ImageBase<TPixel> source, ImageBase<TPixel> destination, Rectangle sourceRectangle);

        /// <summary>
        /// This method is called after the process is applied to prepare the processor.
        /// </summary>
        /// <param name="source">The source image. Cannot be null.</param>
        /// <param name="destination">The cloned/destination image. Cannot be null.</param>
        /// <param name="sourceRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to draw.
        /// </param>
        protected virtual void AfterApply(ImageBase<TPixel> source, ImageBase<TPixel> destination, Rectangle sourceRectangle)
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