// <copyright file="Skew.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Samplers
{
    using System.Threading.Tasks;

    /// <summary>
    /// Provides methods that allow the skewing of images.
    /// </summary>
    public class Skew : ImageSampler
    {
        /// <summary>
        /// The angle of rotation along the x-axis.
        /// </summary>
        private float angleX;

        /// <summary>
        /// The angle of rotation along the y-axis.
        /// </summary>
        private float angleY;

        /// <summary>
        /// Gets or sets the angle of rotation along the x-axis in degrees.
        /// </summary>
        public float AngleX
        {
            get
            {
                return this.angleX;
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

                this.angleX = value;
            }
        }

        /// <summary>
        /// Gets or sets the angle of rotation along the y-axis in degrees.
        /// </summary>
        public float AngleY
        {
            get
            {
                return this.angleY;
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

                this.angleY = value;
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
            float negativeAngleX = -this.angleX;
            float negativeAngleY = -this.angleY;
            Point centre = this.Center == Point.Empty ? Rectangle.Center(sourceRectangle) : this.Center;

            // Scaling factors
            float widthFactor = source.Width / (float)target.Width;
            float heightFactor = source.Height / (float)target.Height;

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

                            // Skew at the centre point
                            Point skewed = Point.Skew(new Point(originX, originY), centre, negativeAngleX, negativeAngleY);
                            if (sourceRectangle.Contains(skewed.X, skewed.Y))
                            {
                                target[x, y] = source[skewed.X, skewed.Y];
                            }
                        }
                        this.OnRowProcessed();
                    }
                });
        }
    }
}