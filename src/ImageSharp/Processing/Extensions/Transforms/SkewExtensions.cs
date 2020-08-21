// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing.Processors.Transforms;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Defines extensions that allow the application of skew operations on an <see cref="Image"/>
    /// using Mutate/Clone.
    /// </summary>
    public static class SkewExtensions
    {
        /// <summary>
        /// Skews an image by the given angles in degrees.
        /// </summary>
        /// <param name="source">The image to skew.</param>
        /// <param name="degreesX">The angle in degrees to perform the skew along the x-axis.</param>
        /// <param name="degreesY">The angle in degrees to perform the skew along the y-axis.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext
            Skew(this IImageProcessingContext source, float degreesX, float degreesY) =>
            Skew(source, degreesX, degreesY, KnownResamplers.Bicubic);

        /// <summary>
        /// Skews an image by the given angles in degrees using the specified sampling algorithm.
        /// </summary>
        /// <param name="source">The image to skew.</param>
        /// <param name="degreesX">The angle in degrees to perform the skew along the x-axis.</param>
        /// <param name="degreesY">The angle in degrees to perform the skew along the y-axis.</param>
        /// <param name="sampler">The <see cref="IResampler"/> to perform the resampling.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext Skew(
            this IImageProcessingContext source,
            float degreesX,
            float degreesY,
            IResampler sampler) =>
            source.ApplyProcessor(new SkewProcessor(degreesX, degreesY, sampler, source.GetCurrentSize()));
    }
}