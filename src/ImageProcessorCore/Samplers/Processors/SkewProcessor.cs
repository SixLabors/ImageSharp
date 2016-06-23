// <copyright file="SkewProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

using System;

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
            // If we are expanding we need to pad the bounds of the source rectangle.
            // We can use the resizer in nearest neighbor mode to do this fairly quickly.
            if (this.Expand)
            {
                // First find out how big the target rectangle should be.
                Point centre = this.Center == Point.Empty ? Rectangle.Center(sourceRectangle) : this.Center;
                Matrix3x2 skew = Point.CreateSkew(centre, -this.angleX, -this.angleY);
                Rectangle rectangle = ImageMaths.GetBoundingRectangle(sourceRectangle, skew);
                ResizeOptions options = new ResizeOptions
                {
                    Size = new Size(rectangle.Width, rectangle.Height),
                    Mode = ResizeMode.BoxPad
                };

                // Get the padded bounds and resize the image.
                Rectangle bounds = ResizeHelper.CalculateTargetLocationAndBounds(source, options);
                target.SetPixels(rectangle.Width, rectangle.Height, new float[rectangle.Width * rectangle.Height * 4]);
            }
        }

        /// <inheritdoc/>
        protected override void Apply(ImageBase target, ImageBase source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            int skewMaxX = target.Width - source.Width;
            int skewMaxY = target.Height - source.Height;

            bool revX = ImageMaths.DegreesToRadians(angleX) < 0;
            bool revY = ImageMaths.DegreesToRadians(angleY) < 0;
            Parallel.For(
              0,
              source.Height,
              sy =>
              {
                  int deltaX;
                  if (revX)
                  {
                      deltaX = ((skewMaxX * (-sy + (source.Height - 1))) / (source.Height - 1));
                  }
                  else
                  {
                      deltaX = (((skewMaxX * sy)) / (source.Height - 1));
                  }

                  for (int sx = 0; sx < source.Width; sx++)
                  {
                      int deltaY;
                      if (revY)
                      {
                          deltaY = ((((skewMaxY * -sx ))+ (skewMaxY-1)) -(skewMaxY-1)) / (source.Width - 1) + skewMaxY;
                          //deltaY = (((skewMaxY * sx)) / (source.Height - 1));
                         // deltaY = -deltaY + skewMaxY;
                          //deltaY = 0;
                      }
                      else
                      {
                          deltaY = ((skewMaxY * sx) / (source.Width - 1));
                      }
                      target[deltaX + sx, sy + deltaY] = source[sx, sy];
                  }
                  this.OnRowProcessed();
              });
        }


        /// <inheritdoc/>
        protected override void AfterApply(ImageBase source, ImageBase target, Rectangle targetRectangle, Rectangle sourceRectangle)
        {
            // Cleanup.
        }
    }
}