// <copyright file="VignetteProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;
    using System.Numerics;
    using System.Threading.Tasks;

    using ImageSharp.PixelFormats;

    /// <summary>
    /// An <see cref="IImageProcessor{TPixel}"/> that applies a radial vignette effect to an <see cref="Image{TPixel}"/>.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class VignetteProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="VignetteProcessor{TPixel}" /> class.
        /// </summary>
        /// <param name="color">The color of the vignette.</param>
        public VignetteProcessor(TPixel color)
        {
            this.VignetteColor = color;
        }

        /// <summary>
        /// Gets or sets the vignette color to apply.
        /// </summary>
        public TPixel VignetteColor { get; set; }

        /// <summary>
        /// Gets or sets the the x-radius.
        /// </summary>
        public float RadiusX { get; set; }

        /// <summary>
        /// Gets or sets the the y-radius.
        /// </summary>
        public float RadiusY { get; set; }

        /// <inheritdoc/>
        protected override void OnApply(ImageBase<TPixel> source, Rectangle sourceRectangle)
        {
            int startY = sourceRectangle.Y;
            int endY = sourceRectangle.Bottom;
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;
            TPixel vignetteColor = this.VignetteColor;
            Vector2 centre = Rectangle.Center(sourceRectangle).ToVector2();
            float rX = this.RadiusX > 0 ? MathF.Min(this.RadiusX, sourceRectangle.Width * .5F) : sourceRectangle.Width * .5F;
            float rY = this.RadiusY > 0 ? MathF.Min(this.RadiusY, sourceRectangle.Height * .5F) : sourceRectangle.Height * .5F;
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

            using (PixelAccessor<TPixel> sourcePixels = source.Lock())
            {
                Parallel.For(
                    minY,
                    maxY,
                    this.ParallelOptions,
                    y =>
                        {
                            int offsetY = y - startY;
                            for (int x = minX; x < maxX; x++)
                            {
                                int offsetX = x - startX;
                                float distance = Vector2.Distance(centre, new Vector2(offsetX, offsetY));
                                Vector4 sourceColor = sourcePixels[offsetX, offsetY].ToVector4();
                                TPixel packed = default(TPixel);
                                packed.PackFromVector4(Vector4BlendTransforms.PremultipliedLerp(sourceColor, vignetteColor.ToVector4(), .9F * (distance / maxDistance)));
                                sourcePixels[offsetX, offsetY] = packed;
                            }
                        });
            }
        }
    }
}