// <copyright file="ImageProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Allows the application of processors to images.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public abstract class ImageProcessor<TColor, TPacked> : IImageProcessor
        where TColor : IPackedVector<TPacked>
        where TPacked : struct
    {
        /// <summary>
        /// Gets or sets the number of rows processed by a derived class.
        /// </summary>
        protected int NumRowsProcessed;

        /// <inheritdoc/>
        public event ProgressEventHandler OnProgress;

        /// <inheritdoc/>
        public virtual ParallelOptions ParallelOptions { get; set; } = Bootstrapper.Instance.ParallelOptions;

        /// <inheritdoc/>
        public virtual bool Compand { get; set; } = false;

        /// <summary>
        /// Gets or sets the total number of rows that will be processed by a derived class.
        /// </summary>
        protected int TotalRows { get; set; }

        /// <summary>
        /// Must be called by derived classes after processing a single row.
        /// </summary>
        protected void OnRowProcessed()
        {
            if (this.OnProgress != null)
            {
                int currThreadNumRows = Interlocked.Add(ref this.NumRowsProcessed, 1);

                // Multi-pass filters process multiple times more rows than totalRows, so update totalRows on the fly
                if (currThreadNumRows > this.TotalRows)
                {
                    this.TotalRows = currThreadNumRows;
                }

                // Report progress. This may be on the client's thread, or on a Task library thread.
                this.OnProgress(this, new ProgressEventArgs { RowsProcessed = currThreadNumRows, TotalRows = this.TotalRows });
            }
        }
    }
}