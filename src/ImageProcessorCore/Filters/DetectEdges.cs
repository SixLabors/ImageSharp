// <copyright file="DetectEdges.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    using Processors;

    /// <summary>
    /// Extension methods for the <see cref="Image{T,TP}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Detects any edges within the image. Uses the <see cref="SobelProcessor"/> filter
        /// operating in Grayscale mode.
        /// </summary>
        /// <typeparam name="T">The pixel format.</typeparam>
        /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image{T,TP}"/>.</returns>
        public static Image<T, TP> DetectEdges<T, TP>(this Image<T, TP> source, ProgressEventHandler progressHandler = null)
            where T : IPackedVector<TP>
            where TP : struct
        {
            return DetectEdges(source, source.Bounds, new SobelProcessor<T, TP> { Grayscale = true }, progressHandler);
        }

        /// <summary>
        /// Detects any edges within the image.
        /// </summary>
        /// <typeparam name="T">The pixel format.</typeparam>
        /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="filter">The filter for detecting edges.</param>
        /// <param name="Grayscale">Whether to convert the image to Grayscale first. Defaults to true.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image{T,TP}"/>.</returns>
        public static Image<T, TP> DetectEdges<T, TP>(this Image<T, TP> source, EdgeDetection filter, bool Grayscale = true, ProgressEventHandler progressHandler = null)
            where T : IPackedVector<TP>
            where TP : struct
        {
            IEdgeDetectorFilter<T, TP> processor;

            switch (filter)
            {
                case EdgeDetection.Kayyali:
                    processor = new KayyaliProcessor<T, TP> { Grayscale = Grayscale };
                    break;

                case EdgeDetection.Kirsch:
                    processor = new KirschProcessor<T, TP> { Grayscale = Grayscale };
                    break;

                case EdgeDetection.Lapacian3X3:
                    processor = new Laplacian3X3Processor<T, TP> { Grayscale = Grayscale };
                    break;

                case EdgeDetection.Lapacian5X5:
                    processor = new Laplacian5X5Processor<T, TP> { Grayscale = Grayscale };
                    break;

                case EdgeDetection.LaplacianOfGaussian:
                    processor = new LaplacianOfGaussianProcessor<T, TP> { Grayscale = Grayscale };
                    break;

                case EdgeDetection.Prewitt:
                    processor = new PrewittProcessor<T, TP> { Grayscale = Grayscale };
                    break;

                case EdgeDetection.RobertsCross:
                    processor = new RobertsCrossProcessor<T, TP> { Grayscale = Grayscale };
                    break;

                case EdgeDetection.Scharr:
                    processor = new ScharrProcessor<T, TP> { Grayscale = Grayscale };
                    break;

                default:
                    processor = new ScharrProcessor<T, TP> { Grayscale = Grayscale };
                    break;
            }

            return DetectEdges(source, source.Bounds, processor, progressHandler);
        }

        /// <summary>
        /// Detects any edges within the image.
        /// </summary>
        /// <typeparam name="T">The pixel format.</typeparam>
        /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="filter">The filter for detecting edges.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image{T,TP}"/>.</returns>
        public static Image<T, TP> DetectEdges<T, TP>(this Image<T, TP> source, IEdgeDetectorFilter<T, TP> filter, ProgressEventHandler progressHandler = null)
            where T : IPackedVector<TP>
            where TP : struct
        {
            return DetectEdges(source, source.Bounds, filter, progressHandler);
        }

        /// <summary>
        /// Detects any edges within the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <param name="filter">The filter for detecting edges.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image{T,TP}"/>.</returns>
        public static Image<T, TP> DetectEdges<T, TP>(this Image<T, TP> source, Rectangle rectangle, IEdgeDetectorFilter<T, TP> filter, ProgressEventHandler progressHandler = null)
            where T : IPackedVector<TP>
            where TP : struct
        {
            filter.OnProgress += progressHandler;

            try
            {
                return source.Process(rectangle, filter);
            }
            finally
            {
                filter.OnProgress -= progressHandler;
            }
        }
    }
}
