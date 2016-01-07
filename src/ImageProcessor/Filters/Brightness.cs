// <copyright file="Brightness.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Filters
{
    using System;
    using System.Numerics;
    using System.Threading.Tasks;

    /// <summary>
    /// An <see cref="IImageProcessor"/> to change the brightness of an <see cref="Image"/>.
    /// </summary>
    public class Brightness : ParallelImageProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Brightness"/> class.
        /// </summary>
        /// <param name="brightness">The new brightness of the image. Must be between -100 and 100.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="brightness"/> is less than -100 or is greater than 100.
        /// </exception>
        public Brightness(int brightness)
        {
            Guard.MustBeBetweenOrEqualTo(brightness, -100, 100, nameof(brightness));
            this.Value = brightness;
        }

        /// <summary>
        /// Gets the brightness value.
        /// </summary>
        public int Value { get; }

        /// <inheritdoc/>
        protected override void Apply(ImageBase target, ImageBase source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            float brightness = this.Value / 100f;
            int sourceY = sourceRectangle.Y;
            int sourceBottom = sourceRectangle.Bottom;
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;

            Parallel.For(
                startY,
                endY,
                y =>
                    {
                        if (y >= sourceY && y < sourceBottom)
                        {
                            for (int x = startX; x < endX; x++)
                            {
                                Color color = Color.Expand(source[x, y]);

                                Vector3 vector3 = color.ToVector3();
                                vector3 += new Vector3(brightness);

                                target[x, y] = Color.Compress(new Color(vector3, color.A));
                            }
                        }
                    });
        }
    }
}
