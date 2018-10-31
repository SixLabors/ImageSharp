using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Binarization;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Extensions to perform AdaptiveThreshold through Mutator
    /// </summary>
    public static class AdaptiveThresholdExtensions
    {
        /// <summary>
        /// Applies Bradley Adaptive Threshold to the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> AdaptiveThreshold<TPixel>(this IImageProcessingContext<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new AdaptiveThresholdProcessor<TPixel>());

        /// <summary>
        /// Applies Bradley Adaptive Threshold to the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="thresholdLimit">Threshold limit (0.0-1.0) to consider for binarization.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> AdaptiveThreshold<TPixel>(this IImageProcessingContext<TPixel> source, float thresholdLimit)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new AdaptiveThresholdProcessor<TPixel>(thresholdLimit));

        /// <summary>
        /// Applies Bradley Adaptive Threshold to the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="upper">Upper (white) color for thresholding.</param>
        /// <param name="lower">Lower (black) color for thresholding</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> AdaptiveThreshold<TPixel>(this IImageProcessingContext<TPixel> source, TPixel upper, TPixel lower)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new AdaptiveThresholdProcessor<TPixel>(upper, lower));

        /// <summary>
        /// Applies Bradley Adaptive Threshold to the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="upper">Upper (white) color for thresholding.</param>
        /// <param name="lower">Lower (black) color for thresholding</param>
        /// <param name="thresholdLimit">Threshold limit (0.0-1.0) to consider for binarization.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> AdaptiveThreshold<TPixel>(this IImageProcessingContext<TPixel> source, TPixel upper, TPixel lower, float thresholdLimit)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new AdaptiveThresholdProcessor<TPixel>(upper, lower, thresholdLimit));

        /// <summary>
        /// Applies Bradley Adaptive Threshold to the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="upper">Upper (white) color for thresholding.</param>
        /// <param name="lower">Lower (black) color for thresholding</param>
        /// <param name="rectangle">Rectangle region to apply the processor on.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> AdaptiveThreshold<TPixel>(this IImageProcessingContext<TPixel> source, TPixel upper, TPixel lower, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new AdaptiveThresholdProcessor<TPixel>(upper, lower), rectangle);

        /// <summary>
        /// Applies Bradley Adaptive Threshold to the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="upper">Upper (white) color for thresholding.</param>
        /// <param name="lower">Lower (black) color for thresholding</param>
        /// <param name="thresholdLimit">Threshold limit (0.0-1.0) to consider for binarization.</param>
        /// <param name="rectangle">Rectangle region to apply the processor on.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> AdaptiveThreshold<TPixel>(this IImageProcessingContext<TPixel> source, TPixel upper, TPixel lower, float thresholdLimit, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new AdaptiveThresholdProcessor<TPixel>(upper, lower, thresholdLimit), rectangle);
    }
}