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
        public static Pen Solid(Color32 color, float width) => new Pen(color, width);

        /// <summary>
        /// Create a solid pen with out any drawing patterns
        /// </summary>
        /// <param name="brush">The brush.</param>
        /// <param name="width">The width.</param>
        /// <returns>The Pen</returns>
        public static Pen Solid(IBrush<Color32> brush, float width) => new Pen(brush, width);

        /// <summary>
        /// Create a pen with a 'Dash' drawing patterns
        /// </summary>
        /// <param name="color">The color.</param>
        /// <param name="width">The width.</param>
        /// <returns>The Pen</returns>
        public static Pen Dash(Color32 color, float width) => new Pen(Pens<Color32>.Dash(color, width));

        /// <summary>
        /// Create a pen with a 'Dash' drawing patterns
        /// </summary>
        /// <param name="brush">The brush.</param>
        /// <param name="width">The width.</param>
        /// <returns>The Pen</returns>
        public static Pen Dash(IBrush<Color32> brush, float width) => new Pen(Pens<Color32>.Dash(brush, width));

        /// <summary>
        /// Create a pen with a 'Dot' drawing patterns
        /// </summary>
        /// <param name="color">The color.</param>
        /// <param name="width">The width.</param>
        /// <returns>The Pen</returns>
        public static Pen Dot(Color32 color, float width) => new Pen(Pens<Color32>.Dot(color, width));

        /// <summary>
        /// Create a pen with a 'Dot' drawing patterns
        /// </summary>
        /// <param name="brush">The brush.</param>
        /// <param name="width">The width.</param>
        /// <returns>The Pen</returns>
        public static Pen Dot(IBrush<Color32> brush, float width) => new Pen(Pens<Color32>.Dot(brush, width));

        /// <summary>
        /// Create a pen with a 'Dash Dot' drawing patterns
        /// </summary>
        /// <param name="color">The color.</param>
        /// <param name="width">The width.</param>
        /// <returns>The Pen</returns>
        public static Pen DashDot(Color32 color, float width) => new Pen(Pens<Color32>.DashDot(color, width));

        /// <summary>
        /// Create a pen with a 'Dash Dot' drawing patterns
        /// </summary>
        /// <param name="brush">The brush.</param>
        /// <param name="width">The width.</param>
        /// <returns>The Pen</returns>
        public static Pen DashDot(IBrush<Color32> brush, float width) => new Pen(Pens<Color32>.DashDot(brush, width));

        /// <summary>
        /// Create a pen with a 'Dash Dot Dot' drawing patterns
        /// </summary>
        /// <param name="color">The color.</param>
        /// <param name="width">The width.</param>
        /// <returns>The Pen</returns>
        public static Pen DashDotDot(Color32 color, float width) => new Pen(Pens<Color32>.DashDotDot(color, width));

        /// <summary>
        /// Create a pen with a 'Dash Dot Dot' drawing patterns
        /// </summary>
        /// <param name="brush">The brush.</param>
        /// <param name="width">The width.</param>
        /// <returns>The Pen</returns>
        public static Pen DashDotDot(IBrush<Color32> brush, float width) => new Pen(Pens<Color32>.DashDotDot(brush, width));
    }
}