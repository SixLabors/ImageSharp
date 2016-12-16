// <copyright file="SolidBrush`2.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Brushes
{
    using System.Numerics;

    using Processors;

    /// <summary>
    /// Provides an implementation of a solid brush for painting solid color areas.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public class SolidBrush<TColor, TPacked> : IBrush<TColor, TPacked>
        where TColor : struct, IPackedPixel<TPacked>
        where TPacked : struct
    {
        /// <summary>
        /// The color to paint.
        /// </summary>
        private readonly TColor color;

        /// <summary>
        /// Initializes a new instance of the <see cref="SolidBrush{TColor, TPacked}"/> class.
        /// </summary>
        /// <param name="color">The color.</param>
        public SolidBrush(TColor color)
        {
            this.color = color;
        }

        /// <summary>
        /// Gets the color.
        /// </summary>
        /// <value>
        /// The color.
        /// </value>
        public TColor Color => this.color;

        /// <inheritdoc />
        public IBrushApplicator<TColor, TPacked> CreateApplicator(RectangleF region)
        {
            return new SolidBrushApplicator(this.color);
        }

        /// <summary>
        /// The solid brush applicator.
        /// </summary>
        private class SolidBrushApplicator : IBrushApplicator<TColor, TPacked>
        {
            /// <summary>
            /// The solid color.
            /// </summary>
            private readonly TColor color;

            /// <summary>
            /// Initializes a new instance of the <see cref="SolidBrushApplicator"/> class.
            /// </summary>
            /// <param name="color">The color.</param>
            public SolidBrushApplicator(TColor color)
            {
                this.color = color;
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
                return this.color;
            }

            /// <inheritdoc />
            public void Dispose()
            {
                // noop
            }
        }
    }
}