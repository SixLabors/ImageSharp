// <copyright file="SkewProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

using System;
using System.Linq;

namespace ImageProcessorCore
{
    using System.Numerics;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides methods that allow the skewing of images.
    /// </summary>
    public class SkewProcessor : ImageSampler
    {
        /// <summary>
        /// The image used for storing the first pass pixels.
        /// </summary>
     //   private Image firstPass;

        /// <summary>
        /// The angle of rotation along the x-axis.
        /// </summary>
        private float angleX;

        /// <summary>
        /// The angle of rotation along the y-axis.
        /// </summary>
        private float angleY;

        /// <inheritdoc/>
        public override int Parallelism { get; set; } = 1;

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

        /// <summary>
        /// Gets or sets a value indicating whether to expand the canvas to fit the skewed image.
        /// </summary>
        public bool Expand { get; set; }

        /// <inheritdoc/>
        protected override void OnApply(ImageBase target, ImageBase source, Rectangle targetRectangle, Rectangle sourceRectangle)
        {
            if (this.Expand)
            {
                Point centre = new Point(0,0);
                Matrix3x2 skew = Point.CreateSkew(centre, -this.angleX, -angleY);
                Rectangle rect = ImageMaths.GetBoundingRectangle(source.Bounds, skew);
                target.SetPixels(rect.Width, rect.Height, new float[rect.Width*rect.Height*4]);
            }
        }

        /// <inheritdoc/>
        protected override void Apply(ImageBase target, ImageBase source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            int height = target.Height;
            int startX = 0;
            int endX = target.Width;
            Point centre;
            centre = new Point(0,0);
            Matrix3x2 skew = Point.CreateSkew(centre, -this.angleX, -this.angleY);
            Parallel.For(
                0,
                height,
                y =>
                {
                    for (int x = startX; x < endX; x++)
                    {
                        Point skewed = Point.Skew(new Point(x, y), skew);
                        if (source.Bounds.Contains(skewed.X, skewed.Y))
                        {
                            target[x, y] = source[skewed.X, skewed.Y];
                        }

                        else
                        {
                            // just for debugging.
                            Color c= source[Math.Abs(skewed.X%(source.Width-1)), Math.Abs(skewed.Y % (source.Height - 1))];
                            c.B = 0;
                            c.G = 0;
                            target[x, y]=c;
                        }
                    
                    }
                    this.OnRowProcessed();
                });
        }

    }
}