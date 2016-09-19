// <copyright file="IImageProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    using System.Threading.Tasks;

    /// <summary>
    /// Encapsulates methods to alter the pixels of an image.
    /// </summary>
    public interface IImageProcessor
    {
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
