// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Memory;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Provides an implementation of a solid brush for painting solid color areas.
    /// </summary>
    public class SolidBrush : IBrush
    {
        /// <summary>
        /// The color to paint.
        /// </summary>
        private readonly Color color;

        /// <summary>
        /// Initializes a new instance of the <see cref="SolidBrush"/> class.
        /// </summary>
        /// <param name="color">The color.</param>
        public SolidBrush(Color color)
        {
            this.color = color;
        }

        /// <summary>
        /// Gets the color.
        /// </summary>
        /// <value>
        /// The color.
        /// </value>
        public Color Color => this.color;

        /// <inheritdoc />
        public BrushApplicator<TPixel> CreateApplicator<TPixel>(ImageFrame<TPixel> source, RectangleF region, GraphicsOptions options)
            where TPixel : struct, IPixel<TPixel>
        {
            return new SolidBrushApplicator<TPixel>(source, this.color.ToPixel<TPixel>(), options);
        }

        /// <summary>
        /// The solid brush applicator.
        /// </summary>
        private class SolidBrushApplicator<TPixel> : BrushApplicator<TPixel>
            where TPixel : struct, IPixel<TPixel>
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="SolidBrushApplicator{TPixel}"/> class.
            /// </summary>
            /// <param name="source">The source image.</param>
            /// <param name="color">The color.</param>
            /// <param name="options">The options</param>
            public SolidBrushApplicator(ImageFrame<TPixel> source, TPixel color, GraphicsOptions options)
                : base(source, options)
            {
                this.Colors = source.MemoryAllocator.Allocate<TPixel>(source.Width);
                this.Colors.GetSpan().Fill(color);
            }

            /// <summary>
            /// Gets the colors.
            /// </summary>
            protected IMemoryOwner<TPixel> Colors { get; }

            /// <summary>
            /// Gets the color for a single pixel.
            /// </summary>
            /// <param name="x">The x.</param>
            /// <param name="y">The y.</param>
            /// <returns>
            /// The color
            /// </returns>
            internal override TPixel this[int x, int y] => this.Colors.GetSpan()[x];

            /// <inheritdoc />
            public override void Dispose()
            {
                this.Colors.Dispose();
            }

            /// <inheritdoc />
            internal override void Apply(Span<float> scanline, int x, int y)
            {
                Span<TPixel> destinationRow = this.Target.GetPixelRowSpan(y).Slice(x);

                // constrain the spans to each other
                if (destinationRow.Length > scanline.Length)
                {
                    destinationRow = destinationRow.Slice(0, scanline.Length);
                }
                else
                {
                    scanline = scanline.Slice(0, destinationRow.Length);
                }

                MemoryAllocator memoryAllocator = this.Target.MemoryAllocator;
                Configuration configuration = this.Target.Configuration;

                if (this.Options.BlendPercentage == 1f)
                {
                    this.Blender.Blend(configuration, destinationRow, destinationRow, this.Colors.GetSpan(), scanline);
                }
                else
                {
                    using (IMemoryOwner<float> amountBuffer = memoryAllocator.Allocate<float>(scanline.Length))
                    {
                        Span<float> amountSpan = amountBuffer.GetSpan();

                        for (int i = 0; i < scanline.Length; i++)
                        {
                            amountSpan[i] = scanline[i] * this.Options.BlendPercentage;
                        }

                        this.Blender.Blend(
                            configuration,
                            destinationRow,
                            destinationRow,
                            this.Colors.GetSpan(),
                            amountSpan);
                    }
                }
            }
        }
    }
}