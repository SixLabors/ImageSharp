// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Threading.Tasks;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Primitives;
using SixLabors.ImageSharp.Processing.Filters.Processors;
using SixLabors.ImageSharp.Processing.Processors;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Convolution.Processors
{
    /// <summary>
    /// Defines a processor that detects edges within an image using a eight two dimensional matrices.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal abstract class EdgeDetectorCompassProcessor<TPixel> : ImageProcessor<TPixel>, IEdgeDetectorProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EdgeDetectorCompassProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="grayscale">Whether to convert the image to grayscale before performing edge detection.</param>
        protected EdgeDetectorCompassProcessor(bool grayscale)
        {
            this.Grayscale = grayscale;
        }

        /// <summary>
        /// Gets the North gradient operator
        /// </summary>
        public abstract DenseMatrix<float> North { get; }

        /// <summary>
        /// Gets the NorthWest gradient operator
        /// </summary>
        public abstract DenseMatrix<float> NorthWest { get; }

        /// <summary>
        /// Gets the West gradient operator
        /// </summary>
        public abstract DenseMatrix<float> West { get; }

        /// <summary>
        /// Gets the SouthWest gradient operator
        /// </summary>
        public abstract DenseMatrix<float> SouthWest { get; }

        /// <summary>
        /// Gets the South gradient operator
        /// </summary>
        public abstract DenseMatrix<float> South { get; }

        /// <summary>
        /// Gets the SouthEast gradient operator
        /// </summary>
        public abstract DenseMatrix<float> SouthEast { get; }

        /// <summary>
        /// Gets the East gradient operator
        /// </summary>
        public abstract DenseMatrix<float> East { get; }

        /// <summary>
        /// Gets the NorthEast gradient operator
        /// </summary>
        public abstract DenseMatrix<float> NorthEast { get; }

        /// <inheritdoc/>
        public bool Grayscale { get; }

        /// <inheritdoc/>
        protected override void BeforeFrameApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            if (this.Grayscale)
            {
                new GrayscaleBt709Processor<TPixel>(1F).Apply(source, sourceRectangle, configuration);
            }
        }

        /// <inheritdoc />
        protected override void OnFrameApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            DenseMatrix<float>[] kernels = { this.North, this.NorthWest, this.West, this.SouthWest, this.South, this.SouthEast, this.East, this.NorthEast };

            int startY = sourceRectangle.Y;
            int endY = sourceRectangle.Bottom;
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;

            // Align start/end positions.
            int minX = Math.Max(0, startX);
            int maxX = Math.Min(source.Width, endX);
            int minY = Math.Max(0, startY);
            int maxY = Math.Min(source.Height, endY);

            // we need a clean copy for each pass to start from
            using (ImageFrame<TPixel> cleanCopy = source.Clone())
            {
                new ConvolutionProcessor<TPixel>(kernels[0]).Apply(source, sourceRectangle, configuration);

                if (kernels.Length == 1)
                {
                    return;
                }

                int shiftY = startY;
                int shiftX = startX;

                // Reset offset if necessary.
                if (minX > 0)
                {
                    shiftX = 0;
                }

                if (minY > 0)
                {
                    shiftY = 0;
                }

                // Additional runs.
                // ReSharper disable once ForCanBeConvertedToForeach
                for (int i = 1; i < kernels.Length; i++)
                {
                    using (ImageFrame<TPixel> pass = cleanCopy.Clone())
                    {
                        new ConvolutionProcessor<TPixel>(kernels[i]).Apply(pass, sourceRectangle, configuration);

                        using (PixelAccessor<TPixel> passPixels = pass.Lock())
                        using (PixelAccessor<TPixel> targetPixels = source.Lock())
                        {
                            Parallel.For(
                                minY,
                                maxY,
                                configuration.ParallelOptions,
                                y =>
                                {
                                    int offsetY = y - shiftY;
                                    for (int x = minX; x < maxX; x++)
                                    {
                                        int offsetX = x - shiftX;

                                        // Grab the max components of the two pixels
                                        TPixel packed = default(TPixel);
                                        packed.PackFromVector4(Vector4.Max(passPixels[offsetX, offsetY].ToVector4(), targetPixels[offsetX, offsetY].ToVector4()));
                                        targetPixels[offsetX, offsetY] = packed;
                                    }
                                });
                        }
                    }
                }
            }
        }
    }
}