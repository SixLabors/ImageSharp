// <copyright file="Pen.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Pens
{
    /// <summary>
    /// Represents a <see cref="Pen{TColor}"/> in the <see cref="Rgba32"/> color space.
    /// </summary>
    public class Pen : Pen<Rgba32>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Pen"/> class.
        /// </summary>
        /// <param name="color">The color.</param>
        /// <param name="width">The width.</param>
        public Pen(Rgba32 color, float width)
            : base(color, width)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Pen"/> class.
        /// </summary>
        /// <param name="brush">The brush.</param>
        /// <param name="width">The width.</param>
        public Pen(IBrush<Rgba32> brush, float width)
            : base(brush, width)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Pen"/> class.
        /// </summary>
        /// <param name="brush">The brush.</param>
        /// <param name="width">The width.</param>
        /// <param name="pattern">The pattern.</param>
        public Pen(IBrush<Rgba32> brush, float width, float[] pattern)
            : base(brush, width, pattern)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Pen"/> class.
        /// </summary>
        /// <param name="pen">The pen.</param>
        internal Pen(Pen<Rgba32> pen)
            : base(pen)
        {
        }
    }
}