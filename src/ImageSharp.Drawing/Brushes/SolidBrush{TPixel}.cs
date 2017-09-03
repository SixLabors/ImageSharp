// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Drawing.Brushes.Processors;
using SixLabors.ImageSharp.Drawing.Processors;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Drawing.Brushes
{
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
        public BrushApplicator<TPixel> CreateApplicator(ImageBase<TPixel> source, RectangleF region, GraphicsOptions options)
        {
            return new SolidBrushApplicator(source, this.color, options);
        }

        /// <summary>
        /// The solid brush applicator.
        /// </summary>
        private class SolidBrushApplicator : BrushApplicator<TPixel>
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="SolidBrushApplicator"/> class.
            /// </summary>
            /// <param name="source">The source image.</param>
            /// <param name="color">The color.</param>
            /// <param name="options">The options</param>
            public SolidBrushApplicator(ImageBase<TPixel> source, TPixel color, GraphicsOptions options)
                : base(source, options)
            {
                this.Colors = new Buffer<TPixel>(source.Width);
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
                try
                {
                    Span<TPixel> destinationRow = this.Target.GetPixelRowSpan(y).Slice(x, scanline.Length);

                    using (var amountBuffer = new Buffer<float>(scanline.Length))
                    {
                        for (int i = 0; i < scanline.Length; i++)
                        {
                            amountBuffer[i] = scanline[i] * this.Options.BlendPercentage;
                        }

                        this.Blender.Blend(destinationRow, destinationRow, this.Colors, amountBuffer);
                    }
                }
                catch (Exception)
                {
                    throw;
                }
            }
        }
    }
}