// <copyright file="Invert.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Filters
{
    using System.Numerics;
    using System.Threading.Tasks;

    /// <summary>
    /// An <see cref="IImageProcessorCore"/> to invert the colors of an <see cref="Image"/>.
    /// </summary>
    public class Invert : ParallelImageProcessorCore
    {
        /// <inheritdoc/>
        protected override void Apply(ImageBase target, ImageBase source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            int sourceY = sourceRectangle.Y;
            int sourceBottom = sourceRectangle.Bottom;
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;
            Vector3 inverseVector = Vector3.One;

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
                                Vector3 vector = inverseVector - color.ToVector3();
                                target[x, y] = new Color(vector, color.A);
                            }
                            this.OnRowProcessed();
                        }
                    });
        }
    }
}
