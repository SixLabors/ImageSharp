// <copyright file="Invert.cs" company="James South">
// Copyright (c) James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Filters
{
    using System.Threading.Tasks;

    /// <summary>
    /// An <see cref="IImageProcessor"/> to invert the colors of an <see cref="Image"/>.
    /// </summary>
    public class Invert : ParallelImageProcessor
    {
        /// <inheritdoc/>
        protected override void Apply(ImageBase target, ImageBase source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
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
                                // TODO: This doesn't work for gamma test images.
                                Color color = source[x, y];
                                color.R = 1 - color.R;
                                color.G = 1 - color.G;
                                color.B = 1 - color.B;
                                target[x, y] = color;
                            }
                        }
                    });
        }
    }
}
