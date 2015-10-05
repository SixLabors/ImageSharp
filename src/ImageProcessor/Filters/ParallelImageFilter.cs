namespace ImageProcessor.Filters
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Allows the application of filters using prallel processing.
    /// </summary>
    public abstract class ParallelImageFilter : IImageFilter
    {
        /// <summary>
        /// Gets or sets the count of workers to run the filter in parallel.
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
        /// This method is called before the filter is applied to prepare the filter.
        /// </summary>
        protected virtual void OnApply()
        {
        }

        protected abstract void Apply(ImageBase target, ImageBase source, Rectangle rectangle, int startY, int endY);
    }
}
