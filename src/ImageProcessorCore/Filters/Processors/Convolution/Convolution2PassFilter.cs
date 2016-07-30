// <copyright file="Convolution2PassFilter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    using System.Numerics;
    using System.Threading.Tasks;

    /// <summary>
    /// Defines a filter that uses two one-dimensional matrices to perform two-pass convolution against an image.
    /// </summary>
    /// <typeparam name="T">The pixel format.</typeparam>
    /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
    public abstract class Convolution2PassFilter<T, TP> : ImageProcessor<T, TP>
        where T : IPackedVector<TP>
        where TP : struct
    {
        /// <summary>
        /// Gets the horizontal gradient operator.
        /// </summary>
        public abstract float[,] KernelX { get; }

        /// <summary>
        /// Gets the vertical gradient operator.
        /// </summary>
        public abstract float[,] KernelY { get; }

        /// <inheritdoc/>
        protected override void Apply(ImageBase<T, TP> target, ImageBase<T, TP> source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            float[,] kernelX = this.KernelX;
            float[,] kernelY = this.KernelY;

            ImageBase<T, TP> firstPass = new Image<T, TP>(source.Width, source.Height);
            this.ApplyConvolution(firstPass, source, sourceRectangle, startY, endY, kernelX);
            this.ApplyConvolution(target, firstPass, sourceRectangle, startY, endY, kernelY);
        }

        /// <summary>
        /// Applies the process to the specified portion of the specified <see cref="ImageBase{T,TP}"/> at the specified location
        /// and with the specified size.
        /// </summary>
        /// <param name="target">Target image to apply the process to.</param>
        /// <param name="source">The source image. Cannot be null.</param>
        /// <param name="sourceRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to draw.
        /// </param>
        /// <param name="startY">The index of the row within the source image to start processing.</param>
        /// <param name="endY">The index of the row within the source image to end processing.</param>
        /// <param name="kernel">The kernel operator.</param>
        private void ApplyConvolution(ImageBase<T, TP> target, ImageBase<T, TP> source, Rectangle sourceRectangle, int startY, int endY, float[,] kernel)
        {
            int kernelHeight = kernel.GetLength(0);
            int kernelWidth = kernel.GetLength(1);
            int radiusY = kernelHeight >> 1;
            int radiusX = kernelWidth >> 1;

            int sourceBottom = sourceRectangle.Bottom;
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;
            int maxY = sourceBottom - 1;
            int maxX = endX - 1;

            using (IPixelAccessor<T, TP> sourcePixels = source.Lock())
            using (IPixelAccessor<T, TP> targetPixels = target.Lock())
            {
                Parallel.For(
                startY,
                endY,
                Bootstrapper.Instance.ParallelOptions,
                y =>
                {
                    for (int x = startX; x < endX; x++)
                    {
                        Vector4 destination = new Vector4();

                        // Apply each matrix multiplier to the color components for each pixel.
                        for (int fy = 0; fy < kernelHeight; fy++)
                        {
                            int fyr = fy - radiusY;
                            int offsetY = y + fyr;

                            offsetY = offsetY.Clamp(0, maxY);

                            for (int fx = 0; fx < kernelWidth; fx++)
                            {
                                int fxr = fx - radiusX;
                                int offsetX = x + fxr;

                                offsetX = offsetX.Clamp(0, maxX);

                                Vector4 currentColor = sourcePixels[offsetX, offsetY].ToVector4();
                                destination += kernel[fy, fx] * currentColor;
                            }
                        }

                        T packed = default(T);
                        packed.PackVector(destination);
                        targetPixels[x, y] = packed;
                    }

                    this.OnRowProcessed();
                });
            }
        }
    }
}