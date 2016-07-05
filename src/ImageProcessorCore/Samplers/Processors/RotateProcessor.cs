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
    public class RotateProcessor : Matrix3x2Processor
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
        protected override void OnApply(ImageBase target, ImageBase source, Rectangle targetRectangle, Rectangle sourceRectangle)
        {
            processMatrix = Point.CreateRotation(new Point(0, 0), -this.Angle);
            if (this.Expand)
            {
                CreateNewTarget(target, sourceRectangle, processMatrix);
            }
        }

        /// <inheritdoc/>
        protected override void Apply(ImageBase target, ImageBase source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            Matrix3x2 matrix = GetCenteredMatrix(target, source, this.processMatrix);

            using (PixelAccessor sourcePixels = source.Lock())
            using (PixelAccessor targetPixels = target.Lock())
            {
                Parallel.For(
                    0,
                    target.Height,
                    y =>
                    {
                        for (int x = 0; x < target.Width; x++)
                        {
                            Point transformedPoint = Point.Rotate(new Point(x, y), matrix);
                            if (source.Bounds.Contains(transformedPoint.X, transformedPoint.Y))
                            {
                                targetPixels[x, y] = sourcePixels[transformedPoint.X, transformedPoint.Y];
                            }
                        }

                        OnRowProcessed();
                    });
            }
        }
    }
}