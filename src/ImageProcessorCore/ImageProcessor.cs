// <copyright file="ImageProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Allows the application of processors to images.
    /// </summary>
    public abstract class ImageProcessor<TColor, TPacked> : IImageProcessor<TColor, TPacked>
        where TColor : IPackedVector<TPacked>
        where TPacked : struct
    {
        /// <inheritdoc/>
        public event ProgressEventHandler OnProgress;

        /// <summary>
        /// The number of rows processed by a derived class.
        /// </summary>
        private int numRowsProcessed;

        /// <summary>
        /// The total number of rows that will be processed by a derived class.
        /// </summary>
        private int totalRows;

        /// <inheritdoc/>
        public virtual ParallelOptions ParallelOptions { get; set; } = Bootstrapper.Instance.ParallelOptions;

        /// <inheritdoc/>
        public virtual bool Compand { get; set; } = false;

        /// <inheritdoc/>
        public void Apply(ImageBase<TColor, TPacked> target, ImageBase<TColor, TPacked> source, Rectangle sourceRectangle)
        {
            try
            {
                this.OnApply(target, source, target.Bounds, sourceRectangle);

                this.numRowsProcessed = 0;
                this.totalRows = sourceRectangle.Height;

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

                this.numRowsProcessed = 0;
                this.totalRows = targetRectangle.Height;

                this.Apply(target, source, targetRectangle, sourceRectangle, targetRectangle.Y, targetRectangle.Bottom);

                this.AfterApply(target, source, target.Bounds, sourceRectangle);
            }
            catch (Exception ex)
            {
                throw new ImageProcessingException($"An error occured when processing the image using {this.GetType().Name}. See the inner exception for more detail.", ex);
            }
        }

        /// <summary>
        /// This method is called before the process is applied to prepare the processor.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
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
        /// The method keeps the source image unchanged and returns the
        /// the result of image process as new image.
        /// </remarks>
        protected abstract void Apply(ImageBase<TColor, TPacked> target, ImageBase<TColor, TPacked> source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY);

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

        /// <summary>
        /// Must be called by derived classes after processing a single row.
        /// </summary>
        protected void OnRowProcessed()
        {
            if (this.OnProgress != null)
            {
                int currThreadNumRows = Interlocked.Add(ref this.numRowsProcessed, 1);

                // Multi-pass filters process multiple times more rows than totalRows, so update totalRows on the fly
                if (currThreadNumRows > this.totalRows)
                {
                    this.totalRows = currThreadNumRows;
                }

                // Report progress. This may be on the client's thread, or on a Task library thread.
                this.OnProgress(this, new ProgressEventArgs { RowsProcessed = currThreadNumRows, TotalRows = this.totalRows });
            }
        }
    }
}
