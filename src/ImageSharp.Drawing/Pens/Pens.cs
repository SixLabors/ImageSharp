// <copyright file="Pens.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Pens
{
    /// <summary>
    /// Common Pen styles
    /// </summary>
    public class Pens
    {
        /// <summary>
        /// Create a solid pen with out any drawing patterns
        /// </summary>
        /// <param name="color">The color.</param>
        /// <param name="width">The width.</param>
        /// <returns>The Pen</returns>
        public static Pen Solid(Color color, float width) => new Pen(color, width);

        /// <summary>
        /// Create a solid pen with out any drawing patterns
        /// </summary>
        /// <param name="brush">The brush.</param>
        /// <param name="width">The width.</param>
        /// <returns>The Pen</returns>
        public static Pen Solid(IBrush<Color> brush, float width) => new Pen(brush, width);

        /// <summary>
        /// Create a pen with a 'Dash' drawing patterns
        /// </summary>
        /// <param name="color">The color.</param>
        /// <param name="width">The width.</param>
        /// <returns>The Pen</returns>
        public static Pen Dash(Color color, float width) => new Pen(Pens<Color>.Dash(color, width));

        /// <summary>
        /// Create a pen with a 'Dash' drawing patterns
        /// </summary>
        /// <param name="brush">The brush.</param>
        /// <param name="width">The width.</param>
        /// <returns>The Pen</returns>
        public static Pen Dash(IBrush<Color> brush, float width) => new Pen(Pens<Color>.Dash(brush, width));

        /// <summary>
        /// Create a pen with a 'Dot' drawing patterns
        /// </summary>
        /// <param name="color">The color.</param>
        /// <param name="width">The width.</param>
        /// <returns>The Pen</returns>
        public static Pen Dot(Color color, float width) => new Pen(Pens<Color>.Dot(color, width));

        /// <summary>
        /// Create a pen with a 'Dot' drawing patterns
        /// </summary>
        /// <param name="brush">The brush.</param>
        /// <param name="width">The width.</param>
        /// <returns>The Pen</returns>
        public static Pen Dot(IBrush<Color> brush, float width) => new Pen(Pens<Color>.Dot(brush, width));

        /// <summary>
        /// Create a pen with a 'Dash Dot' drawing patterns
        /// </summary>
        /// <param name="color">The color.</param>
        /// <param name="width">The width.</param>
        /// <returns>The Pen</returns>
        public static Pen DashDot(Color color, float width) => new Pen(Pens<Color>.DashDot(color, width));

        /// <summary>
        /// Create a pen with a 'Dash Dot' drawing patterns
        /// </summary>
        /// <param name="brush">The brush.</param>
        /// <param name="width">The width.</param>
        /// <returns>The Pen</returns>
        public static Pen DashDot(IBrush<Color> brush, float width) => new Pen(Pens<Color>.DashDot(brush, width));

        /// <summary>
        /// Create a pen with a 'Dash Dot Dot' drawing patterns
        /// </summary>
        /// <param name="color">The color.</param>
        /// <param name="width">The width.</param>
        /// <returns>The Pen</returns>
        public static Pen DashDotDot(Color color, float width) => new Pen(Pens<Color>.DashDotDot(color, width));

        /// <summary>
        /// Create a pen with a 'Dash Dot Dot' drawing patterns
        /// </summary>
        /// <param name="brush">The brush.</param>
        /// <param name="width">The width.</param>
        /// <returns>The Pen</returns>
        public static Pen DashDotDot(IBrush<Color> brush, float width) => new Pen(Pens<Color>.DashDotDot(brush, width));
    }
}