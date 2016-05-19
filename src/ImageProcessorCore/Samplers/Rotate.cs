// <copyright file="Rotate.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Numerics;

namespace ImageProcessorCore.Samplers
{
    using System.Threading.Tasks;

    /// <summary>
    /// Provides methods that allow the rotating of images.
    /// </summary>
    public class Rotate : ImageSampler
    {
        /// <summary>
        /// The angle of rotation.
        /// </summary>
        private float angle;

        /// <summary>
        /// Gets or sets the angle of rotation.
        /// </summary>
        public float Angle
        {
            get
            {
                return this.angle;
            }

            set
            {
                if (value > 360)
                {
                    value -= 360;
                }

                if (value < 0)
                {
                    value += 360;
                }

                this.angle = value;
            }
        }

        /// <summary>
        /// Gets or sets the center point.
        /// </summary>
        public Point Center { get; set; }

        /// <inheritdoc/>
        protected override void Apply(ImageBase target, ImageBase source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            int targetY = targetRectangle.Y;
            int targetBottom = targetRectangle.Bottom;
            int startX = targetRectangle.X;
            int endX = targetRectangle.Right;
            float negativeAngle = -this.angle;
            Point centre = this.Center == Point.Empty ? Rectangle.Center(sourceRectangle) : this.Center;

            // Scaling factors
            float widthFactor = source.Width / (float)target.Width;
            float heightFactor = source.Height / (float)target.Height;

            Matrix3x2 rotation = Point.CreateRotatation( centre, negativeAngle );

            Parallel.For(
                startY,
                endY,
                y =>
                {
                    if (y >= targetY && y < targetBottom)
                    {
                        // Y coordinates of source points
                        int originY = (int)((y - targetY) * heightFactor);

                        for (int x = startX; x < endX; x++)
                        {
                            // X coordinates of source points
                            int originX = (int)((x - startX) * widthFactor);

                            // Rotate at the centre point
                            Point rotated = Point.Rotate(new Point(originX, originY), rotation);
                            if (sourceRectangle.Contains(rotated.X, rotated.Y))
                            {
                                target[x, y] = source[rotated.X, rotated.Y];
                            }
                        }
                        this.OnRowProcessed();
                    }
                });
        }
    }
}