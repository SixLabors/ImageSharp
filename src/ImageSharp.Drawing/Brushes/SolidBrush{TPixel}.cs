// <copyright file="SolidBrush{TPixel}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Brushes
{
    using System;
    using System.Numerics;

    using Processors;

    /// <summary>
    /// Provides an implementation of a solid brush for painting solid color areas.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    public class SolidBrush<TPixel> : IBrush<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// The color to paint.
        /// </summary>
        private readonly TPixel color;

        /// <summary>
        /// Initializes a new instance of the <see cref="SolidBrush{TPixel}"/> class.
        /// </summary>
        /// <param name="color">The color.</param>
        public SolidBrush(TPixel color)
        {
            this.color = color;
        }

        /// <summary>
        /// Gets the color.
        /// </summary>
        /// <value>
        /// The color.
        /// </value>
        public TPixel Color => this.color;

        /// <inheritdoc />
        public BrushApplicator<TPixel> CreateApplicator(PixelAccessor<TPixel> sourcePixels, RectangleF region)
        {
            return new SolidBrushApplicator(sourcePixels, this.color);
        }

        /// <summary>
        /// The solid brush applicator.
        /// </summary>
        private class SolidBrushApplicator : BrushApplicator<TPixel>
        {
            /// <summary>
            /// The solid color.
            /// </summary>
            private readonly TPixel color;
            private readonly Vector4 colorVector;

            /// <summary>
            /// Initializes a new instance of the <see cref="SolidBrushApplicator"/> class.
            /// </summary>
            /// <param name="color">The color.</param>
            /// <param name="sourcePixels">The sourcePixels.</param>
            public SolidBrushApplicator(PixelAccessor<TPixel> sourcePixels, TPixel color)
                : base(sourcePixels)
            {
                this.color = color;
                this.colorVector = color.ToVector4();
            }

            /// <summary>
            /// Gets the color for a single pixel.
            /// </summary>
            /// <param name="x">The x.</param>
            /// <param name="y">The y.</param>
            /// <returns>
            /// The color
            /// </returns>
            internal override TPixel this[int x, int y] => this.color;

            /// <inheritdoc />
            public override void Dispose()
            {
                // noop
            }

            /// <inheritdoc />
            internal override void Apply(float[] scanlineBuffer, int scanlineWidth, int offset, int x, int y)
            {
                Guard.MustBeGreaterThanOrEqualTo(scanlineBuffer.Length, offset + scanlineWidth, nameof(scanlineWidth));

                using (Buffer<float> buffer = new Buffer<float>(scanlineBuffer))
                {
                    BufferSpan<float> slice = buffer.Slice(offset);

                    for (int xPos = 0; xPos < scanlineWidth; xPos++)
                    {
                        int targetX = xPos + x;
                        int targetY = y;

                        float opacity = slice[xPos];
                        if (opacity > Constants.Epsilon)
                        {
                            Vector4 backgroundVector = this.Target[targetX, targetY].ToVector4();
                            Vector4 sourceVector = this.colorVector;

                            Vector4 finalColor = Vector4BlendTransforms.PremultipliedLerp(backgroundVector, sourceVector, opacity);

                            TPixel packed = default(TPixel);
                            packed.PackFromVector4(finalColor);
                            this.Target[targetX, targetY] = packed;
                        }
                    }
                }
            }
        }
    }
}