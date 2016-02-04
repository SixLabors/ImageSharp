// <copyright file="Contrast.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Filters
{
    using System;
    using System.Numerics;
    using System.Threading.Tasks;

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
            float contrast = (100f + this.Value) / 100f;
            int sourceY = sourceRectangle.Y;
            int sourceBottom = sourceRectangle.Bottom;
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;
            Vector4 contrastVector = new Vector4(contrast, contrast, contrast, 1);
            Vector4 shiftVector = new Vector4(.5f, .5f, .5f, 1);
            Parallel.For(
                startY,
                endY,
                y =>
                    {
                        if (y >= sourceY && y < sourceBottom)
                        {
                            for (int x = startX; x < endX; x++)
                            {
                                Vector4 color = Color.Expand(source[x, y]).ToVector4();
                                color -= shiftVector;
                                color *= contrastVector;
                                color += shiftVector;
                                target[x, y] = Color.Compress(new Color(color));
                            }
                            this.OnRowProcessed();
                        }
                    });
        }
    }
}
