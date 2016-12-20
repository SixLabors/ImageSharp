// <copyright file="Pens{TColor}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Pens
{
    using System;

    /// <summary>
    /// Common Pen styles
    /// </summary>
    /// <typeparam name="TColor">The type of the color.</typeparam>
    public class Pens<TColor>
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        private static readonly float[] DashDotPattern = new[] { 3f, 1f, 1f, 1f };
        private static readonly float[] DashDotDotPattern = new[] { 3f, 1f, 1f, 1f, 1f, 1f };
        private static readonly float[] DottedPattern = new[] { 1f, 1f };
        private static readonly float[] DashedPattern = new[] { 3f, 1f };

        /// <summary>
        /// Create a solid pen with out any drawing patterns
        /// </summary>
        /// <param name="color">The color.</param>
        /// <param name="width">The width.</param>
        /// <returns>The Pen</returns>
        public static Pen<TColor> Solid(TColor color, float width)
            => new Pen<TColor>(color, width);

        /// <summary>
        /// Create a solid pen with out any drawing patterns
        /// </summary>
        /// <param name="brush">The brush.</param>
        /// <param name="width">The width.</param>
        /// <returns>The Pen</returns>
        public static Pen<TColor> Solid(IBrush<TColor> brush, float width)
            => new Pen<TColor>(brush, width);

        /// <summary>
        /// Create a pen with a 'Dash' drawing patterns
        /// </summary>
        /// <param name="color">The color.</param>
        /// <param name="width">The width.</param>
        /// <returns>The Pen</returns>
        public static Pen<TColor> Dash(TColor color, float width)
            => new Pen<TColor>(color, width, DashedPattern);

        /// <summary>
        /// Create a pen with a 'Dash' drawing patterns
        /// </summary>
        /// <param name="brush">The brush.</param>
        /// <param name="width">The width.</param>
        /// <returns>The Pen</returns>
        public static Pen<TColor> Dash(IBrush<TColor> brush, float width)
            => new Pen<TColor>(brush, width, DashedPattern);

        /// <summary>
        /// Create a pen with a 'Dot' drawing patterns
        /// </summary>
        /// <param name="color">The color.</param>
        /// <param name="width">The width.</param>
        /// <returns>The Pen</returns>
        public static Pen<TColor> Dot(TColor color, float width)
            => new Pen<TColor>(color, width, DottedPattern);

        /// <summary>
        /// Create a pen with a 'Dot' drawing patterns
        /// </summary>
        /// <param name="brush">The brush.</param>
        /// <param name="width">The width.</param>
        /// <returns>The Pen</returns>
        public static Pen<TColor> Dot(IBrush<TColor> brush, float width)
            => new Pen<TColor>(brush, width, DottedPattern);

        /// <summary>
        /// Create a pen with a 'Dash Dot' drawing patterns
        /// </summary>
        /// <param name="color">The color.</param>
        /// <param name="width">The width.</param>
        /// <returns>The Pen</returns>
        public static Pen<TColor> DashDot(TColor color, float width)
            => new Pen<TColor>(color, width, DashDotPattern);

        /// <summary>
        /// Create a pen with a 'Dash Dot' drawing patterns
        /// </summary>
        /// <param name="brush">The brush.</param>
        /// <param name="width">The width.</param>
        /// <returns>The Pen</returns>
        public static Pen<TColor> DashDot(IBrush<TColor> brush, float width)
            => new Pen<TColor>(brush, width, DashDotPattern);

        /// <summary>
        /// Create a pen with a 'Dash Dot Dot' drawing patterns
        /// </summary>
        /// <param name="color">The color.</param>
        /// <param name="width">The width.</param>
        /// <returns>The Pen</returns>
        public static Pen<TColor> DashDotDot(TColor color, float width)
            => new Pen<TColor>(color, width, DashDotDotPattern);

        /// <summary>
        /// Create a pen with a 'Dash Dot Dot' drawing patterns
        /// </summary>
        /// <param name="brush">The brush.</param>
        /// <param name="width">The width.</param>
        /// <returns>The Pen</returns>
        public static Pen<TColor> DashDotDot(IBrush<TColor> brush, float width)
            => new Pen<TColor>(brush, width, DashDotDotPattern);
    }
}