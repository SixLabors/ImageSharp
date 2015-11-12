// <copyright file="Crop.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Samplers
{
    using System.Threading.Tasks;

    /// <summary>
    /// Provides methods to allow the cropping of an image.
    /// </summary>
    public class Crop : ParallelImageProcessor
    {
        /// <inheritdoc/>
        protected override void Apply(
            ImageBase target,
            ImageBase source,
            Rectangle targetRectangle,
            Rectangle sourceRectangle,
            int startY,
            int endY)
        {
            int targetY = targetRectangle.Y;
            int targetBottom = targetRectangle.Bottom;
            int startX = targetRectangle.X;
            int endX = targetRectangle.Right;

            Parallel.For(
            startY,
            endY,
            y =>
            {
                if (y >= targetY && y < targetBottom)
                {
                    for (int x = startX; x < endX; x++)
                    {
                        target[x, y] = source[x, y];
                    }
                }
            });
        }
    }
}
