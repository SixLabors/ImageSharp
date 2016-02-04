// <copyright file="Vignette.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Filters
{
    using System;
    using System.Numerics;
    using System.Threading.Tasks;

    /// <summary>
    /// Creates a vignette effect on the image
    /// </summary>
    public class Vignette : ParallelImageProcessor
    {
        /// <summary>
        /// Gets or sets the vignette color to apply.
        /// </summary>
        public Color Color { get; set; } = Color.Black;

        /// <summary>
        /// Gets or sets the the x-radius.
        /// </summary>
        public float RadiusX { get; set; }

        /// <summary>
        /// Gets or sets the the y-radius.
        /// </summary>
        public float RadiusY { get; set; }

        /// <inheritdoc/>
        protected override void Apply(ImageBase target, ImageBase source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;
            Color vignetteColor = this.Color;
            Vector2 centre = Rectangle.Center(targetRectangle).ToVector2();
            float rX = this.RadiusX > 0 ? this.RadiusX : targetRectangle.Width / 2f;
            float rY = this.RadiusY > 0 ? this.RadiusY : targetRectangle.Height / 2f;
            float maxDistance = (float)Math.Sqrt(rX * rX + rY * rY);

            Parallel.For(
                startY,
                endY,
                y =>
                    {
                        for (int x = startX; x < endX; x++)
                        {
                            float distance = Vector2.Distance(centre, new Vector2(x, y));
                            Color sourceColor = target[x, y];
                            target[x, y] = Color.Lerp(vignetteColor, sourceColor, 1 - .9f * distance / maxDistance);
                        }
                        this.OnRowProcessed();
                    });
        }
    }
}

