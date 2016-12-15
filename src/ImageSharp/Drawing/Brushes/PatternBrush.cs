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
    public class PatternBrush : PatternBrush<Color, uint>
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
        private readonly TColor[][] pattern;
        private readonly int stride;

        /// <summary>
        /// Initializes a new instance of the <see cref="PatternBrush{TColor, TPacked}"/> class.
        /// </summary>
        /// <param name="foreColor">Color of the fore.</param>
        /// <param name="backColor">Color of the back.</param>
        /// <param name="pattern">The pattern.</param>
        public PatternBrush(TColor foreColor, TColor backColor, bool[,] pattern)
        {
            this.stride = pattern.GetLength(1);

            // convert the multidimension array into a jagged one.
            var height = pattern.GetLength(0);
            this.pattern = new TColor[height][];
            for (var x = 0; x < height; x++)
            {
                this.pattern[x] = new TColor[stride];
                for (var y = 0; y < stride; y++)
                {
                    if (pattern[x, y])
                    {
                        this.pattern[x][y] = foreColor;
                    }else
                    {
                        this.pattern[x][y] = backColor;
                    }
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PatternBrush{TColor, TPacked}"/> class.
        /// </summary>
        /// <param name="brush">The brush.</param>
        internal PatternBrush(PatternBrush<TColor, TPacked> brush)
        {
            this.pattern = brush.pattern;
            this.stride = brush.stride;
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
            return new PatternBrushApplicator(this.pattern, this.stride);
        }

        private class PatternBrushApplicator : IBrushApplicator<TColor, TPacked>
        {
            private readonly int xLength;
            private readonly int stride;
            private readonly TColor[][] pattern;

            /// <summary>
            /// Initializes a new instance of the <see cref="PatternBrushApplicator" /> class.
            /// </summary>
            /// <param name="foreColor">Color of the fore.</param>
            /// <param name="backColor">Color of the back.</param>
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
            public TColor GetColor(Vector2 point)
            {
                var x = (int)point.X % this.xLength;
                var y = (int)point.Y % this.stride;

                return this.pattern[x][y];
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