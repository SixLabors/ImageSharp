// <copyright file="BackgroundColor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Filters
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Sets the background color of the image.
    /// </summary>
    public class BackgroundColor : ParallelImageProcessor
    {
        /// <summary>
        /// The epsilon for comparing floating point numbers.
        /// </summary>
        private const float Epsilon = 0.001f;

        /// <summary>
        /// Initializes a new instance of the <see cref="BackgroundColor"/> class.
        /// </summary>
        /// <param name="color">The <see cref="Color"/> to set the background color to.</param>
        public BackgroundColor(Color color)
        {
            this.Value = Color.FromNonPremultiplied(color);
        }

        /// <summary>
        /// Gets the background color value.
        /// </summary>
        public Color Value { get; }

        /// <inheritdoc/>
        protected override void Apply(ImageBase target, ImageBase source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            int sourceY = sourceRectangle.Y;
            int sourceBottom = sourceRectangle.Bottom;
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;
            Color backgroundColor = this.Value;

            Parallel.For(
                startY,
                endY,
                y =>
                {
                    if (y >= sourceY && y < sourceBottom)
                    {
                        for (int x = startX; x < endX; x++)
                        {
                            Color color = source[x, y];

                            if (color.A < 1 && color.A > 0)
                            {
                                color = Color.Lerp(color, backgroundColor, .5f);
                            }

                            if (Math.Abs(color.A) < Epsilon)
                            {
                                color = backgroundColor;
                            }

                            target[x, y] = color;
                        }
                    }
                });
        }
    }
}
