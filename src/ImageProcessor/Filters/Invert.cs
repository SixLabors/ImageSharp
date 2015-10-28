// <copyright file="Invert.cs" company="James South">
// Copyright (c) James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Filters
{
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

            for (int y = startY; y < endY; y++)
            {
                if (y >= sourceY && y < sourceBottom)
                {
                    for (int x = startX; x < endX; x++)
                    {
                        // TODO: This doesn't work for gamma test images.
                        Bgra color = source[x, y];
                        Bgra targetColor = new Bgra((255 - color.B).ToByte(), (255 - color.G).ToByte(), (255 - color.R).ToByte(), color.A);
                        target[x, y] = targetColor;
                    }
                }
            }
        }
    }
}
