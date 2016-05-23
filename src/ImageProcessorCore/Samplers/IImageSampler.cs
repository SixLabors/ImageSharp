// <copyright file="IImageSampler.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Samplers
{
    /// <summary>
    /// Acts as a marker for generic parameters that require an image sampler.
    /// </summary>
    public interface IImageSampler : IImageProcessor
    {
        /// <summary>
        /// Gets or sets a value indicating whether to compress
        /// or expand individual pixel colors the value on processing.
        /// </summary>
        bool Compand { get; set; }
    }
}
