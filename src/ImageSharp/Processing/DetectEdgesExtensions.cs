// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Convolution;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Adds edge detection extensions to the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static class DetectEdgesExtensions
    {
        /// <summary>
        /// Detects any edges within the image. Uses the <see cref="SobelProcessor{TPixel}"/> filter
        /// operating in grayscale mode.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> DetectEdges<TPixel>(this IImageProcessingContext<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
            => DetectEdges(source, new SobelProcessor<TPixel>(true));

        /// <summary>
        /// Detects any edges within the image. Uses the <see cref="SobelProcessor{TPixel}"/> filter
        /// operating in grayscale mode.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> DetectEdges<TPixel>(this IImageProcessingContext<TPixel> source, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
            => DetectEdges(source, rectangle, new SobelProcessor<TPixel>(true));

        /// <summary>
        /// Detects any edges within the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="filter">The filter for detecting edges.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> DetectEdges<TPixel>(this IImageProcessingContext<TPixel> source, EdgeDetectionOperators filter)
            where TPixel : struct, IPixel<TPixel>
            => DetectEdges(source, GetProcessor<TPixel>(filter, true));

        /// <summary>
        /// Detects any edges within the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="filter">The filter for detecting edges.</param>
        /// <param name="grayscale">Whether to convert the image to grayscale first. Defaults to true.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> DetectEdges<TPixel>(this IImageProcessingContext<TPixel> source, EdgeDetectionOperators filter, bool grayscale)
            where TPixel : struct, IPixel<TPixel>
            => DetectEdges(source, GetProcessor<TPixel>(filter, grayscale));

        /// <summary>
        /// Detects any edges within the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="filter">The filter for detecting edges.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <param name="grayscale">Whether to convert the image to grayscale first. Defaults to true.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> DetectEdges<TPixel>(this IImageProcessingContext<TPixel> source, EdgeDetectionOperators filter, Rectangle rectangle, bool grayscale = true)
            where TPixel : struct, IPixel<TPixel>
            => DetectEdges(source, rectangle, GetProcessor<TPixel>(filter, grayscale));

        /// <summary>
        /// Detects any edges within the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="filter">The filter for detecting edges.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> DetectEdges<TPixel>(this IImageProcessingContext<TPixel> source, IEdgeDetectorProcessor<TPixel> filter)
            where TPixel : struct, IPixel<TPixel>
        {
            return source.ApplyProcessor(filter);
        }

        /// <summary>
        /// Detects any edges within the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <param name="filter">The filter for detecting edges.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> DetectEdges<TPixel>(this IImageProcessingContext<TPixel> source, Rectangle rectangle, IEdgeDetectorProcessor<TPixel> filter)
            where TPixel : struct, IPixel<TPixel>
        {
            source.ApplyProcessor(filter, rectangle);
            return source;
        }

        private static IEdgeDetectorProcessor<TPixel> GetProcessor<TPixel>(EdgeDetectionOperators filter, bool grayscale)
            where TPixel : struct, IPixel<TPixel>
        {
            IEdgeDetectorProcessor<TPixel> processor;

            switch (filter)
            {
                case EdgeDetectionOperators.Kayyali:
                    processor = new KayyaliProcessor<TPixel>(grayscale);
                    break;

                case EdgeDetectionOperators.Kirsch:
                    processor = new KirschProcessor<TPixel>(grayscale);
                    break;

                case EdgeDetectionOperators.Laplacian3x3:
                    processor = new Laplacian3x3Processor<TPixel>(grayscale);
                    break;

                case EdgeDetectionOperators.Laplacian5x5:
                    processor = new Laplacian5x5Processor<TPixel>(grayscale);
                    break;

                case EdgeDetectionOperators.LaplacianOfGaussian:
                    processor = new LaplacianOfGaussianProcessor<TPixel>(grayscale);
                    break;

                case EdgeDetectionOperators.Prewitt:
                    processor = new PrewittProcessor<TPixel>(grayscale);
                    break;

                case EdgeDetectionOperators.RobertsCross:
                    processor = new RobertsCrossProcessor<TPixel>(grayscale);
                    break;

                case EdgeDetectionOperators.Robinson:
                    processor = new RobinsonProcessor<TPixel>(grayscale);
                    break;

                case EdgeDetectionOperators.Scharr:
                    processor = new ScharrProcessor<TPixel>(grayscale);
                    break;

                default:
                    processor = new SobelProcessor<TPixel>(grayscale);
                    break;
            }

            return processor;
        }
    }
}