// <copyright file="EdgeDetectorCompassFilter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    using System;
    using System.Numerics;
    using System.Threading.Tasks;

    public class EdgeDetectorCompassFilter<TColor, TPacked> : ImageSampler<TColor, TPacked>, IEdgeDetectorFilter<TColor, TPacked>
        where TColor : IPackedVector<TPacked>
        where TPacked : struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EdgeDetectorCompassFilter{TColor,TPacked}"/> class.
        /// </summary>
        /// <param name="kernels">
        /// The collection of 2d gradient operator.
        /// </param>
        /// <param name="grayscale">Whether to convert the image to grayscale before performing edge detection..</param>
        public EdgeDetectorCompassFilter(float[][,] kernels, bool grayscale)
        {
            this.Kernels = kernels;
            this.Grayscale = grayscale;
        }

        /// <summary>
        /// Gets the collection of 2d gradient operators.
        /// </summary>
        public float[][,] Kernels { get; }

        /// <inheritdoc />
        public bool Grayscale { get; }

        /// <inheritdoc />
        public override void Apply(ImageBase<TColor, TPacked> target, ImageBase<TColor, TPacked> source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;

            // Align start/end positions.
            int minX = Math.Max(0, startX);
            int maxX = Math.Min(source.Width, endX);
            int minY = Math.Max(0, startY);
            int maxY = Math.Min(source.Height, endY);

            // Reset offset if necessary.
            if (minX > 0)
            {
                startX = 0;
            }

            if (minY > 0)
            {
                startY = 0;
            }


            new EdgeDetectorFilter<TColor, TPacked>(this.Kernels[0], this.Grayscale).Apply(target, source, sourceRectangle, targetRectangle, startY, endY);

            //this.ApplyConvolution(target, source, sourceRectangle, this.Grayscale, this.Kernels[0]);

            if (this.Kernels.Length == 1)
            {
                return;
            }

            for (int i = 1; i < this.Kernels.Length; i++)
            {
                ImageBase<TColor, TPacked> pass = new Image<TColor, TPacked>(source.Width, source.Height);
                new EdgeDetectorFilter<TColor, TPacked>(this.Kernels[0], false).Apply(pass, source, sourceRectangle, targetRectangle, startY, endY);


                // this.ApplyConvolution(pass, source, sourceRectangle, false, this.Kernels[0]);

                using (PixelAccessor<TColor, TPacked> passPixels = pass.Lock())
                using (PixelAccessor<TColor, TPacked> targetPixels = target.Lock())
                {
                    Parallel.For(
                        minY,
                        maxY,
                        this.ParallelOptions,
                        y =>
                        {
                            int offsetY = y - startY;
                            for (int x = minX; x < maxX; x++)
                            {
                                int offsetX = x - startX;
                                Vector4 passColor = passPixels[offsetX, offsetY].ToVector4();
                                Vector4 targetColor = targetPixels[offsetX, offsetY].ToVector4();

                                TColor packed = default(TColor);
                                packed.PackFromVector4(Vector4.Max(passColor, targetColor));
                                targetPixels[offsetX, offsetY] = packed;
                            }
                        });
                }
            }
        }

        /// <inheritdoc/>
        protected override void OnApply(ImageBase<TColor, TPacked> target, ImageBase<TColor, TPacked> source, Rectangle targetRectangle, Rectangle sourceRectangle)
        {
            if (this.Grayscale)
            {
                new GrayscaleBt709Processor<TColor, TPacked>().Apply(source, sourceRectangle);
            }
        }

        /// <summary>
        /// Applies the convolution process to the specified portion of the specified <see cref="ImageBase{TColor, TPacked}"/> at the specified location
        /// and with the specified size.
        /// </summary>
        /// <param name="target">Target image to apply the process to.</param>
        /// <param name="source">The source image. Cannot be null.</param>
        /// <param name="sourceRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to draw.
        /// </param>
        /// <param name="grayScale">Whether to convert the image to grayscale before performing edge detection.</param>
        /// <param name="kernel">The kernel operator.</param>
        private void ApplyConvolution(ImageBase<TColor, TPacked> target, ImageBase<TColor, TPacked> source, Rectangle sourceRectangle, bool grayScale, float[,] kernel)
        {
            new EdgeDetectorFilter<TColor, TPacked>(kernel, grayScale).Apply(target, source, sourceRectangle);
        }
    }
}
