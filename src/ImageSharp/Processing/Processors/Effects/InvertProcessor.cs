// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors
{
    /// <summary>
    /// An <see cref="IImageProcessor{TPixel}"/> to invert the colors of an <see cref="Image{TPixel}"/>.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class InvertProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <inheritdoc/>
        protected override void OnApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            int startY = sourceRectangle.Y;
            int endY = sourceRectangle.Bottom;
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;
            Vector3 inverseVector = Vector3.One;

            // Align start/end positions.
            int minX = Math.Max(0, startX);
            int maxX = Math.Min(source.Width, endX);
            int minY = Math.Max(0, startY);
            int maxY = Math.Min(source.Height, endY);

            // Reset offset if necessary.
            if (minX > 0)
            {
                startX = 0;
            }

            if (minY > 0)
            {
                startY = 0;
            }

            Parallel.For(
                minY,
                maxY,
                configuration.ParallelOptions,
                y =>
                    {
                        Span<TPixel> row = source.GetPixelRowSpan(y - startY);

                        for (int x = minX; x < maxX; x++)
                        {
                            ref TPixel pixel = ref row[x - startX];

                            var vector = pixel.ToVector4();
                            Vector3 vector3 = inverseVector - new Vector3(vector.X, vector.Y, vector.Z);

                            pixel.PackFromVector4(new Vector4(vector3, vector.W));
                        }
                    });
        }
    }
}