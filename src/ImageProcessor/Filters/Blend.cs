// <copyright file="Blend.cs" company="James South">
// Copyright (c) James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Filters
{
    using System.Threading.Tasks;

    /// <summary>
    /// Combines two images together by blending the pixels.
    /// </summary>
    public class Blend : ParallelImageProcessor
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

                                // Combining colors is dependent on the alpha of the blended color
                                float alphaFactor = alpha > 0 ? alpha : blendedColor.A;
                                float invertedAlphaFactor = 1 - alphaFactor;

                                color.R = (color.R * invertedAlphaFactor) + (blendedColor.R * alphaFactor);
                                color.G = (color.G * invertedAlphaFactor) + (blendedColor.G * alphaFactor);
                                color.B = (color.B * invertedAlphaFactor) + (blendedColor.B * alphaFactor);
                            }

                            target[x, y] = color;
                        }
                    }
                });
        }
    }
}
