// <copyright file="IImageProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Threading.Tasks;

namespace ImageProcessorCore.Processors
{
    /// <summary>
    /// A delegate which is called as progress is made processing an image.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">An object that contains the event data.</param>
    public delegate void ProgressEventHandler(object sender, ProgressEventArgs e);

    /// <summary>
    /// Encapsulates methods to alter the pixels of an image.
    /// </summary>
    public interface IImageProcessor
    {
        /// <summary>
        /// Event fires when each row of the source image has been processed.
        /// </summary>
        /// <remarks>
        /// This event may be called from threads other than the client thread, and from multiple threads simultaneously.
        /// Individual row notifications may arrived out of order.
        /// </remarks>
        event ProgressEventHandler OnProgress;

        /// <summary>
        /// Gets or sets the parallel options for processing tasks in parallel.
        /// </summary>
        ParallelOptions ParallelOptions { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to compress
        /// or expand individual pixel colors the value on processing.
        /// </summary>
        bool Compand { get; set; }
    }
}
