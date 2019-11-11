// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;

using SixLabors.ImageSharp.Advanced;
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
        /// Initializes a new instance of the <see cref="SolidBrush"/> class.
        /// </summary>
        /// <param name="color">The color.</param>
        public SolidBrush(Color color)
        {
            this.Color = color;
        }

        /// <summary>
        /// Gets the color.
        /// </summary>
        public Color Color { get; }

        /// <inheritdoc />
        public BrushApplicator<TPixel> CreateApplicator<TPixel>(
            Configuration configuration,
            GraphicsOptions options,
            ImageFrame<TPixel> source,
            RectangleF region)
            where TPixel : struct, IPixel<TPixel>
        {
            return new SolidBrushApplicator<TPixel>(configuration, options, source, this.Color.ToPixel<TPixel>());
        }

        /// <summary>
        /// The solid brush applicator.
        /// </summary>
        private class SolidBrushApplicator<TPixel> : BrushApplicator<TPixel>
            where TPixel : struct, IPixel<TPixel>
        {
            private bool isDisposed;

            /// <summary>
            /// Initializes a new instance of the <see cref="SolidBrushApplicator{TPixel}"/> class.
            /// </summary>
            /// <param name="configuration">The configuration instance to use when performing operations.</param>
            /// <param name="options">The graphics options.</param>
            /// <param name="source">The source image.</param>
            /// <param name="color">The color.</param>
            public SolidBrushApplicator(
                Configuration configuration,
                GraphicsOptions options,
                ImageFrame<TPixel> source,
                TPixel color)
                : base(configuration, options, source)
            {
                this.Colors = source.MemoryAllocator.Allocate<TPixel>(source.Width);
                this.Colors.Memory.Span.Fill(color);
            }

            /// <summary>
            /// Gets the colors.
            /// </summary>
            protected IMemoryOwner<TPixel> Colors { get; private set; }

            /// <inheritdoc/>
            internal override TPixel this[int x, int y] => this.Colors.Memory.Span[x];

            /// <inheritdoc />
            protected override void Dispose(bool disposing)
            {
                if (this.isDisposed)
                {
                    return;
                }

                if (disposing)
                {
                    this.Colors.Dispose();
                }

                this.Colors = null;
                this.isDisposed = true;
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
                Configuration configuration = this.Configuration;

                if (this.Options.BlendPercentage == 1f)
                {
                    this.Blender.Blend(configuration, destinationRow, destinationRow, this.Colors.Memory.Span, scanline);
                }
                else
                {
                    using (IMemoryOwner<float> amountBuffer = memoryAllocator.Allocate<float>(scanline.Length))
                    {
                        Span<float> amountSpan = amountBuffer.Memory.Span;

                        for (int i = 0; i < scanline.Length; i++)
                        {
                            amountSpan[i] = scanline[i] * this.Options.BlendPercentage;
                        }

                        this.Blender.Blend(
                            configuration,
                            destinationRow,
                            destinationRow,
                            this.Colors.Memory.Span,
                            amountSpan);
                    }
                }
            }
        }
    }
}
