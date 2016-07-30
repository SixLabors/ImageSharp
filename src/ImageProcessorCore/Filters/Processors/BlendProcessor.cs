// <copyright file="BlendProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    using System.Numerics;
    using System.Threading.Tasks;

    /// <summary>
    /// Combines two images together by blending the pixels.
    /// </summary>
    /// <typeparam name="T">The pixel format.</typeparam>
    /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
    public class BlendProcessor<T, TP> : ImageProcessor<T, TP>
        where T : IPackedVector<TP>
        where TP : struct
    {
        /// <summary>
        /// The image to blend.
        /// </summary>
        private readonly ImageBase<T, TP> blend;

        /// <summary>
        /// Initializes a new instance of the <see cref="BlendProcessor"/> class.
        /// </summary>
        /// <param name="image">
        /// The image to blend with the currently processing image. 
        /// Disposal of this image is the responsibility of the developer.
        /// </param>
        /// <param name="alpha">The opacity of the image to blend. Between 0 and 100.</param>
        public BlendProcessor(ImageBase<T, TP> image, int alpha = 100)
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
        protected override void Apply(ImageBase<T, TP> target, ImageBase<T, TP> source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            int sourceY = sourceRectangle.Y;
            int sourceBottom = sourceRectangle.Bottom;
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;
            Rectangle bounds = this.blend.Bounds;
            float alpha = this.Value / 100f;

            using (IPixelAccessor<T, TP> toBlendPixels = this.blend.Lock())
            using (IPixelAccessor<T, TP> sourcePixels = source.Lock())
            using (IPixelAccessor<T, TP> targetPixels = target.Lock())
            {
                Parallel.For(
                    startY,
                    endY,
                    y =>
                        {
                            if (y >= sourceY && y < sourceBottom)
                            {
                                for (int x = startX; x < endX; x++)
                                {
                                    Vector4 color = sourcePixels[x, y].ToVector4();

                                    if (bounds.Contains(x, y))
                                    {
                                        Vector4 blendedColor = toBlendPixels[x, y].ToVector4();

                                        if (blendedColor.W > 0)
                                        {
                                            // Lerping colors is dependent on the alpha of the blended color
                                            float alphaFactor = alpha > 0 ? alpha : blendedColor.W;
                                            color = Vector4.Lerp(color, blendedColor, alphaFactor);
                                        }
                                    }

                                    T packed = default(T);
                                    packed.PackVector(color);
                                    targetPixels[x, y] = packed;
                                }

                                this.OnRowProcessed();
                            }
                        });
            }
        }
    }
}
