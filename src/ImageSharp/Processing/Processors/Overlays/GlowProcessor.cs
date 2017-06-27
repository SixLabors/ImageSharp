// <copyright file="GlowProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;
    using System.Numerics;
    using System.Threading.Tasks;

    using ImageSharp.Memory;
    using ImageSharp.PixelFormats;
    using SixLabors.Primitives;

    /// <summary>
    /// An <see cref="IImageProcessor{TPixel}"/> that applies a radial glow effect an <see cref="Image{TPixel}"/>.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class GlowProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private readonly GraphicsOptions options;
        private readonly PixelBlender<TPixel> blender;

        /// <summary>
        /// Initializes a new instance of the <see cref="GlowProcessor{TPixel}" /> class.
        /// </summary>
        /// <param name="color">The color or the glow.</param>
        /// <param name="options">The options effecting blending and composition.</param>
        public GlowProcessor(TPixel color, GraphicsOptions options)
        {
            this.options = options;
            this.GlowColor = color;
            this.blender = PixelOperations<TPixel>.Instance.GetPixelBlender(this.options.BlenderMode);
        }

        /// <summary>
        /// Gets or sets the glow color to apply.
        /// </summary>
        public TPixel GlowColor { get; set; }

        /// <summary>
        /// Gets or sets the the radius.
        /// </summary>
        public float Radius { get; set; }

        /// <inheritdoc/>
        protected override void OnApply(ImageBase<TPixel> source, Rectangle sourceRectangle)
        {
            int startY = sourceRectangle.Y;
            int endY = sourceRectangle.Bottom;
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;
            TPixel glowColor = this.GlowColor;
            Vector2 centre = Rectangle.Center(sourceRectangle);
            float maxDistance = this.Radius > 0 ? MathF.Min(this.Radius, sourceRectangle.Width * .5F) : sourceRectangle.Width * .5F;

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
            using (var rowColors = new Buffer<TPixel>(width))
            {
                for (int i = 0; i < width; i++)
                {
                    rowColors[i] = glowColor;
                }

                Parallel.For(
                    minY,
                    maxY,
                    this.ParallelOptions,
                    y =>
                    {
                        using (var amounts = new Buffer<float>(width))
                        {
                            int offsetY = y - startY;
                            int offsetX = minX - startX;
                            for (int i = 0; i < width; i++)
                            {
                                float distance = Vector2.Distance(centre, new Vector2(i + offsetX, offsetY));
                                amounts[i] = (this.options.BlendPercentage * (1 - (.95F * (distance / maxDistance)))).Clamp(0, 1);
                            }

                            Span<TPixel> destination = source.GetRowSpan(offsetY).Slice(offsetX, width);

                            this.blender.Blend(destination, destination, rowColors, amounts);
                        }
                    });
            }
        }
    }
}