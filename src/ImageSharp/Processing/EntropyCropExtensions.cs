// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing.Processors.Transforms;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Adds extensions that allow the application of entropy cropping operations to the <see cref="Image"/> type.
    /// </summary>
    public static class EntropyCropExtensions
    {
        /// <summary>
        /// Crops an image to the area of greatest entropy using a threshold for entropic density of <value>.5F</value>.
        /// </summary>
        /// <param name="source">The image to crop.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext EntropyCrop(this IImageProcessingContext source) =>
            source.ApplyProcessor(new EntropyCropProcessor());

        /// <summary>
        /// Crops an image to the area of greatest entropy.
        /// </summary>
        /// <param name="source">The image to crop.</param>
        /// <param name="threshold">The threshold for entropic density.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext EntropyCrop(this IImageProcessingContext source, float threshold) =>
            source.ApplyProcessor(new EntropyCropProcessor(threshold));
    }
}