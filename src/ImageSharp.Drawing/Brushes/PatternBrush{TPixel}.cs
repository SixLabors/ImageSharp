// <copyright file="PatternBrush{TPixel}.cs" company="James Jackson-South">
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
    using SixLabors.Primitives;

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
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    public class PatternBrush<TPixel> : IBrush<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// The pattern.
        /// </summary>
        private readonly Fast2DArray<TPixel> pattern;
        private readonly Fast2DArray<Vector4> patternVector;

        /// <summary>
        /// Initializes a new instance of the <see cref="PatternBrush{TPixel}"/> class.
        /// </summary>
        /// <param name="foreColor">Color of the fore.</param>
        /// <param name="backColor">Color of the back.</param>
        /// <param name="pattern">The pattern.</param>
        public PatternBrush(TPixel foreColor, TPixel backColor, bool[,] pattern)
            : this(foreColor, backColor, new Fast2DArray<bool>(pattern))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PatternBrush{TPixel}"/> class.
        /// </summary>
        /// <param name="foreColor">Color of the fore.</param>
        /// <param name="backColor">Color of the back.</param>
        /// <param name="pattern">The pattern.</param>
        internal PatternBrush(TPixel foreColor, TPixel backColor, Fast2DArray<bool> pattern)
        {
            var foreColorVector = foreColor.ToVector4();
            var backColorVector = backColor.ToVector4();
            this.pattern = new Fast2DArray<TPixel>(pattern.Width, pattern.Height);
            this.patternVector = new Fast2DArray<Vector4>(pattern.Width, pattern.Height);
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
        /// Initializes a new instance of the <see cref="PatternBrush{TPixel}"/> class.
        /// </summary>
        /// <param name="brush">The brush.</param>
        internal PatternBrush(PatternBrush<TPixel> brush)
        {
            this.pattern = brush.pattern;
            this.patternVector = brush.patternVector;
        }

        /// <inheritdoc />
        public BrushApplicator<TPixel> CreateApplicator(ImageBase<TPixel> source, RectangleF region, GraphicsOptions options)
        {
            return new PatternBrushApplicator(source, this.pattern, this.patternVector, options);
        }

        /// <summary>
        /// The pattern brush applicator.
        /// </summary>
        private class PatternBrushApplicator : BrushApplicator<TPixel>
        {
            /// <summary>
            /// The pattern.
            /// </summary>
            private readonly Fast2DArray<TPixel> pattern;
            private readonly Fast2DArray<Vector4> patternVector;

            /// <summary>
            /// Initializes a new instance of the <see cref="PatternBrushApplicator" /> class.
            /// </summary>
            /// <param name="source">The source image.</param>
            /// <param name="pattern">The pattern.</param>
            /// <param name="patternVector">The patternVector.</param>
            /// <param name="options">The options</param>
            public PatternBrushApplicator(ImageBase<TPixel> source, Fast2DArray<TPixel> pattern, Fast2DArray<Vector4> patternVector, GraphicsOptions options)
                : base(source, options)
            {
                this.pattern = pattern;
                this.patternVector = patternVector;
            }

            /// <summary>
            /// Gets the color for a single pixel.
            /// </summary>#
            /// <param name="x">The x.</param>
            /// <param name="y">The y.</param>
            /// <returns>
            /// The Color.
            /// </returns>
            internal override TPixel this[int x, int y]
            {
                get
                {
                    x = x % this.pattern.Width;
                    y = y % this.pattern.Height;

                    // 2d array index at row/column
                    return this.pattern[y, x];
                }
            }

            /// <inheritdoc />
            public override void Dispose()
            {
                // noop
            }

            /// <inheritdoc />
            internal override void Apply(Span<float> scanline, int x, int y)
            {
                int patternY = y % this.pattern.Height;
                using (var amountBuffer = new Buffer<float>(scanline.Length))
                using (var overlay = new Buffer<TPixel>(scanline.Length))
                {
                    for (int i = 0; i < scanline.Length; i++)
                    {
                        amountBuffer[i] = (scanline[i] * this.Options.BlendPercentage).Clamp(0, 1);

                        int patternX = (x + i) % this.pattern.Width;
                        overlay[i] = this.pattern[patternY, patternX];
                    }

                    Span<TPixel> destinationRow = this.Target.GetRowSpan(x, y).Slice(0, scanline.Length);
                    this.Blender.Blend(destinationRow, destinationRow, overlay, amountBuffer);
                }
            }
        }
    }
}