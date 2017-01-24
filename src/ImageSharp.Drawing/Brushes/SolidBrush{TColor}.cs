// <copyright file="SolidBrush{TColor}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Brushes
{
    using System;
    using System.Numerics;

    using Processors;

    /// <summary>
    /// Provides an implementation of a solid brush for painting solid color areas.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    public class SolidBrush<TColor> : IBrush<TColor>
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        /// <summary>
        /// The color to paint.
        /// </summary>
        private readonly TColor color;

        /// <summary>
        /// Initializes a new instance of the <see cref="SolidBrush{TColor}"/> class.
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
        public BrushApplicator<TColor> CreateApplicator(PixelAccessor<TColor> sourcePixels, RectangleF region)
        {
            return new SolidBrushApplicator(this.color);
        }

        /// <summary>
        /// The solid brush applicator.
        /// </summary>
        private class SolidBrushApplicator : BrushApplicator<TColor>
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
            public override TColor GetColor(Vector2 point)
            {
                return this.color;
            }

            /// <inheritdoc />
            public override void Dispose()
            {
                // noop
            }
        }
    }
}