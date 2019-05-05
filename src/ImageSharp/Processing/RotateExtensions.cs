﻿// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing.Processors.Transforms;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Adds extensions that allow the application of rotate operations to the <see cref="Image"/> type.
    /// </summary>
    public static class RotateExtensions
    {
        /// <summary>
        /// Rotates and flips an image by the given instructions.
        /// </summary>
        /// <param name="source">The image to rotate.</param>
        /// <param name="rotateMode">The <see cref="RotateMode"/> to perform the rotation.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        public static IImageProcessingContext Rotate(this IImageProcessingContext source, RotateMode rotateMode) =>
            Rotate(source, (float)rotateMode);

        /// <summary>
        /// Rotates an image by the given angle in degrees.
        /// </summary>
        /// <param name="source">The image to rotate.</param>
        /// <param name="degrees">The angle in degrees to perform the rotation.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        public static IImageProcessingContext Rotate(this IImageProcessingContext source, float degrees) =>
            Rotate(source, degrees, KnownResamplers.Bicubic);

        /// <summary>
        /// Rotates an image by the given angle in degrees using the specified sampling algorithm.
        /// </summary>
        /// <param name="source">The image to rotate.</param>
        /// <param name="degrees">The angle in degrees to perform the rotation.</param>
        /// <param name="sampler">The <see cref="IResampler"/> to perform the resampling.</param>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        public static IImageProcessingContext Rotate(
            this IImageProcessingContext source,
            float degrees,
            IResampler sampler) =>
            source.ApplyProcessor(new RotateProcessor(degrees, sampler, source.GetCurrentSize()));
    }
}