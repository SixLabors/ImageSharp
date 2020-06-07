// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing.Processors.Transforms;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Defines extensions that allow the application of entropy cropping operations on an <see cref="Image"/>
    /// using Mutate/Clone.
    /// </summary>
    public static class EntropyCropExtensions
    {
        /// <summary>
        /// Crops an image to the area of greatest entropy using a threshold for entropic density of <value>.5F</value>.
        /// </summary>
        /// <param name="source">The image to crop.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext EntropyCrop(this IImageProcessingContext source) =>
            source.ApplyProcessor(new EntropyCropProcessor());

        /// <summary>
        /// Crops an image to the area of greatest entropy.
        /// </summary>
        /// <param name="source">The image to crop.</param>
        /// <param name="threshold">The threshold for entropic density.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext EntropyCrop(this IImageProcessingContext source, float threshold) =>
            source.ApplyProcessor(new EntropyCropProcessor(threshold));
    }
}