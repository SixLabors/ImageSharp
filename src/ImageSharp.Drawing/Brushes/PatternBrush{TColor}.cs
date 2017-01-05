// <copyright file="PatternBrush{TColor}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Brushes
{
    using System;
    using System.Numerics;

    using Processors;

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
    /// Warning when use array initializer across multiple lines the bools look inverted i.e.
    /// new bool[,]{
    ///     {true, false, false},
    ///     {false,true, false}
    /// }
    /// would be
    /// 10
    /// 01
    /// 00
    /// </remarks>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    public class PatternBrush<TColor> : IBrush<TColor>
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        /// <summary>
        /// The pattern.
        /// </summary>
        private readonly TColor[][] pattern;

        /// <summary>
        /// The stride width.
        /// </summary>
        private readonly int stride;

        /// <summary>
        /// Initializes a new instance of the <see cref="PatternBrush{TColor}"/> class.
        /// </summary>
        /// <param name="foreColor">Color of the fore.</param>
        /// <param name="backColor">Color of the back.</param>
        /// <param name="pattern">The pattern.</param>
        public PatternBrush(TColor foreColor, TColor backColor, bool[,] pattern)
        {
            this.stride = pattern.GetLength(1);

            // Convert the multidimension array into a jagged one.
            int height = pattern.GetLength(0);
            this.pattern = new TColor[height][];
            for (int x = 0; x < height; x++)
            {
                this.pattern[x] = new TColor[this.stride];
                for (int y = 0; y < this.stride; y++)
                {
                    if (pattern[x, y])
                    {
                        this.pattern[x][y] = foreColor;
                    }
                    else
                    {
                        this.pattern[x][y] = backColor;
                    }
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PatternBrush{TColor}"/> class.
        /// </summary>
        /// <param name="brush">The brush.</param>
        internal PatternBrush(PatternBrush<TColor> brush)
        {
            this.pattern = brush.pattern;
            this.stride = brush.stride;
        }

        /// <inheritdoc />
        public BrushApplicator<TColor> CreateApplicator(PixelAccessor<TColor> sourcePixels, RectangleF region)
        {
            return new PatternBrushApplicator(this.pattern, this.stride);
        }

        /// <summary>
        /// The pattern brush applicator.
        /// </summary>
        private class PatternBrushApplicator : BrushApplicator<TColor>
        {
            /// <summary>
            /// The patter x-length.
            /// </summary>
            private readonly int xLength;

            /// <summary>
            /// The stride width.
            /// </summary>
            private readonly int stride;

            /// <summary>
            /// The pattern.
            /// </summary>
            private readonly TColor[][] pattern;

            /// <summary>
            /// Initializes a new instance of the <see cref="PatternBrushApplicator" /> class.
            /// </summary>
            /// <param name="pattern">The pattern.</param>
            /// <param name="stride">The stride.</param>
            public PatternBrushApplicator(TColor[][] pattern, int stride)
            {
                this.pattern = pattern;
                this.xLength = pattern.Length;
                this.stride = stride;
            }

            /// <summary>
            /// Gets the color for a single pixel.
            /// </summary>
            /// <param name="point">The point.</param>
            /// <returns>
            /// The color
            /// </returns>
            public override TColor GetColor(Vector2 point)
            {
                int x = (int)point.X % this.xLength;
                int y = (int)point.Y % this.stride;

                return this.pattern[x][y];
            }

            /// <inheritdoc />
            public override void Dispose()
            {
                // noop
            }
        }
    }
}