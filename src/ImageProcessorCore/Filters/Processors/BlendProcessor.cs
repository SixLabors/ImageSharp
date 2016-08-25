// <copyright file="BlendProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    using System;
    using System.Numerics;
    using System.Threading.Tasks;

    /// <summary>
    /// Combines two images together by blending the pixels.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public class BlendProcessor<TColor, TPacked> : ImageProcessor<TColor, TPacked>
        where TColor : IPackedVector<TPacked>
        where TPacked : struct
    {
        /// <summary>
        /// The image to blend.
        /// </summary>
        private readonly ImageBase<TColor, TPacked> blend;

        /// <summary>
        /// Initializes a new instance of the <see cref="BlendProcessor{T,TP}"/> class.
        /// </summary>
        /// <param name="image">
        /// The image to blend with the currently processing image. 
        /// Disposal of this image is the responsibility of the developer.
        /// </param>
        /// <param name="alpha">The opacity of the image to blend. Between 0 and 100.</param>
        public BlendProcessor(ImageBase<TColor, TPacked> image, int alpha = 100)
        {
            Guard.MustBeBetweenOrEqualTo(alpha, 0, 100, nameof(alpha));
            this.blend = image;
            this.Value = alpha;
        }

        /// <summary>
        /// Gets the alpha percentage value.
        /// </summary>
        public int Value { get; }

        /// <inheritdoc/>
        protected override void Apply(ImageBase<TColor, TPacked> target, ImageBase<TColor, TPacked> source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;
            Rectangle bounds = this.blend.Bounds;

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

            float alpha = this.Value / 100F;

            using (PixelAccessor<TColor, TPacked> toBlendPixels = this.blend.Lock())
            using (PixelAccessor<TColor, TPacked> sourcePixels = source.Lock())
            using (PixelAccessor<TColor, TPacked> targetPixels = target.Lock())
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
                                Vector4 color = sourcePixels[offsetX, offsetY].ToVector4();

                                if (bounds.Contains(offsetX, offsetY))
                                {
                                    Vector4 blendedColor = toBlendPixels[offsetX, offsetY].ToVector4();

                                    if (blendedColor.W > 0)
                                    {
                                        // Lerping colors is dependent on the alpha of the blended color
                                        color = Vector4.Lerp(color, blendedColor, alpha > 0 ? alpha : blendedColor.W);
                                    }
                                }

                                TColor packed = default(TColor);
                                packed.PackFromVector4(color);
                                targetPixels[offsetX, offsetY] = packed;
                            }

                            this.OnRowProcessed();
                        });
            }
        }
    }
}
