// <copyright file="SolidBrush{TPixel}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Brushes
{
    using System;
    using System.Numerics;

    using ImageSharp.Memory;
    using ImageSharp.PixelFormats;
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
        public BrushApplicator<TPixel> CreateApplicator(PixelAccessor<TPixel> sourcePixels, RectangleF region, GraphicsOptions options)
        {
            return new SolidBrushApplicator(sourcePixels, this.color, options);
        }

        /// <summary>
        /// The solid brush applicator.
        /// </summary>
        private class SolidBrushApplicator : BrushApplicator<TPixel>
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="SolidBrushApplicator"/> class.
            /// </summary>
            /// <param name="color">The color.</param>
            /// <param name="options">The options</param>
            /// <param name="sourcePixels">The sourcePixels.</param>
            public SolidBrushApplicator(PixelAccessor<TPixel> sourcePixels, TPixel color, GraphicsOptions options)
                : base(sourcePixels, options)
            {
                this.Colors = new Buffer<TPixel>(sourcePixels.Width);
                for (int i = 0; i < this.Colors.Length; i++)
                {
                    this.Colors[i] = color;
                }
            }

            /// <summary>
            /// Gets the colors.
            /// </summary>
            protected Buffer<TPixel> Colors { get; }

            /// <summary>
            /// Gets the color for a single pixel.
            /// </summary>
            /// <param name="x">The x.</param>
            /// <param name="y">The y.</param>
            /// <returns>
            /// The color
            /// </returns>
            internal override TPixel this[int x, int y] => this.Colors[x];

            /// <inheritdoc />
            public override void Dispose()
            {
                this.Colors.Dispose();
            }

            /// <inheritdoc />
            internal override void Apply(Span<float> scanline, int x, int y)
            {
                Span<TPixel> destinationRow = this.Target.GetRowSpan(x, y).Slice(0, scanline.Length);

                using (Buffer<float> amountBuffer = new Buffer<float>(scanline.Length))
                {
                    for (int i = 0; i < scanline.Length; i++)
                    {
                        amountBuffer[i] = scanline[i] * this.Options.BlendPercentage;
                    }

                    this.Blender.Blend(destinationRow, destinationRow, this.Colors, amountBuffer);
                }
            }
        }
    }
}