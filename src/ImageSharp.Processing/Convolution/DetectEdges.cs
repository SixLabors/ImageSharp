// <copyright file="DetectEdges.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;

    using Processing;
    using Processing.Processors;

    /// <summary>
    /// Extension methods for the <see cref="Image{TColor}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Detects any edges within the image. Uses the <see cref="SobelProcessor{TColor}"/> filter
        /// operating in Grayscale mode.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <returns>The <see cref="Image{TColor}"/>.</returns>
        public static Image<TColor> DetectEdges<TColor>(this Image<TColor> source)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return DetectEdges(source, source.Bounds, new SobelProcessor<TColor> { Grayscale = true });
        }

        /// <summary>
        /// Detects any edges within the image. Uses the <see cref="SobelProcessor{TColor}"/> filter
        /// operating in Grayscale mode.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TColor}"/>.</returns>
        public static Image<TColor> DetectEdges<TColor>(this Image<TColor> source, Rectangle rectangle)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return DetectEdges(source, rectangle, new SobelProcessor<TColor> { Grayscale = true });
        }

        /// <summary>
        /// Detects any edges within the image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="filter">The filter for detecting edges.</param>
        /// <param name="grayscale">Whether to convert the image to Grayscale first. Defaults to true.</param>
        /// <returns>The <see cref="Image{TColor}"/>.</returns>
        public static Image<TColor> DetectEdges<TColor>(this Image<TColor> source, EdgeDetection filter, bool grayscale = true)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return DetectEdges(source, filter, source.Bounds, grayscale);
        }

        /// <summary>
        /// Detects any edges within the image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="filter">The filter for detecting edges.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <param name="grayscale">Whether to convert the image to Grayscale first. Defaults to true.</param>
        /// <returns>The <see cref="Image{TColor}"/>.</returns>
        public static Image<TColor> DetectEdges<TColor>(this Image<TColor> source, EdgeDetection filter, Rectangle rectangle, bool grayscale = true)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            IEdgeDetectorProcessor<TColor> processor;

            switch (filter)
            {
                case EdgeDetection.Kayyali:
                    processor = new KayyaliProcessor<TColor> { Grayscale = grayscale };
                    break;

                case EdgeDetection.Kirsch:
                    processor = new KirschProcessor<TColor> { Grayscale = grayscale };
                    break;

                case EdgeDetection.Lapacian3X3:
                    processor = new Laplacian3X3Processor<TColor> { Grayscale = grayscale };
                    break;

                case EdgeDetection.Lapacian5X5:
                    processor = new Laplacian5X5Processor<TColor> { Grayscale = grayscale };
                    break;

                case EdgeDetection.LaplacianOfGaussian:
                    processor = new LaplacianOfGaussianProcessor<TColor> { Grayscale = grayscale };
                    break;

                case EdgeDetection.Prewitt:
                    processor = new PrewittProcessor<TColor> { Grayscale = grayscale };
                    break;

                case EdgeDetection.RobertsCross:
                    processor = new RobertsCrossProcessor<TColor> { Grayscale = grayscale };
                    break;

                case EdgeDetection.Robinson:
                    processor = new RobinsonProcessor<TColor> { Grayscale = grayscale };
                    break;

                case EdgeDetection.Scharr:
                    processor = new ScharrProcessor<TColor> { Grayscale = grayscale };
                    break;

                default:
                    processor = new SobelProcessor<TColor> { Grayscale = grayscale };
                    break;
            }

            return DetectEdges(source, rectangle, processor);
        }

        /// <summary>
        /// Detects any edges within the image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="filter">The filter for detecting edges.</param>
        /// <returns>The <see cref="Image{TColor}"/>.</returns>
        public static Image<TColor> DetectEdges<TColor>(this Image<TColor> source, IEdgeDetectorProcessor<TColor> filter)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return DetectEdges(source, source.Bounds, filter);
        }

        /// <summary>
        /// Detects any edges within the image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <param name="filter">The filter for detecting edges.</param>
        /// <returns>The <see cref="Image{TColor}"/>.</returns>
        public static Image<TColor> DetectEdges<TColor>(this Image<TColor> source, Rectangle rectangle, IEdgeDetectorProcessor<TColor> filter)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            return source.Apply(rectangle, filter);
        }
    }
}