// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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
        /// Applies binarization to the image splitting the pixels at the given threshold with
        /// Luminance as the color component to be compared to threshold.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="threshold">The threshold to apply binarization of the image. Must be between 0 and 1.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext BinaryThreshold(this IImageProcessingContext source, float threshold)
            => source.ApplyProcessor(new BinaryThresholdProcessor(threshold, BinaryThresholdMode.Luminance));

        /// <summary>
        /// Applies binarization to the image splitting the pixels at the given threshold.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="threshold">The threshold to apply binarization of the image. Must be between 0 and 1.</param>
        /// <param name="mode">Selects the value to be compared to threshold.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext BinaryThreshold(
            this IImageProcessingContext source,
            float threshold,
            BinaryThresholdMode mode)
            => source.ApplyProcessor(new BinaryThresholdProcessor(threshold, mode));

        /// <summary>
        /// Applies binarization to the image splitting the pixels at the given threshold with
        /// Luminance as the color component to be compared to threshold.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="threshold">The threshold to apply binarization of the image. Must be between 0 and 1.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext BinaryThreshold(
            this IImageProcessingContext source,
            float threshold,
            Rectangle rectangle)
            => source.ApplyProcessor(new BinaryThresholdProcessor(threshold, BinaryThresholdMode.Luminance), rectangle);

        /// <summary>
        /// Applies binarization to the image splitting the pixels at the given threshold.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="threshold">The threshold to apply binarization of the image. Must be between 0 and 1.</param>
        /// <param name="mode">Selects the value to be compared to threshold.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext BinaryThreshold(
            this IImageProcessingContext source,
            float threshold,
            BinaryThresholdMode mode,
            Rectangle rectangle)
            => source.ApplyProcessor(new BinaryThresholdProcessor(threshold, mode), rectangle);

        /// <summary>
        /// Applies binarization to the image splitting the pixels at the given threshold with
        /// Luminance as the color component to be compared to threshold.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="threshold">The threshold to apply binarization of the image. Must be between 0 and 1.</param>
        /// <param name="upperColor">The color to use for pixels that are above the threshold.</param>
        /// <param name="lowerColor">The color to use for pixels that are below the threshold</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext BinaryThreshold(
            this IImageProcessingContext source,
            float threshold,
            Color upperColor,
            Color lowerColor)
            => source.ApplyProcessor(new BinaryThresholdProcessor(threshold, upperColor, lowerColor, BinaryThresholdMode.Luminance));

        /// <summary>
        /// Applies binarization to the image splitting the pixels at the given threshold.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="threshold">The threshold to apply binarization of the image. Must be between 0 and 1.</param>
        /// <param name="upperColor">The color to use for pixels that are above the threshold.</param>
        /// <param name="lowerColor">The color to use for pixels that are below the threshold</param>
        /// <param name="mode">Selects the value to be compared to threshold.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext BinaryThreshold(
            this IImageProcessingContext source,
            float threshold,
            Color upperColor,
            Color lowerColor,
            BinaryThresholdMode mode)
            => source.ApplyProcessor(new BinaryThresholdProcessor(threshold, upperColor, lowerColor, mode));

        /// <summary>
        /// Applies binarization to the image splitting the pixels at the given threshold with
        /// Luminance as the color component to be compared to threshold.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="threshold">The threshold to apply binarization of the image. Must be between 0 and 1.</param>
        /// <param name="upperColor">The color to use for pixels that are above the threshold.</param>
        /// <param name="lowerColor">The color to use for pixels that are below the threshold</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext BinaryThreshold(
            this IImageProcessingContext source,
            float threshold,
            Color upperColor,
            Color lowerColor,
            Rectangle rectangle)
            => source.ApplyProcessor(new BinaryThresholdProcessor(threshold, upperColor, lowerColor, BinaryThresholdMode.Luminance), rectangle);

        /// <summary>
        /// Applies binarization to the image splitting the pixels at the given threshold.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="threshold">The threshold to apply binarization of the image. Must be between 0 and 1.</param>
        /// <param name="upperColor">The color to use for pixels that are above the threshold.</param>
        /// <param name="lowerColor">The color to use for pixels that are below the threshold</param>
        /// <param name="mode">Selects the value to be compared to threshold.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext BinaryThreshold(
            this IImageProcessingContext source,
            float threshold,
            Color upperColor,
            Color lowerColor,
            BinaryThresholdMode mode,
            Rectangle rectangle) =>
            source.ApplyProcessor(new BinaryThresholdProcessor(threshold, upperColor, lowerColor, mode), rectangle);
    }
}
