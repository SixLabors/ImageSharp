// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Numerics;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Primitives;
using SixLabors.Memory;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Provides an implementation of a pattern brush for painting patterns.
    /// </summary>
    /// <remarks>
    /// The patterns that are used to create a custom pattern brush are made up of a repeating matrix of flags,
    /// where each flag denotes whether to draw the foreground color or the background color.
    /// so to create a new bool[,] with your flags
    /// <para>
    /// For example if you wanted to create a diagonal line that repeat every 4 pixels you would use a pattern like so
    /// 1000
    /// 0100
    /// 0010
    /// 0001
    /// </para>
    /// <para>
    /// or you want a horizontal stripe which is 3 pixels apart you would use a pattern like
    ///  1
    ///  0
    ///  0
    /// </para>
    /// </remarks>
    public class PatternBrush : IBrush
    {
        /// <summary>
        /// The pattern.
        /// </summary>
        private readonly DenseMatrix<Color> pattern;
        private readonly DenseMatrix<Vector4> patternVector;

        /// <summary>
        /// Initializes a new instance of the <see cref="PatternBrush"/> class.
        /// </summary>
        /// <param name="foreColor">Color of the fore.</param>
        /// <param name="backColor">Color of the back.</param>
        /// <param name="pattern">The pattern.</param>
        public PatternBrush(Color foreColor, Color backColor, bool[,] pattern)
            : this(foreColor, backColor, new DenseMatrix<bool>(pattern))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PatternBrush"/> class.
        /// </summary>
        /// <param name="foreColor">Color of the fore.</param>
        /// <param name="backColor">Color of the back.</param>
        /// <param name="pattern">The pattern.</param>
        internal PatternBrush(Color foreColor, Color backColor, in DenseMatrix<bool> pattern)
        {
            var foreColorVector = (Vector4)foreColor;
            var backColorVector = (Vector4)backColor;
            this.pattern = new DenseMatrix<Color>(pattern.Columns, pattern.Rows);
            this.patternVector = new DenseMatrix<Vector4>(pattern.Columns, pattern.Rows);
            for (int i = 0; i < pattern.Data.Length; i++)
            {
                if (pattern.Data[i])
                {
                    this.pattern.Data[i] = foreColor;
                    this.patternVector.Data[i] = foreColorVector;
                }
                else
                {
                    this.pattern.Data[i] = backColor;
                    this.patternVector.Data[i] = backColorVector;
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PatternBrush"/> class.
        /// </summary>
        /// <param name="brush">The brush.</param>
        internal PatternBrush(PatternBrush brush)
        {
            this.pattern = brush.pattern;
            this.patternVector = brush.patternVector;
        }

        /// <inheritdoc />
        public BrushApplicator<TPixel> CreateApplicator<TPixel>(
            Configuration configuration,
            GraphicsOptions options,
            ImageFrame<TPixel> source,
            RectangleF region)
            where TPixel : struct, IPixel<TPixel> =>
            new PatternBrushApplicator<TPixel>(
                configuration,
                options,
                source,
                this.pattern.ToPixelMatrix<TPixel>(configuration));

        /// <summary>
        /// The pattern brush applicator.
        /// </summary>
        private class PatternBrushApplicator<TPixel> : BrushApplicator<TPixel>
            where TPixel : struct, IPixel<TPixel>
        {
            /// <summary>
            /// The pattern.
            /// </summary>
            private readonly DenseMatrix<TPixel> pattern;

            /// <summary>
            /// Initializes a new instance of the <see cref="PatternBrushApplicator{TPixel}" /> class.
            /// </summary>
            /// <param name="configuration">The configuration instance to use when performing operations.</param>
            /// <param name="options">The graphics options.</param>
            /// <param name="source">The source image.</param>
            /// <param name="pattern">The pattern.</param>
            public PatternBrushApplicator(
                Configuration configuration,
                GraphicsOptions options,
                ImageFrame<TPixel> source,
                in DenseMatrix<TPixel> pattern)
                : base(configuration, options, source)
            {
                this.pattern = pattern;
            }

            /// <inheritdoc/>
            internal override TPixel this[int x, int y]
            {
                get
                {
                    x %= this.pattern.Columns;
                    y %= this.pattern.Rows;

                    // 2d array index at row/column
                    return this.pattern[y, x];
                }
            }

            /// <inheritdoc />
            internal override void Apply(Span<float> scanline, int x, int y)
            {
                int patternY = y % this.pattern.Rows;
                MemoryAllocator memoryAllocator = this.Target.MemoryAllocator;

                using (IMemoryOwner<float> amountBuffer = memoryAllocator.Allocate<float>(scanline.Length))
                using (IMemoryOwner<TPixel> overlay = memoryAllocator.Allocate<TPixel>(scanline.Length))
                {
                    Span<float> amountSpan = amountBuffer.Memory.Span;
                    Span<TPixel> overlaySpan = overlay.Memory.Span;

                    for (int i = 0; i < scanline.Length; i++)
                    {
                        amountSpan[i] = NumberUtils.ClampFloat(scanline[i] * this.Options.BlendPercentage, 0, 1F);

                        int patternX = (x + i) % this.pattern.Columns;
                        overlaySpan[i] = this.pattern[patternY, patternX];
                    }

                    Span<TPixel> destinationRow = this.Target.GetPixelRowSpan(y).Slice(x, scanline.Length);
                    this.Blender.Blend(
                        this.Configuration,
                        destinationRow,
                        destinationRow,
                        overlaySpan,
                        amountSpan);
                }
            }
        }
    }
}
