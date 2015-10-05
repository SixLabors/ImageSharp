// <copyright file="ParallelImageProcessor.cs" company="James South">
// Copyright © James South and contributors.
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
        public int Parallelism { get; set; } = Environment.ProcessorCount;

        /// <inheritdoc/>
        public void Apply(ImageBase target, ImageBase source, Rectangle rectangle)
        {
            this.OnApply();

            if (this.Parallelism > 1)
            {
                int partitionCount = this.Parallelism;

                Task[] tasks = new Task[partitionCount];

                for (int p = 0; p < partitionCount; p++)
                {
                    int current = p;
                    tasks[p] = Task.Run(() =>
                    {
                        int batchSize = rectangle.Height / partitionCount;
                        int yStart = rectangle.Y + (current * batchSize);
                        int yEnd = current == partitionCount - 1 ? rectangle.Bottom : yStart + batchSize;

                        this.Apply(target, source, rectangle, yStart, yEnd);
                    });
                }

                Task.WaitAll(tasks);
            }
            else
            {
                this.Apply(target, source, rectangle, rectangle.Y, rectangle.Bottom);
            }
        }

        /// <summary>
        /// This method is called before the process is applied to prepare the processor.
        /// </summary>
        protected virtual void OnApply()
        {
        }

        /// <summary>
        /// Apply a process to an image to alter the pixels at the area of the specified rectangle.
        /// </summary>
        /// <param name="target">Target image to apply the process to.</param>
        /// <param name="source">The source image. Cannot be null.</param>
        /// <param name="rectangle">
        /// The rectangle, which defines the area of the image where the process should be applied to.
        /// </param>
        /// <param name="startY">The index of the row within the image to start processing.</param>
        /// <param name="endY">The index of the row within the image to end processing.</param>
        /// <remarks>
        /// The method keeps the source image unchanged and returns the
        /// the result of image processing filter as new image.
        /// </remarks>
        protected abstract void Apply(ImageBase target, ImageBase source, Rectangle rectangle, int startY, int endY);
    }
}
