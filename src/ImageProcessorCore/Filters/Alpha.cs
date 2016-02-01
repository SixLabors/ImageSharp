// <copyright file="Alpha.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Filters
{
    using System;
    using System.Numerics;
    using System.Threading.Tasks;

    /// <summary>
    /// An <see cref="IImageProcessor"/> to change the Alpha of an <see cref="Image"/>.
    /// </summary>
    public class Alpha : ParallelImageProcessorCore
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Alpha"/> class.
        /// </summary>
        /// <param name="percent">The percentage to adjust the opacity of the image. Must be between 0 and 100.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="percent"/> is less than 0 or is greater than 100.
        /// </exception>
        public Alpha(int percent)
        {
            Guard.MustBeBetweenOrEqualTo(percent, 0, 100, nameof(percent));
            this.Value = percent;
        }

        /// <summary>
        /// Gets the alpha value.
        /// </summary>
        public int Value { get; }

        /// <inheritdoc/>
        protected override void Apply(ImageBase target, ImageBase source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            float alpha = this.Value / 100f;
            int sourceY = sourceRectangle.Y;
            int sourceBottom = sourceRectangle.Bottom;
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;
            Vector4 alphaVector = new Vector4(1, 1, 1, alpha);

            Parallel.For(
                startY,
                endY,
                y =>
                    {
                        if (y >= sourceY && y < sourceBottom)
                        {
                            for (int x = startX; x < endX; x++)
                            {
                                Vector4 color = Color.ToNonPremultiplied(source[x, y]).ToVector4();
                                color *= alphaVector;
                                target[x, y] = Color.FromNonPremultiplied(new Color(color));
                            }
                            this.OnRowProcessed();
                        }
                    });
        }
    }
}
