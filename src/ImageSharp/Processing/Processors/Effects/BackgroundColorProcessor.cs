// <copyright file="BackgroundColorProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;
    using System.Threading.Tasks;

    using ImageSharp.Memory;
    using ImageSharp.PixelFormats;
    using SixLabors.Primitives;

    /// <summary>
    /// Sets the background color of the image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class BackgroundColorProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private readonly GraphicsOptions options;

        /// <summary>
        /// Initializes a new instance of the <see cref="BackgroundColorProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="color">The <typeparamref name="TPixel"/> to set the background color to.</param>
        /// <param name="options">The options defining blending algorithum and amount.</param>
        public BackgroundColorProcessor(TPixel color, GraphicsOptions options)
        {
            this.Value = color;
            this.options = options;
        }

        /// <summary>
        /// Gets the background color value.
        /// </summary>
        public TPixel Value { get; }

        /// <inheritdoc/>
        protected override void OnApply(ImageBase<TPixel> source, Rectangle sourceRectangle)
        {
            int startY = sourceRectangle.Y;
            int endY = sourceRectangle.Bottom;
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;

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

            int width = maxX - minX;

            using (var colors = new Buffer<TPixel>(width))
            using (var amount = new Buffer<float>(width))
            {
                for (int i = 0; i < width; i++)
                {
                    colors[i] = this.Value;
                    amount[i] = this.options.BlendPercentage;
                }

                PixelBlender<TPixel> blender = PixelOperations<TPixel>.Instance.GetPixelBlender(this.options.BlenderMode);
                Parallel.For(
                    minY,
                    maxY,
                    this.ParallelOptions,
                    y =>
                    {
                        Span<TPixel> destination = source.GetRowSpan(y - startY).Slice(minX - startX, width);

                        // This switched color & destination in the 2nd and 3rd places because we are applying the target colour under the current one
                        blender.Blend(destination, colors, destination, amount);
                    });
            }
        }
    }
}