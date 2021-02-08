// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing.Processors.Binarization;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Defines extension methods to apply binary thresholding on an <see cref="Image"/>
    /// using Mutate/Clone.
    /// </summary>
    public static class BinaryThresholdExtensions
    {
        /// <summary>
        /// Applies binarization to the image splitting the pixels at the given threshold.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="threshold">The threshold to apply binarization of the image. Must be between 0 and 1.</param>
        /// <param name="useSaturationNotLuminance">
        /// Use saturation value instead of luminance.
        /// </param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext BinaryThreshold(this IImageProcessingContext source, float threshold, bool useSaturationNotLuminance = false) =>
            source.ApplyProcessor(new BinaryThresholdProcessor(threshold, useSaturationNotLuminance));

        /// <summary>
        /// Applies binarization to the image splitting the pixels at the given threshold.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="threshold">The threshold to apply binarization of the image. Must be between 0 and 1.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <param name="useSaturationNotLuminance">
        /// Use saturation value instead of luminance.
        /// </param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext BinaryThreshold(
            this IImageProcessingContext source,
            float threshold,
            Rectangle rectangle,
            bool useSaturationNotLuminance = false) =>
            source.ApplyProcessor(new BinaryThresholdProcessor(threshold, useSaturationNotLuminance), rectangle);

        /// <summary>
        /// Applies binarization to the image splitting the pixels at the given threshold.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="threshold">The threshold to apply binarization of the image. Must be between 0 and 1.</param>
        /// <param name="upperColor">The color to use for pixels that are above the threshold.</param>
        /// <param name="lowerColor">The color to use for pixels that are below the threshold</param>
        /// <param name="useSaturationNotLuminance">
        /// Use saturation value instead of luminance.
        /// </param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext BinaryThreshold(
            this IImageProcessingContext source,
            float threshold,
            Color upperColor,
            Color lowerColor,
            bool useSaturationNotLuminance = false) =>
            source.ApplyProcessor(new BinaryThresholdProcessor(threshold, upperColor, lowerColor, useSaturationNotLuminance));

        /// <summary>
        /// Applies binarization to the image splitting the pixels at the given threshold.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="threshold">The threshold to apply binarization of the image. Must be between 0 and 1.</param>
        /// <param name="upperColor">The color to use for pixels that are above the threshold.</param>
        /// <param name="lowerColor">The color to use for pixels that are below the threshold</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <param name="useSaturationNotLuminance">
        /// Use saturation value instead of luminance.
        /// </param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext BinaryThreshold(
            this IImageProcessingContext source,
            float threshold,
            Color upperColor,
            Color lowerColor,
            Rectangle rectangle,
            bool useSaturationNotLuminance = false) =>
            source.ApplyProcessor(new BinaryThresholdProcessor(threshold, upperColor, lowerColor, useSaturationNotLuminance), rectangle);
    }
}
