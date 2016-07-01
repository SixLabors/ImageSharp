// <copyright file="RotateProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    using System.Numerics;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides methods that allow the rotating of images.
    /// </summary>
    public class RotateProcessor : ImageSampler
    {
        private Matrix3x2 processMatrix;

        /// <summary>
        /// The angle of processMatrix in degrees.
        /// </summary>
        private float angle;

        /// <inheritdoc/>
        public override int Parallelism { get; set; } = 1;

        /// <summary>
        /// Gets or sets the angle of processMatrix in degrees.
        /// </summary>
        public float Angle
        {
            get
            {
                return this.angle;
            }

            set
            {
                this.angle = value;
            }
        }
        /// <summary>
        /// Gets or sets a value indicating whether to expand the canvas to fit the rotated image.
        /// </summary>
        public bool Expand { get; set; }

        /// <inheritdoc/>
        protected override void OnApply(ImageBase target, ImageBase source, Rectangle targetRectangle, Rectangle sourceRectangle)
        {
            processMatrix = Point.CreateRotation(new Point(0, 0), -this.angle);
            if (this.Expand)
            {
                processMatrix = Point.CreateRotation(new Point(0,0), -this.angle);
                ProcessMatrixHelper.CreateNewTarget(target, sourceRectangle,processMatrix);
            }
        }

        /// <inheritdoc/>
        protected override void Apply(ImageBase target, ImageBase source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            var apply = ProcessMatrixHelper.Matrix3X2(target, source,processMatrix);
            Parallel.For(
                0,
                target.Height,
                y =>
                {
                    ProcessMatrixHelper.DrawHorizontalData(target, source, y, apply);
                    OnRowProcessed();
                });
        }
    }
}