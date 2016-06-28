// <copyright file="SkewProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    using System.Numerics;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides methods that allow the skewing of images.
    /// </summary>
    public class SkewProcessor : ImageSampler
    {
        /// <inheritdoc/>
        public override int Parallelism { get; set; } = 1;

        /// <summary>
        /// Gets or sets the angle of rotation along the x-axis in degrees.
        /// </summary>
        public float AngleX { get; set; }

        /// <summary>
        /// Gets or sets the angle of rotation along the y-axis in degrees.
        /// </summary>
        public float AngleY { get; set; }

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
                Point centre = this.Center;
                Matrix3x2 skew = Point.CreateSkew(centre, -this.AngleX, -this.AngleY);
                Matrix3x2 invertedSkew;
                Matrix3x2.Invert(skew, out invertedSkew);
                Rectangle bounds = ImageMaths.GetBoundingRectangle(source.Bounds, invertedSkew);
                target.SetPixels(bounds.Width, bounds.Height, new float[bounds.Width * bounds.Height * 4]);
            }
        }

        /// <inheritdoc/>
        protected override void Apply(ImageBase target, ImageBase source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            int height = target.Height;
            int startX = 0;
            int endX = target.Width;
            Point centre = this.Center;

            Matrix3x2 invertedSkew;
            Matrix3x2 skew = Point.CreateSkew(centre, -this.AngleX, -this.AngleY);
            Matrix3x2.Invert(skew, out invertedSkew);
            Vector2 rightTop = Vector2.Transform(new Vector2(source.Width, 0), invertedSkew);
            Vector2 leftBottom = Vector2.Transform(new Vector2(0, source.Height), invertedSkew);

            if (this.AngleX < 0 && this.AngleY > 0)
            {
                skew = Point.CreateSkew(new Point((int)-leftBottom.X, (int)leftBottom.Y), -this.AngleX, -this.AngleY);
            }

            if (this.AngleX > 0 && this.AngleY < 0)
            {
                skew = Point.CreateSkew(new Point((int)rightTop.X, (int)-rightTop.Y), -this.AngleX, -this.AngleY);
            }

            if (this.AngleX < 0 && this.AngleY < 0)
            {
                skew = Point.CreateSkew(new Point(target.Width - 1, target.Height - 1), -this.AngleX, -this.AngleY);
            }

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
                            Point skewed = Point.Skew(new Point(x, y), skew);
                            if (source.Bounds.Contains(skewed.X, skewed.Y))
                            {
                                targetPixels[x, y] = sourcePixels[skewed.X, skewed.Y];
                            }
                        }
                        this.OnRowProcessed();
                    });
            }
        }
    }
}