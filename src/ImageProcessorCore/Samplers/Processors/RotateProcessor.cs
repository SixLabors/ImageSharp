// <copyright file="RotateProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>
namespace ImageProcessorCore.Processors
{
    using System.Numerics;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides methods that allow the rotating of images.
    /// </summary>
    /// <typeparam name="T">The pixel format.</typeparam>
    /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
    public class RotateProcessor<T, TP> : Matrix3x2Processor<T, TP>
        where T : IPackedVector<TP>
        where TP : struct
    {
        /// <summary>
        /// The tranform matrix to apply.
        /// </summary>
        private Matrix3x2 processMatrix;

        /// <summary>
        /// Gets or sets the angle of processMatrix in degrees.
        /// </summary>
        public float Angle { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to expand the canvas to fit the rotated image.
        /// </summary>
        public bool Expand { get; set; } = true;

        /// <inheritdoc/>
        protected override void OnApply(ImageBase<T, TP> target, ImageBase<T, TP> source, Rectangle targetRectangle, Rectangle sourceRectangle)
        {
            this.processMatrix = Point.CreateRotation(new Point(0, 0), -this.Angle);
            if (this.Expand)
            {
                CreateNewTarget(target, sourceRectangle, this.processMatrix);
            }
        }

        /// <inheritdoc/>
        protected override void Apply(ImageBase<T, TP> target, ImageBase<T, TP> source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            int height = target.Height;
            int width = target.Width;
            Matrix3x2 matrix = GetCenteredMatrix(target, source, this.processMatrix);

            using (IPixelAccessor<T, TP> sourcePixels = source.Lock())
            using (IPixelAccessor<T, TP> targetPixels = target.Lock())
            {
                Parallel.For(
                    0,
                    height,
                    this.ParallelOptions,
                    y =>
                    {
                        for (int x = 0; x < width; x++)
                        {
                            Point transformedPoint = Point.Rotate(new Point(x, y), matrix);
                            if (source.Bounds.Contains(transformedPoint.X, transformedPoint.Y))
                            {
                                targetPixels[x, y] = sourcePixels[transformedPoint.X, transformedPoint.Y];
                            }
                        }

                        this.OnRowProcessed();
                    });
            }
        }
    }
}