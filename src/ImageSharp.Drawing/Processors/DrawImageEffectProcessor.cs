// <copyright file="DrawImageProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Processors
{
    using System;
    using System.Numerics;
    using System.Threading.Tasks;
    using ImageSharp.PixelFormats;
    using ImageSharp.Processing;

    /// <summary>
    /// Combines two images together by blending the pixels.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class DrawImageEffectProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DrawImageEffectProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="image">The image to blend with the currently processing image.</param>        
        /// <param name="size">The size to draw the blended image.</param>
        /// <param name="location">The location to draw the blended image.</param>
        /// <param name="pixelFunction">Pixel function effect to apply on every pixel</param>
        /// <param name="alpha">The opacity of the image to blend. Between 0 and 100.</param>
        public DrawImageEffectProcessor(Image<TPixel> image, Size size, Point location, Func<TPixel, TPixel, float, TPixel> pixelFunction, int alpha = 100)
        {
            Guard.MustBeBetweenOrEqualTo(alpha, 0, 100, nameof(alpha));
            this.Image = image;
            this.PixelFunction = pixelFunction;
            this.Size = size;
            this.Location = location;
            this.Alpha = alpha;
        }

        /// <summary>
        /// Gets the image to blend.
        /// </summary>
        public Image<TPixel> Image { get; private set; }

        /// <summary>
        /// Gets The function effect to apply on a per pixel basis
        /// </summary>
        public Func<TPixel, TPixel, float, TPixel> PixelFunction { get; private set; }

        /// <summary>
        /// Gets the alpha percentage value.
        /// </summary>
        public int Alpha { get; }

        /// <summary>
        /// Gets the size to draw the blended image.
        /// </summary>
        public Size Size { get; }

        /// <summary>
        /// Gets the location to draw the blended image.
        /// </summary>
        public Point Location { get; }

        /// <inheritdoc/>
        protected override void OnApply(ImageBase<TPixel> target, Rectangle sourceRectangle)
        {
            if (this.Image.Bounds.Size != this.Size)
            {
                // should Resize be moved to core?
                this.Image = this.Image.Resize(this.Size.Width, this.Size.Height);
            }

            // Align start/end positions.
            Rectangle bounds = this.Image.Bounds;
            int minX = Math.Max(this.Location.X, sourceRectangle.X);
            int maxX = Math.Min(this.Location.X + bounds.Width, sourceRectangle.Width);
            int minY = Math.Max(this.Location.Y, sourceRectangle.Y);
            int maxY = Math.Min(this.Location.Y + bounds.Height, sourceRectangle.Bottom);

            float alpha = this.Alpha / 100F;

            using (PixelAccessor<TPixel> sourcePixels = this.Image.Lock())
            using (PixelAccessor<TPixel> targetPixels = target.Lock())
            {
                Parallel.For(
                    minY,
                    maxY,
                    this.ParallelOptions,
                    y =>
                        {
                            for (int x = minX; x < maxX; x++)
                            {
                                TPixel targetColor = targetPixels[x, y];
                                TPixel sourceColor = sourcePixels[x - minX, y - minY];

                                targetPixels[x, y] = this.PixelFunction(targetColor, sourceColor, alpha);
                            }
                        });
            }
        }
    }
}