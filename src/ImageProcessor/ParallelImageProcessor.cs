// <copyright file="ParallelImageProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Allows the application of processors using parallel processing.
    /// </summary>
    public abstract class ParallelImageProcessor : IImageProcessor
    {
        /// <summary>
        /// Gets or sets the count of workers to run the process in parallel.
        /// </summary>
        public virtual int Parallelism { get; set; } = Environment.ProcessorCount;

        /// <inheritdoc/>
        public void Apply(ImageBase target, ImageBase source, Rectangle sourceRectangle)
        {
            // We don't want to affect the original source pixels so we make clone here.
            ImageFrame frame = source as ImageFrame;
            Image temp = frame != null ? new Image(frame) : new Image((Image)source);
            this.OnApply(temp, target, target.Bounds, sourceRectangle);

            if (this.Parallelism > 1)
            {
                int partitionCount = this.Parallelism;

                Task[] tasks = new Task[partitionCount];

                for (int p = 0; p < partitionCount; p++)
                {
                    int current = p;
                    tasks[p] = Task.Run(() =>
                    {
                        int batchSize = sourceRectangle.Height / partitionCount;
                        int yStart = sourceRectangle.Y + (current * batchSize);
                        int yEnd = current == partitionCount - 1 ? sourceRectangle.Bottom : yStart + batchSize;

                        this.Apply(target, temp, target.Bounds, sourceRectangle, yStart, yEnd);
                    });
                }

                Task.WaitAll(tasks);
            }
            else
            {
                this.Apply(target, temp, target.Bounds, sourceRectangle, sourceRectangle.Y, sourceRectangle.Bottom);
            }
        }

        /// <inheritdoc/>
        public void Apply(ImageBase target, ImageBase source, int width, int height, Rectangle targetRectangle = default(Rectangle), Rectangle sourceRectangle = default(Rectangle))
        {
            float[] pixels = new float[width * height * 4];
            target.SetPixels(width, height, pixels);

            if (sourceRectangle == Rectangle.Empty)
            {
                sourceRectangle = source.Bounds;
            }

            // We don't want to affect the original source pixels so we make clone here.
            ImageFrame frame = source as ImageFrame;
            Image temp = frame != null ? new Image(frame) : new Image((Image)source);
            this.OnApply(temp, target, target.Bounds, sourceRectangle);

            targetRectangle = target.Bounds;

            if (this.Parallelism > 1)
            {
                int partitionCount = this.Parallelism;

                Task[] tasks = new Task[partitionCount];

                for (int p = 0; p < partitionCount; p++)
                {
                    int current = p;
                    tasks[p] = Task.Run(() =>
                    {
                        int batchSize = targetRectangle.Bottom / partitionCount;
                        int yStart = current * batchSize;
                        int yEnd = current == partitionCount - 1 ? targetRectangle.Bottom : yStart + batchSize;

                        this.Apply(target, temp, targetRectangle, sourceRectangle, yStart, yEnd);
                    });
                }

                Task.WaitAll(tasks);
            }
            else
            {
                this.Apply(target, temp, targetRectangle, sourceRectangle, targetRectangle.Y, targetRectangle.Bottom);
            }
        }

        /// <summary>
        /// This method is called before the process is applied to prepare the processor.
        /// </summary>
        /// <param name="source">The source image. Cannot be null.</param>
        /// <param name="target">Target image to apply the process to.</param>
        /// <param name="targetRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the location and size of the drawn image.
        /// The image is scaled to fit the rectangle.
        /// </param>
        /// <param name="sourceRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to draw.
        /// </param>
        protected virtual void OnApply(ImageBase source, ImageBase target, Rectangle targetRectangle, Rectangle sourceRectangle)
        {
        }

        /// <summary>
        /// Applies the process to the specified portion of the specified <see cref="ImageBase"/> at the specified location
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
        protected abstract void Apply(ImageBase target, ImageBase source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY);
    }
}
