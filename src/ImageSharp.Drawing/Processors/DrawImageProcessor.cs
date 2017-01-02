// <copyright file="DrawImageProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Processors
{
    using System;
    using System.Numerics;
    using System.Threading.Tasks;

    using ImageSharp.Processing;

    /// <summary>
    /// Combines two images together by blending the pixels.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    public class DrawImageProcessor<TColor> : ImageProcessor<TColor>
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DrawImageProcessor{TColor}"/> class.
        /// </summary>
        /// <param name="image">The image to blend with the currently processing image.</param>
        /// <param name="size">The size to draw the blended image.</param>
        /// <param name="location">The location to draw the blended image.</param>
        /// <param name="alpha">The opacity of the image to blend. Between 0 and 100.</param>
        public DrawImageProcessor(Image<TColor> image, Size size, Point location, int alpha = 100)
        {
            Guard.MustBeBetweenOrEqualTo(alpha, 0, 100, nameof(alpha));
            this.Image = image;
            this.Size = size;
            this.Alpha = alpha;
            this.Location = location;
        }

        /// <summary>
        /// Gets the image to blend.
        /// </summary>
        public Image<TColor> Image { get; private set; }

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
        protected override void OnApply(ImageBase<TColor> source, Rectangle sourceRectangle)
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

            using (PixelAccessor<TColor> toBlendPixels = this.Image.Lock())
            using (PixelAccessor<TColor> sourcePixels = source.Lock())
            {
                Parallel.For(
                    minY,
                    maxY,
                    this.ParallelOptions,
                    y =>
                        {
                            for (int x = minX; x < maxX; x++)
                            {
                                Vector4 backgroundVector = sourcePixels[x, y].ToVector4();
                                Vector4 sourceVector = toBlendPixels[x - minX, y - minY].ToVector4();

                                // Lerping colors is dependent on the alpha of the blended color
                                backgroundVector = Vector4BlendTransforms.PremultipliedLerp(backgroundVector, sourceVector, alpha);

                                TColor packed = default(TColor);
                                packed.PackFromVector4(backgroundVector);
                                sourcePixels[x, y] = packed;
                            }
                        });
            }
        }
    }
}