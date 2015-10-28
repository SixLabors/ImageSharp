// <copyright file="Contrast.cs" company="James South">
// Copyright (c) James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Filters
{
    using System;

    /// <summary>
    /// An <see cref="IImageProcessor"/> to change the contrast of an <see cref="Image"/>.
    /// </summary>
    public class Contrast : ParallelImageProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Contrast"/> class.
        /// </summary>
        /// <param name="contrast">The new contrast of the image. Must be between -100 and 100.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="contrast"/> is less than -100 or is greater than 100.
        /// </exception>
        public Contrast(int contrast)
        {
            Guard.MustBeBetweenOrEqualTo(contrast, -100, 100, nameof(contrast));
            this.Value = contrast;
        }

        /// <summary>
        /// Gets the contrast value.
        /// </summary>
        public int Value { get; }

        /// <inheritdoc/>
        protected override void Apply(ImageBase target, ImageBase source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            double contrast = (100.0 + this.Value) / 100.0;
            int sourceY = sourceRectangle.Y;
            int sourceBottom = sourceRectangle.Bottom;
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;

            for (int y = startY; y < endY; y++)
            {
                if (y >= sourceY && y < sourceBottom)
                {
                    for (int x = startX; x < endX; x++)
                    {
                        Bgra sourceColor = source[x, y];
                        sourceColor = PixelOperations.ToLinear(sourceColor);

                        double r = sourceColor.R / 255.0;
                        r -= 0.5;
                        r *= contrast;
                        r += 0.5;
                        r *= 255;
                        r = r.ToByte();

                        double g = sourceColor.G / 255.0;
                        g -= 0.5;
                        g *= contrast;
                        g += 0.5;
                        g *= 255;
                        g = g.ToByte();

                        double b = sourceColor.B / 255.0;
                        b -= 0.5;
                        b *= contrast;
                        b += 0.5;
                        b *= 255;
                        b = b.ToByte();

                        Bgra destinationColor = new Bgra(b.ToByte(), g.ToByte(), r.ToByte(), sourceColor.A);
                        destinationColor = PixelOperations.ToSrgb(destinationColor);
                        target[x, y] = destinationColor;
                    }
                }
            }
        }
    }
}
