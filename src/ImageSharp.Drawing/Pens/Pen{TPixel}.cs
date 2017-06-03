// <copyright file="Pen{TPixel}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Pens
{
    using System;
    using System.Numerics;

    using ImageSharp.Drawing.Brushes;
    using ImageSharp.PixelFormats;
    using Processors;

    /// <summary>
    /// Provides a pen that can apply a pattern to a line with a set brush and thickness
    /// </summary>
    /// <typeparam name="TPixel">The type of the color.</typeparam>
    /// <remarks>
    /// The pattern will be in to the form of new float[]{ 1f, 2f, 0.5f} this will be
    /// converted into a pattern that is 3.5 times longer that the width with 3 sections
    /// section 1 will be width long (making a square) and will be filled by the brush
    /// section 2 will be width * 2 long and will be empty
    /// section 3 will be width/2 long and will be filled
    /// the the pattern will imidiatly repeat without gap.
    /// </remarks>
    public class Pen<TPixel> : IPen<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private static readonly float[] EmptyPattern = new float[0];
        private readonly float[] pattern;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageSharp.Drawing.Pens.Pen{TPixel}"/> class.
        /// </summary>
        /// <param name="color">The color.</param>
        /// <param name="width">The width.</param>
        /// <param name="pattern">The pattern.</param>
        public Pen(TPixel color, float width, float[] pattern)
            : this(new SolidBrush<TPixel>(color), width, pattern)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageSharp.Drawing.Pens.Pen{TPixel}"/> class.
        /// </summary>
        /// <param name="brush">The brush.</param>
        /// <param name="width">The width.</param>
        /// <param name="pattern">The pattern.</param>
        public Pen(IBrush<TPixel> brush, float width, float[] pattern)
        {
            this.StrokeFill = brush;
            this.StrokeWidth = width;
            this.pattern = pattern;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageSharp.Drawing.Pens.Pen{TPixel}"/> class.
        /// </summary>
        /// <param name="color">The color.</param>
        /// <param name="width">The width.</param>
        public Pen(TPixel color, float width)
            : this(new SolidBrush<TPixel>(color), width)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageSharp.Drawing.Pens.Pen{TPixel}"/> class.
        /// </summary>
        /// <param name="brush">The brush.</param>
        /// <param name="width">The width.</param>
        public Pen(IBrush<TPixel> brush, float width)
            : this(brush, width, EmptyPattern)
        {
        }

        /// <inheritdoc/>
        public IBrush<TPixel> StrokeFill { get; }

        /// <inheritdoc/>
        public float StrokeWidth { get; }

        /// <inheritdoc/>
        public ReadOnlySpan<float> StrokePattern => this.pattern;
    }
}
