// <copyright file="Resize.cs" company="James South">
// Copyright © James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Samplers
{
    using System;

    /// <summary>
    /// Provides methods that allow the resizing of images using various resampling algorithms.
    /// </summary>
    public class Resize : ParallelImageProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Resize"/> class.
        /// </summary>
        /// <param name="sampler">
        /// The sampler to perform the resize operation.
        /// </param>
        public Resize(IResampler sampler)
        {
            Guard.NotNull(sampler, nameof(sampler));

            this.Sampler = sampler;
        }

        /// <summary>
        /// Gets the sampler to perform the resize operation.
        /// </summary>
        public IResampler Sampler { get; }

        /// <inheritdoc/>
        protected override void Apply(ImageBase target, ImageBase source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            int sourceWidth = source.Width;
            int sourceHeight = source.Height;

            int width = target.Width;
            int height = target.Height;

            int targetY = targetRectangle.Y;
            int startX = targetRectangle.X;
            int endX = targetRectangle.Right;
            int right = (int)(this.Sampler.Radius + .5);
            int left = -right;

            // Scaling factors
            double widthFactor = sourceWidth / (double)targetRectangle.Width;
            double heightFactor = sourceHeight / (double)targetRectangle.Height;

            // Width and height decreased by 1
            int maxHeight = sourceHeight - 1;
            int maxWidth = sourceWidth - 1;

            for (int y = startY; y < endY; y++)
            {
                if (y >= 0 && y < height)
                {
                    // Y coordinates of source points.
                    double originY = ((y - targetY) * heightFactor) - 0.5;
                    int originY1 = (int)originY;
                    double dy = originY - originY1;

                    // For each row.
                    for (int x = startX; x < endX; x++)
                    {
                        if (x >= 0 && x < width)
                        {
                            // X coordinates of source points.
                            double originX = ((x - startX) * widthFactor) - 0.5f;
                            int originX1 = (int)originX;
                            double dx = originX - originX1;

                            // Destination color components
                            double r = 0;
                            double g = 0;
                            double b = 0;
                            double a = 0;

                            for (int yy = left; yy < right; yy++)
                            {
                                // Get Y cooefficient
                                double kernel1 = this.Sampler.GetValue(dy - yy);

                                int originY2 = originY1 + yy;
                                if (originY2 < 0)
                                {
                                    originY2 = 0;
                                }

                                if (originY2 > maxHeight)
                                {
                                    originY2 = maxHeight;
                                }

                                for (int xx = left; xx < right; xx++)
                                {
                                    // Get X cooefficient
                                    double kernel2 = kernel1 * this.Sampler.GetValue(xx - dx);

                                    int originX2 = originX1 + xx;
                                    if (originX2 < 0)
                                    {
                                        originX2 = 0;
                                    }

                                    if (originX2 > maxWidth)
                                    {
                                        originX2 = maxWidth;
                                    }

                                    Bgra sourceColor = source[originX2, originY2];
                                    sourceColor = PixelOperations.ToLinear(sourceColor);

                                    r += kernel2 * sourceColor.R;
                                    g += kernel2 * sourceColor.G;
                                    b += kernel2 * sourceColor.B;
                                    a += kernel2 * sourceColor.A;
                                }
                            }

                            Bgra destinationColor = new Bgra(b.ToByte(), g.ToByte(), r.ToByte(), a.ToByte());
                            destinationColor = PixelOperations.ToSrgb(destinationColor);
                            target[x, y] = destinationColor;
                        }
                    }
                }
            }
        }
    }
}
