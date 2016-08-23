// <copyright file="CompandingResizeProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    using System;
    using System.Numerics;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides methods that allow the resizing of images using various algorithms. 
    /// This version will expand and compress the image to and from a linear color space during processing.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public class CompandingResizeProcessor<TColor, TPacked> : ResamplingWeightedProcessor<TColor, TPacked>
        where TColor : IPackedVector<TPacked>
        where TPacked : struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CompandingResizeProcessor{TColor, TPacked}"/> class.
        /// </summary>
        /// <param name="sampler">
        /// The sampler to perform the resize operation.
        /// </param>
        public CompandingResizeProcessor(IResampler sampler)
            : base(sampler)
        {
        }

        /// <inheritdoc/>
        public override bool Compand { get; set; } = true;

        /// <inheritdoc/>
        protected override void Apply(ImageBase<TColor, TPacked> target, ImageBase<TColor, TPacked> source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            // Jump out, we'll deal with that later.
            if (source.Bounds == target.Bounds && sourceRectangle == targetRectangle)
            {
                return;
            }

            int width = target.Width;
            int height = target.Height;
            int sourceHeight = sourceRectangle.Height;
            int targetX = target.Bounds.X;
            int targetY = target.Bounds.Y;
            int targetRight = target.Bounds.Right;
            int targetBottom = target.Bounds.Bottom;
            int startX = targetRectangle.X;
            int endX = targetRectangle.Right;

            int minX = Math.Max(targetX, startX);
            int maxX = Math.Min(targetRight, endX);
            int minY = Math.Max(targetY, startY);
            int maxY = Math.Min(targetBottom, endY);

            if (this.Sampler is NearestNeighborResampler)
            {
                // Scaling factors
                float widthFactor = sourceRectangle.Width / (float)targetRectangle.Width;
                float heightFactor = sourceRectangle.Height / (float)targetRectangle.Height;

                using (PixelAccessor<TColor, TPacked> sourcePixels = source.Lock())
                using (PixelAccessor<TColor, TPacked> targetPixels = target.Lock())
                {
                    Parallel.For(
                        minY,
                        maxY,
                        this.ParallelOptions,
                        y =>
                        {
                            // Y coordinates of source points
                            int originY = (int)((y - startY) * heightFactor);

                            for (int x = minX; x < maxX; x++)
                            {
                                // X coordinates of source points
                                targetPixels[x, y] = sourcePixels[(int)((x - startX) * widthFactor), originY];
                            }

                            this.OnRowProcessed();
                        });
                }

                // Break out now.
                return;
            }

            // Interpolate the image using the calculated weights.
            // A 2-pass 1D algorithm appears to be faster than splitting a 1-pass 2D algorithm 
            // First process the columns. Since we are not using multiple threads startY and endY
            // are the upper and lower bounds of the source rectangle.
            Image<TColor, TPacked> firstPass = new Image<TColor, TPacked>(target.Width, source.Height);
            using (PixelAccessor<TColor, TPacked> sourcePixels = source.Lock())
            using (PixelAccessor<TColor, TPacked> firstPassPixels = firstPass.Lock())
            using (PixelAccessor<TColor, TPacked> targetPixels = target.Lock())
            {
                minX = Math.Max(0, startX);
                maxX = Math.Min(width, endX);
                minY = Math.Max(0, startY);
                maxY = Math.Min(height, endY);

                Parallel.For(
                    0,
                    sourceHeight,
                    this.ParallelOptions,
                    y =>
                    {
                        for (int x = minX; x < maxX; x++)
                        {
                            // Ensure offsets are normalised for cropping and padding.
                            Weight[] horizontalValues = this.HorizontalWeights[x - startX].Values;

                            // Destination color components
                            Vector4 destination = Vector4.Zero;

                            for (int i = 0; i < horizontalValues.Length; i++)
                            {
                                Weight xw = horizontalValues[i];
                                destination += sourcePixels[xw.Index, y].ToVector4().Expand() * xw.Value;
                            }

                            TColor d = default(TColor);
                            d.PackFromVector4(destination.Compress());
                            firstPassPixels[x, y] = d;
                        }
                    });

                // Now process the rows.
                Parallel.For(
                    minY,
                    maxY,
                    this.ParallelOptions,
                    y =>
                    {
                        // Ensure offsets are normalised for cropping and padding.
                        Weight[] verticalValues = this.VerticalWeights[y - startY].Values;

                        for (int x = 0; x < width; x++)
                        {
                            // Destination color components
                            Vector4 destination = Vector4.Zero;

                            for (int i = 0; i < verticalValues.Length; i++)
                            {
                                Weight yw = verticalValues[i];
                                destination += firstPassPixels[x, yw.Index].ToVector4().Expand() * yw.Value;
                            }

                            TColor d = default(TColor);
                            d.PackFromVector4(destination.Compress());
                            targetPixels[x, y] = d;
                        }

                        this.OnRowProcessed();
                    });

            }
        }
    }
}