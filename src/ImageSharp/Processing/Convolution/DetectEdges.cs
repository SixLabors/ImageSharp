// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Extension methods for the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Detects any edges within the image. Uses the <see cref="SobelProcessor{TPixel}"/> filter
        /// operating in Grayscale mode.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> DetectEdges<TPixel>(this IImageProcessingContext<TPixel> source)
            where TPixel : struct, IPixel<TPixel>
        {
            return DetectEdges(source, new SobelProcessor<TPixel> { Grayscale = true });
        }

        /// <summary>
        /// Detects any edges within the image. Uses the <see cref="SobelProcessor{TPixel}"/> filter
        /// operating in Grayscale mode.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> DetectEdges<TPixel>(this IImageProcessingContext<TPixel> source, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
        {
            return DetectEdges(source, rectangle, new SobelProcessor<TPixel> { Grayscale = true });
        }

        /// <summary>
        /// Detects any edges within the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="filter">The filter for detecting edges.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> DetectEdges<TPixel>(this IImageProcessingContext<TPixel> source, EdgeDetection filter)
            where TPixel : struct, IPixel<TPixel>
            => DetectEdges(source, GetProcessor<TPixel>(filter, true));

        /// <summary>
        /// Detects any edges within the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="filter">The filter for detecting edges.</param>
        /// <param name="grayscale">Whether to convert the image to Grayscale first. Defaults to true.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> DetectEdges<TPixel>(this IImageProcessingContext<TPixel> source, EdgeDetection filter, bool grayscale)
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
        /// <param name="grayscale">Whether to convert the image to Grayscale first. Defaults to true.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> DetectEdges<TPixel>(this IImageProcessingContext<TPixel> source, EdgeDetection filter, Rectangle rectangle, bool grayscale = true)
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

        private static IEdgeDetectorProcessor<TPixel> GetProcessor<TPixel>(EdgeDetection filter, bool grayscale)
            where TPixel : struct, IPixel<TPixel>
        {
            IEdgeDetectorProcessor<TPixel> processor;

            switch (filter)
            {
                case EdgeDetection.Kayyali:
                    processor = new KayyaliProcessor<TPixel> { Grayscale = grayscale };
                    break;

                case EdgeDetection.Kirsch:
                    processor = new KirschProcessor<TPixel> { Grayscale = grayscale };
                    break;

                case EdgeDetection.Lapacian3X3:
                    processor = new Laplacian3X3Processor<TPixel> { Grayscale = grayscale };
                    break;

                case EdgeDetection.Lapacian5X5:
                    processor = new Laplacian5X5Processor<TPixel> { Grayscale = grayscale };
                    break;

                case EdgeDetection.LaplacianOfGaussian:
                    processor = new LaplacianOfGaussianProcessor<TPixel> { Grayscale = grayscale };
                    break;

                case EdgeDetection.Prewitt:
                    processor = new PrewittProcessor<TPixel> { Grayscale = grayscale };
                    break;

                case EdgeDetection.RobertsCross:
                    processor = new RobertsCrossProcessor<TPixel> { Grayscale = grayscale };
                    break;

                case EdgeDetection.Robinson:
                    processor = new RobinsonProcessor<TPixel> { Grayscale = grayscale };
                    break;

                case EdgeDetection.Scharr:
                    processor = new ScharrProcessor<TPixel> { Grayscale = grayscale };
                    break;

                default:
                    processor = new SobelProcessor<TPixel> { Grayscale = grayscale };
                    break;
            }

            return processor;
        }
    }
}