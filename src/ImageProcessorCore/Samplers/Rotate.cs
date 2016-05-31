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
                // First find out how big the target rectangle should be.
                Point centre = this.Center == Point.Empty ? Rectangle.Center(sourceRectangle) : this.Center;
                Matrix3x2 rotation = Point.CreateRotation(centre, -this.angle);
                Rectangle rectangle = ImageMaths.GetBoundingRectangle(sourceRectangle, rotation);
                ResizeOptions options = new ResizeOptions
                {
                    Size = new Size(rectangle.Width, rectangle.Height),
                    Mode = ResizeMode.BoxPad
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
            int height = this.firstPass.Height;
            int startX = 0;
            int endX = this.firstPass.Width;
            Point centre = this.Center == Point.Empty ? Rectangle.Center(this.firstPass.Bounds) : this.Center;
            Matrix3x2 rotation = Point.CreateRotation(centre, -this.angle);

            // Since we are not working in parallel we use full height and width 
            // of the first pass image.
            Parallel.For(
                0,
                height,
                y =>
                {
                    for (int x = startX; x < endX; x++)
                    {
                        // Rotate at the centre point
                        Point rotated = Point.Rotate(new Point(x, y), rotation);
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