// <copyright file="ImageSampler.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    /// <summary>
    /// Applies sampling methods to an image. 
    /// All processors requiring resampling or resizing should inherit from this.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public abstract class ImageSampler<TColor, TPacked> : ImageProcessor<TColor, TPacked>, IImageSampler<TColor, TPacked>
        where TColor : IPackedVector<TPacked>
        where TPacked : struct
    {
    }
}