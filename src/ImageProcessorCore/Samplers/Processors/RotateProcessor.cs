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
    public class RotateProcessor : ImageSampler
    {
        /// <inheritdoc/>
        public override int Parallelism { get; set; } = 1;

        /// <summary>
        /// Gets or sets the angle of rotation in degrees.
        /// </summary>
        public float Angle { get; set; }

        /// <summary>
        /// Gets or sets the center point.
        /// </summary>
        public Point Center { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to expand the canvas to fit the rotated image.
        /// </summary>
        public bool Expand { get; set; }

        /// <inheritdoc/>
        protected override void OnApply(ImageBase target, ImageBase source, Rectangle targetRectangle, Rectangle sourceRectangle)
        {
            if (this.Expand)
            {
                Point centre = this.Center == Point.Empty ? Rectangle.Center(sourceRectangle) : this.Center;
                Matrix3x2 rotation = Point.CreateRotation(centre, -this.Angle);
                Matrix3x2 invertedRotation;
                Matrix3x2.Invert(rotation, out invertedRotation);
                Rectangle bounds = ImageMaths.GetBoundingRectangle(source.Bounds, invertedRotation);
                target.SetPixels(bounds.Width, bounds.Height, new float[bounds.Width * bounds.Height * 4]);
            }
        }

        /// <inheritdoc/>
        protected override void Apply(ImageBase target, ImageBase source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            int height = target.Height;
            int startX = 0;
            int endX = target.Width;
            Point centre = this.Center == Point.Empty ? Rectangle.Center(target.Bounds) : this.Center;

            //Matrix3x2 invertedRotation;
            Matrix3x2 rotation = Point.CreateRotation(centre, -this.Angle);
            //Matrix3x2.Invert(rotation, out invertedRotation);
            //Vector2 rightTop = Vector2.Transform(new Vector2(source.Width, 0), invertedRotation);
            //Vector2 leftBottom = Vector2.Transform(new Vector2(0, source.Height), invertedRotation);

            //if (this.Angle < 0)
            //{
            //    rotation = Point.CreateRotation(new Point((int)-leftBottom.X, (int)leftBottom.Y), -this.Angle);
            //}

            //if (this.Angle > 0)
            //{
            //    rotation = Point.CreateRotation(new Point((int)rightTop.X, (int)-rightTop.Y), -this.Angle);
            //}

            // Since we are not working in parallel we use full height and width 
            // of the first pass image.
            using (PixelAccessor sourcePixels = source.Lock())
            using (PixelAccessor targetPixels = target.Lock())
            {
                Parallel.For(
                    0,
                    height,
                    y =>
                        {
                            for (int x = startX; x < endX; x++)
                            {
                                Point rotated = Point.Rotate(new Point(x, y), rotation);
                                if (source.Bounds.Contains(rotated.X, rotated.Y))
                                {
                                    targetPixels[x, y] = sourcePixels[rotated.X, rotated.Y];
                                }
                            }

                            this.OnRowProcessed();
                        });
            }
        }
    }
}