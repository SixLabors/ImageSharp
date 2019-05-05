// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing.Processors;
using SixLabors.ImageSharp.Processing.Processors.Convolution;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Defines edge detection extensions applicable on an <see cref="Image"/> using Mutate/Clone.
    /// </summary>
    public static class DetectEdgesExtensions
    {
        /// <summary>
        /// Detects any edges within the image. Uses the <see cref="SobelProcessor"/> filter
        /// operating in grayscale mode.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext DetectEdges(this IImageProcessingContext source) =>
            DetectEdges(source, new SobelProcessor(true));

        /// <summary>
        /// Detects any edges within the image. Uses the <see cref="SobelProcessor"/> filter
        /// operating in grayscale mode.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext DetectEdges(this IImageProcessingContext source, Rectangle rectangle) =>
            DetectEdges(source, rectangle, new SobelProcessor(true));

        /// <summary>
        /// Detects any edges within the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="filter">The filter for detecting edges.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext DetectEdges(
            this IImageProcessingContext source,
            EdgeDetectionOperators filter) =>
            DetectEdges(source, GetProcessor(filter, true));

        /// <summary>
        /// Detects any edges within the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="filter">The filter for detecting edges.</param>
        /// <param name="grayscale">Whether to convert the image to grayscale first. Defaults to true.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext DetectEdges(
            this IImageProcessingContext source,
            EdgeDetectionOperators filter,
            bool grayscale) =>
            DetectEdges(source, GetProcessor(filter, grayscale));

        /// <summary>
        /// Detects any edges within the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="filter">The filter for detecting edges.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <param name="grayscale">Whether to convert the image to grayscale first. Defaults to true.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext DetectEdges(
            this IImageProcessingContext source,
            EdgeDetectionOperators filter,
            Rectangle rectangle,
            bool grayscale = true) =>
            DetectEdges(source, rectangle, GetProcessor(filter, grayscale));

        /// <summary>
        /// Detects any edges within the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="filter">The filter for detecting edges.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        private static IImageProcessingContext DetectEdges(this IImageProcessingContext source, IImageProcessor filter)
        {
            return source.ApplyProcessor(filter);
        }

        /// <summary>
        /// Detects any edges within the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <param name="filter">The filter for detecting edges.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        private static IImageProcessingContext DetectEdges(
            this IImageProcessingContext source,
            Rectangle rectangle,
            IImageProcessor filter)
        {
            source.ApplyProcessor(filter, rectangle);
            return source;
        }

        private static IImageProcessor GetProcessor(EdgeDetectionOperators filter, bool grayscale)
        {
            IImageProcessor processor;

            switch (filter)
            {
                case EdgeDetectionOperators.Kayyali:
                    processor = new KayyaliProcessor(grayscale);
                    break;

                case EdgeDetectionOperators.Kirsch:
                    processor = new KirschProcessor(grayscale);
                    break;

                case EdgeDetectionOperators.Laplacian3x3:
                    processor = new Laplacian3x3Processor(grayscale);
                    break;

                case EdgeDetectionOperators.Laplacian5x5:
                    processor = new Laplacian5x5Processor(grayscale);
                    break;

                case EdgeDetectionOperators.LaplacianOfGaussian:
                    processor = new LaplacianOfGaussianProcessor(grayscale);
                    break;

                case EdgeDetectionOperators.Prewitt:
                    processor = new PrewittProcessor(grayscale);
                    break;

                case EdgeDetectionOperators.RobertsCross:
                    processor = new RobertsCrossProcessor(grayscale);
                    break;

                case EdgeDetectionOperators.Robinson:
                    processor = new RobinsonProcessor(grayscale);
                    break;

                case EdgeDetectionOperators.Scharr:
                    processor = new ScharrProcessor(grayscale);
                    break;

                default:
                    processor = new SobelProcessor(grayscale);
                    break;
            }

            return processor;
        }
    }
}