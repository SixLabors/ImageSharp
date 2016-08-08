// <copyright file="CompandingResizeProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    using System.Numerics;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides methods that allow the resizing of images using various algorithms. 
    /// This version will expand and compress the image to and from a linear color space during processing.
    /// </summary>
    /// <typeparam name="T">The pixel format.</typeparam>
    /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
    public class CompandingResizeProcessor<T, TP> : ResamplingWeightedProcessor<T, TP>
        where T : IPackedVector<TP>
        where TP : struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CompandingResizeProcessor{T,TP}"/> class.
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
        protected override void Apply(ImageBase<T, TP> target, ImageBase<T, TP> source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
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
            bool compand = this.Compand;

            if (this.Sampler is NearestNeighborResampler)
            {
                // Scaling factors
                float widthFactor = sourceRectangle.Width / (float)targetRectangle.Width;
                float heightFactor = sourceRectangle.Height / (float)targetRectangle.Height;

                using (IPixelAccessor<T, TP> sourcePixels = source.Lock())
                using (IPixelAccessor<T, TP> targetPixels = target.Lock())
                {
                    Parallel.For(
                        startY,
                        endY,
                        this.ParallelOptions,
                        y =>
                        {
                            if (targetY <= y && y < targetBottom)
                            {
                                // Y coordinates of source points
                                int originY = (int)((y - startY) * heightFactor);

                                for (int x = startX; x < endX; x++)
                                {
                                    if (targetX <= x && x < targetRight)
                                    {
                                        // X coordinates of source points
                                        targetPixels[x, y] = sourcePixels[(int)((x - startX) * widthFactor), originY];
                                    }
                                }

                                this.OnRowProcessed();
                            }
                        });
                }

                // Break out now.
                return;
            }

            // Interpolate the image using the calculated weights.
            // A 2-pass 1D algorithm appears to be faster than splitting a 1-pass 2D algorithm 
            // First process the columns. Since we are not using multiple threads startY and endY
            // are the upper and lower bounds of the source rectangle.
            Image<T, TP> firstPass = new Image<T, TP>(target.Width, source.Height);
            using (IPixelAccessor<T, TP> sourcePixels = source.Lock())
            using (IPixelAccessor<T, TP> firstPassPixels = firstPass.Lock())
            using (IPixelAccessor<T, TP> targetPixels = target.Lock())
            {
                Parallel.For(
                    0,
                    sourceHeight,
                    this.ParallelOptions,
                    y =>
                    {
                        for (int x = startX; x < endX; x++)
                        {
                            if (x >= 0 && x < width)
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

                                T d = default(T);
                                d.PackVector(destination.Compress());
                                firstPassPixels[x, y] = d;
                            }
                        }
                    });

                // Now process the rows.
                Parallel.For(
                    startY,
                    endY,
                    this.ParallelOptions,
                    y =>
                    {
                        if (y >= 0 && y < height)
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

                                T d = default(T);
                                d.PackVector(destination.Compress());
                                targetPixels[x, y] = d;
                            }
                        }

                        this.OnRowProcessed();
                    });

            }
        }
    }
}