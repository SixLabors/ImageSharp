// <copyright file="Rotate.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Samplers
{
    using System.Numerics;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides methods that allow the rotating of images.
    /// </summary>
    public class Rotate : ImageSampler
    {
        /// <summary>
        /// The image used for storing the first pass pixels.
        /// </summary>
        private Image firstPass;

        /// <summary>
        /// The angle of rotation in degrees.
        /// </summary>
        private float angle;

        /// <inheritdoc/>
        public override int Parallelism { get; set; } = 1;

        /// <summary>
        /// Gets or sets the angle of rotation in degrees.
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

        /// <summary>
        /// Gets or sets a value indicating whether to expand the canvas to fit the rotated image.
        /// </summary>
        public bool Expand { get; set; }

        /// <inheritdoc/>
        protected override void OnApply(ImageBase source, ImageBase target, Rectangle targetRectangle, Rectangle sourceRectangle)
        {
            // If we are expanding we need to pad the bounds of the source rectangle.
            // We can use the resizer in nearest neighbor mode to do this fairly quickly.
            if (this.Expand)
            {
                // First find out how the target rectangle should be.
                Rectangle rectangle = ImageMaths.GetBoundingRotatedRectangle(source.Width, source.Height, -this.angle);
                Rectangle rectangle2 = ImageMaths.GetBoundingRotatedRectangle(sourceRectangle, -this.angle, this.Center);
                ResizeOptions options = new ResizeOptions
                {
                    Size = new Size(rectangle.Width, rectangle.Height),
                    Mode = ResizeMode.BoxPad,
                    Sampler = new NearestNeighborResampler()
                };

                // Get the padded bounds and resize the image.
                Rectangle bounds = ResizeHelper.CalculateTargetLocationAndBounds(source, options);
                this.firstPass = new Image(rectangle.Width, rectangle.Height);
                target.SetPixels(rectangle.Width, rectangle.Height, new float[rectangle.Width * rectangle.Height * 4]);
                new Resize(new NearestNeighborResampler()).Apply(this.firstPass, source, rectangle.Width, rectangle.Height, bounds, sourceRectangle);
            }
            else
            {
                // Just clone the pixels across.
                this.firstPass = new Image(source.Width, source.Height);
                this.firstPass.ClonePixels(source.Width, source.Height, source.Pixels);
            }
        }

        /// <inheritdoc/>
        protected override void Apply(ImageBase target, ImageBase source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            int targetY = this.firstPass.Bounds.Y;
            int targetHeight = this.firstPass.Height;
            int startX = this.firstPass.Bounds.X;
            int endX = this.firstPass.Bounds.Right;
            float negativeAngle = -this.angle;
            Point centre = this.Center == Point.Empty ? Rectangle.Center(this.firstPass.Bounds) : this.Center;
            Matrix3x2 rotation = Point.CreateRotation(centre, negativeAngle);

            // Since we are not working in parallel we use full height and width of the first pass image.
            Parallel.For(
                0,
                targetHeight,
                y =>
                {
                    // Y coordinates of source points
                    int originY = y - targetY;

                    for (int x = startX; x < endX; x++)
                    {
                        // X coordinates of source points
                        int originX = x - startX;

                        // Rotate at the centre point
                        Point rotated = Point.Rotate(new Point(originX, originY), rotation);
                        if (this.firstPass.Bounds.Contains(rotated.X, rotated.Y))
                        {
                            target[x, y] = this.firstPass[rotated.X, rotated.Y];
                        }
                    }

                    this.OnRowProcessed();
                });
        }

        /// <inheritdoc/>
        protected override void AfterApply(ImageBase source, ImageBase target, Rectangle targetRectangle, Rectangle sourceRectangle)
        {
            // Cleanup.
            this.firstPass.Dispose();
        }
    }
}