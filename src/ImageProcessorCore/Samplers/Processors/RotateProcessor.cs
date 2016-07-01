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
        /// <summary>
        /// The image used for storing the first pass pixels.
        /// </summary>

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
            if (this.Expand)
            {
                // First find out how big the target rectangle should be.
                Point centre = Rectangle.Center(sourceRectangle);
                Matrix3x2 rotation = Point.CreateRotation(centre, -this.angle);
                Rectangle rectangle = ImageMaths.GetBoundingRectangle(sourceRectangle, rotation);
                target.SetPixels(rectangle.Width, rectangle.Height, new float[rectangle.Width * rectangle.Height * 4]);
            }
        }

        /// <inheritdoc/>
        protected override void Apply(ImageBase target, ImageBase source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {

            Matrix3x2 rotation = Point.CreateRotation(new Point(0,0), -this.angle);
            Matrix3x2 tran =Matrix3x2.CreateTranslation(-target.Width/2f, -target.Height/2f);
            rotation = tran* rotation;
            Matrix3x2 tran2 = Matrix3x2.CreateTranslation(source.Width / 2f, source.Height / 2f);
            rotation = rotation*tran2;


            Parallel.For(
                0,
                target.Height,
                y =>
                {
                    for (int x = 0; x < target.Width; x++)
                    {
                        // Rotate at the centre point
                        Point rotated = Point.Rotate(new Point(x, y), rotation);
                        if (source.Bounds.Contains(rotated.X, rotated.Y))
                        {
                            target[x, y] = source[rotated.X, rotated.Y];
                        }
                    }

                    this.OnRowProcessed();
                });
        }

        /// <inheritdoc/>
        protected override void AfterApply(ImageBase source, ImageBase target, Rectangle targetRectangle, Rectangle sourceRectangle)
        {
       
        }
    }
}