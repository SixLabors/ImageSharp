// <copyright file="IImageSampler.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    /// <summary>
    /// Acts as a marker for generic parameters that require an image sampler.
    /// </summary>
    public interface IImageSampler<T, TP> : IImageProcessor<T, TP>
        where T : IPackedVector<TP>
        where TP : struct
    {
    }
}
