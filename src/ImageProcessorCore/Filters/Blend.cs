// <copyright file="Blend.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Filters
{
    using System.Threading.Tasks;

    /// <summary>
    /// Combines two images together by blending the pixels.
    /// </summary>
    public class Blend : ParallelImageProcessorCore
    {
        /// <summary>
        /// The image to blend.
        /// </summary>
        private readonly ImageBase toBlend;

        /// <summary>
        /// Initializes a new instance of the <see cref="Blend"/> class.
        /// </summary>
        /// <param name="image">The image to blend.</param>
        /// <param name="alpha">The opacity of the image to blend. Between 0 and 100.</param>
        public Blend(ImageBase image, int alpha = 100)
        {
            Guard.MustBeBetweenOrEqualTo(alpha, 0, 100, nameof(alpha));
            this.toBlend = image;
            this.Value = alpha;
        }

        /// <summary>
        /// Gets the alpha percentage value.
        /// </summary>
        public int Value { get; }

        /// <inheritdoc/>
        protected override void Apply(ImageBase target, ImageBase source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            int sourceY = sourceRectangle.Y;
            int sourceBottom = sourceRectangle.Bottom;
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;
            Rectangle bounds = this.toBlend.Bounds;
            float alpha = this.Value / 100f;

            Parallel.For(
                startY,
                endY,
                y =>
                {
                    if (y >= sourceY && y < sourceBottom)
                    {
                        for (int x = startX; x < endX; x++)
                        {
                            Color color = source[x, y];

                            if (bounds.Contains(x, y))
                            {
                                Color blendedColor = this.toBlend[x, y];

                                if (blendedColor.A > 0)
                                {
                                    // Lerping colors is dependent on the alpha of the blended color
                                    float alphaFactor = alpha > 0 ? alpha : blendedColor.A;
                                    color = Color.Lerp(color, blendedColor, alphaFactor);
                                }
                            }

                            target[x, y] = color;
                        }
                        this.OnRowProcessed();
                    }
                });
        }
    }
}
