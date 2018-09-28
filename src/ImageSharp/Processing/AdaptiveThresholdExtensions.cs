using SixLabors.Primitives;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors;

namespace SixLabors.ImageSharp.Processing
{
    public static class AdaptiveThresholdExtensions
    {
        /// <summary>
        /// Applies Bradley Adaptive Threshold to the image
        /// </summary>
        /// <param name="source">The image this method extends</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns>The <see cref="Image{TPixel}"/></returns>
        public static IImageProcessingContext<TPixel> AdaptiveThreshold<TPixel>(this IImageProcessingContext<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new AdaptiveThresholdProcessor<TPixel>());

        /// <summary>
        /// Applies Bradley Adaptive Threshold to the image
        /// </summary>
        /// <param name="source">The image this method extends</param>
        /// <param name="rectangle">Rectangle region to apply the processor on</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <returns></returns>
        public static IImageProcessingContext<TPixel> AdaptiveThreshold<TPixel>(this IImageProcessingContext<TPixel> source, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new AdaptiveThresholdProcessor<TPixel>(), rectangle);
    }
}