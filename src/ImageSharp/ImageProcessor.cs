// <copyright file="ImageProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Allows the application of processors to images.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    public abstract class ImageProcessor<TColor> : IImageProcessor<TColor>
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        /// <inheritdoc/>
        public virtual ParallelOptions ParallelOptions { get; set; }

        /// <inheritdoc/>
        public virtual bool Compand { get; set; } = false;

        /// <inheritdoc/>
        public void Apply(ImageBase<TColor> source, Rectangle sourceRectangle)
        {
            if (this.ParallelOptions == null)
            {
                this.ParallelOptions = source.Configuration.ParallelOptions;
            }

            try
            {
                this.BeforeApply(source, sourceRectangle);

                this.OnApply(source, sourceRectangle);

                this.AfterApply(source, sourceRectangle);
            }
            catch (Exception ex)
            {
                throw new ImageProcessingException($"An error occured when processing the image using {this.GetType().Name}. See the inner exception for more detail.", ex);
            }
        }

        /// <summary>
        /// This method is called before the process is applied to prepare the processor.
        /// </summary>
        /// <param name="source">The source image. Cannot be null.</param>
        /// <param name="sourceRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to draw.
        /// </param>
        protected virtual void BeforeApply(ImageBase<TColor> source, Rectangle sourceRectangle)
        {
        }

        /// <summary>
        /// Applies the process to the specified portion of the specified <see cref="ImageBase{TColor}"/> at the specified location
        /// and with the specified size.
        /// </summary>
        /// <param name="source">The source image. Cannot be null.</param>
        /// <param name="sourceRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to draw.
        /// </param>
        protected abstract void OnApply(ImageBase<TColor> source, Rectangle sourceRectangle);

        /// <summary>
        /// This method is called after the process is applied to prepare the processor.
        /// </summary>
        /// <param name="source">The source image. Cannot be null.</param>
        /// <param name="sourceRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to draw.
        /// </param>
        protected virtual void AfterApply(ImageBase<TColor> source, Rectangle sourceRectangle)
        {
        }
    }
}