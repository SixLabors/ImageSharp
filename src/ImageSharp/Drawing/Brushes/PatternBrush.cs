// <copyright file="PatternBrush.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Brushes
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;
    using System.Threading.Tasks;
    using Processors;

    /// <summary>
    /// Provides an implementaion of a pattern brush for painting patterns.
    /// </summary>
    public partial class PatternBrush : PatternBrush<Color, uint>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PatternBrush"/> class.
        /// </summary>
        /// <param name="foreColor">Color of the fore.</param>
        /// <param name="backColor">Color of the back.</param>
        /// <param name="pattern">The pattern.</param>
        public PatternBrush(Color foreColor, Color backColor, bool[,] pattern)
            : base(foreColor, backColor, pattern)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PatternBrush"/> class.
        /// </summary>
        /// <param name="brush">The brush.</param>
        internal PatternBrush(PatternBrush<Color, uint> brush)
            : base(brush)
        {
        }
    }

    /// <summary>
    /// Provides an implementaion of a pattern brush for painting patterns.
    /// </summary>
    /// <typeparam name="TColor">The type of the color.</typeparam>
    /// <typeparam name="TPacked">The type of the packed.</typeparam>
    public partial class PatternBrush<TColor, TPacked> : IBrush<TColor, TPacked>
        where TColor : struct, IPackedPixel<TPacked>
        where TPacked : struct
    {
        private readonly TColor foreColor;
        private readonly TColor backColor;
        private readonly bool[,] pattern;

        /// <summary>
        /// Initializes a new instance of the <see cref="PatternBrush{TColor, TPacked}"/> class.
        /// </summary>
        /// <param name="foreColor">Color of the fore.</param>
        /// <param name="backColor">Color of the back.</param>
        /// <param name="pattern">The pattern.</param>
        public PatternBrush(TColor foreColor, TColor backColor, bool[,] pattern)
        {
            this.foreColor = foreColor;
            this.backColor = backColor;
            this.pattern = pattern;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PatternBrush{TColor, TPacked}"/> class.
        /// </summary>
        /// <param name="brush">The brush.</param>
        internal PatternBrush(PatternBrush<TColor, TPacked> brush)
            : this(brush.foreColor, brush.backColor, brush.pattern)
        {
        }

        /// <summary>
        /// Creates the applicator for this bursh.
        /// </summary>
        /// <param name="region">The region the brush will be applied to.</param>
        /// <returns>
        /// The brush applicator for this brush
        /// </returns>
        /// <remarks>
        /// The <paramref name="region" /> when being applied to things like shapes would ussually be the
        /// bounding box of the shape not necessarily the bounds of the whole image
        /// </remarks>
        public IBrushApplicator<TColor, TPacked> CreateApplicator(RectangleF region)
        {
            return new PatternBrushApplicator(this.foreColor, this.backColor, this.pattern);
        }

        private class PatternBrushApplicator : IBrushApplicator<TColor, TPacked>
        {
            private readonly int xLength;
            private readonly int yLength;
            private readonly bool[,] pattern;
            private readonly TColor backColor = default(TColor);
            private readonly TColor foreColor = default(TColor);

            /// <summary>
            /// Initializes a new instance of the <see cref="PatternBrushApplicator"/> class.
            /// </summary>
            /// <param name="foreColor">Color of the fore.</param>
            /// <param name="backColor">Color of the back.</param>
            /// <param name="pattern">The pattern.</param>
            public PatternBrushApplicator(TColor foreColor, TColor backColor, bool[,] pattern)
            {
                this.foreColor = foreColor;
                this.backColor = backColor;
                this.pattern = pattern;

                this.xLength = this.pattern.GetLength(0);
                this.yLength = this.pattern.GetLength(1);
            }

            /// <summary>
            /// Gets the color for a single pixel.
            /// </summary>
            /// <param name="point">The point.</param>
            /// <returns>
            /// The color
            /// </returns>
            public TColor GetColor(Vector2 point)
            {
                var x = (int)point.X % this.xLength;
                var y = (int)point.Y % this.yLength;

                if (this.pattern[x, y])
                {
                    return this.foreColor;
                }
                else
                {
                    return this.backColor;
                }
            }

            /// <summary>
            /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
            /// </summary>
            public void Dispose()
            {
                // noop
            }
        }
    }
}