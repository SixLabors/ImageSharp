// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Helpers;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors
{
    /// <summary>
    /// An <see cref="IImageProcessor{TPixel}"/> that applies a radial vignette effect to an <see cref="Image{TPixel}"/>.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class VignetteProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private readonly GraphicsOptions options;
        private readonly PixelBlender<TPixel> blender;

        /// <summary>
        /// Initializes a new instance of the <see cref="VignetteProcessor{TPixel}" /> class.
        /// </summary>
        /// <param name="color">The color of the vignette.</param>
        /// <param name="radiusX">The x-radius.</param>
        /// <param name="radiusY">The y-radius.</param>
        /// <param name="options">The options effecting blending and composition.</param>
        public VignetteProcessor(TPixel color, ValueSize radiusX, ValueSize radiusY, GraphicsOptions options)
        {
            this.VignetteColor = color;
            this.RadiusX = radiusX;
            this.RadiusY = radiusY;
            this.options = options;
            this.blender = PixelOperations<TPixel>.Instance.GetPixelBlender(this.options.BlenderMode);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="VignetteProcessor{TPixel}" /> class.
        /// </summary>
        /// <param name="color">The color of the vignette.</param>
        /// <param name="options">The options effecting blending and composition.</param>
        public VignetteProcessor(TPixel color,  GraphicsOptions options)
        {
            this.VignetteColor = color;
            this.options = options;
            this.blender = PixelOperations<TPixel>.Instance.GetPixelBlender(this.options.BlenderMode);
        }

        /// <summary>
        /// Gets the Graphics options to alter how processor is applied.
        /// </summary>
        public GraphicsOptions GraphicsOptions => this.options;

        /// <summary>
        /// Gets or sets the vignette color to apply.
        /// </summary>
        public TPixel VignetteColor { get; set; }

        /// <summary>
        /// Gets or sets the the x-radius.
        /// </summary>
        public ValueSize RadiusX { get; set; }

        /// <summary>
        /// Gets or sets the the y-radius.
        /// </summary>
        public ValueSize RadiusY { get; set; }

        /// <inheritdoc/>
        protected override void OnApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            int startY = sourceRectangle.Y;
            int endY = sourceRectangle.Bottom;
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;
            TPixel vignetteColor = this.VignetteColor;
            Vector2 centre = Rectangle.Center(sourceRectangle);

            var finalradiusX = this.RadiusX.Calculate(source.Size());
            var finalradiusY = this.RadiusY.Calculate(source.Size());
            float rX = finalradiusX > 0 ? MathF.Min(finalradiusX, sourceRectangle.Width * .5F) : sourceRectangle.Width * .5F;
            float rY = finalradiusY > 0 ? MathF.Min(finalradiusY, sourceRectangle.Height * .5F) : sourceRectangle.Height * .5F;
            float maxDistance = MathF.Sqrt((rX * rX) + (rY * rY));

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
                    rowColors[i] = vignetteColor;
                }

                Parallel.For(
                    minY,
                    maxY,
                    configuration.ParallelOptions,
                    y =>
                        {
                            using (var amounts = new Buffer<float>(width))
                            {
                                int offsetY = y - startY;
                                int offsetX = minX - startX;
                                for (int i = 0; i < width; i++)
                                {
                                    float distance = Vector2.Distance(centre, new Vector2(i + offsetX, offsetY));
                                    amounts[i] = (this.options.BlendPercentage * (.9F * (distance / maxDistance))).Clamp(0, 1);
                                }

                                Span<TPixel> destination = source.GetPixelRowSpan(offsetY).Slice(offsetX, width);

                                this.blender.Blend(destination, destination, rowColors, amounts);
                            }
                        });
            }
        }
    }
}