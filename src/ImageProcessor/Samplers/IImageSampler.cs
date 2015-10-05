// <copyright file="IImageSampler.cs" company="James South">
// Copyright © James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Samplers
{
    /// <summary>
    /// Encapsulates the methods required for all image sampling (resizing) algorithms.
    /// </summary>
    public interface IImageSampler
    {
        /// <summary>
        /// Resizes the specified source image by creating a new image with
        /// the specified size which is a resized version of the passed image.
        /// </summary>
        /// <param name="source">The source image.</param>
        /// <param name="target">The target image.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        void Sample(ImageBase source, ImageBase target, int width, int height);
    }
}
