// <copyright file="Pens{TPixel}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Pens
{
    using ImageSharp.PixelFormats;

    /// <summary>
    /// Common Pen styles
    /// </summary>
    /// <typeparam name="TPixel">The type of the color.</typeparam>
    public class Pens<TPixel>
        where TPixel : struct, IPixel<TPixel>
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
        public static Pen<TPixel> Solid(TPixel color, float width)
            => new Pen<TPixel>(color, width);

        /// <summary>
        /// Create a solid pen with out any drawing patterns
        /// </summary>
        /// <param name="brush">The brush.</param>
        /// <param name="width">The width.</param>
        /// <returns>The Pen</returns>
        public static Pen<TPixel> Solid(IBrush<TPixel> brush, float width)
            => new Pen<TPixel>(brush, width);

        /// <summary>
        /// Create a pen with a 'Dash' drawing patterns
        /// </summary>
        /// <param name="color">The color.</param>
        /// <param name="width">The width.</param>
        /// <returns>The Pen</returns>
        public static Pen<TPixel> Dash(TPixel color, float width)
            => new Pen<TPixel>(color, width, DashedPattern);

        /// <summary>
        /// Create a pen with a 'Dash' drawing patterns
        /// </summary>
        /// <param name="brush">The brush.</param>
        /// <param name="width">The width.</param>
        /// <returns>The Pen</returns>
        public static Pen<TPixel> Dash(IBrush<TPixel> brush, float width)
            => new Pen<TPixel>(brush, width, DashedPattern);

        /// <summary>
        /// Create a pen with a 'Dot' drawing patterns
        /// </summary>
        /// <param name="color">The color.</param>
        /// <param name="width">The width.</param>
        /// <returns>The Pen</returns>
        public static Pen<TPixel> Dot(TPixel color, float width)
            => new Pen<TPixel>(color, width, DottedPattern);

        /// <summary>
        /// Create a pen with a 'Dot' drawing patterns
        /// </summary>
        /// <param name="brush">The brush.</param>
        /// <param name="width">The width.</param>
        /// <returns>The Pen</returns>
        public static Pen<TPixel> Dot(IBrush<TPixel> brush, float width)
            => new Pen<TPixel>(brush, width, DottedPattern);

        /// <summary>
        /// Create a pen with a 'Dash Dot' drawing patterns
        /// </summary>
        /// <param name="color">The color.</param>
        /// <param name="width">The width.</param>
        /// <returns>The Pen</returns>
        public static Pen<TPixel> DashDot(TPixel color, float width)
            => new Pen<TPixel>(color, width, DashDotPattern);

        /// <summary>
        /// Create a pen with a 'Dash Dot' drawing patterns
        /// </summary>
        /// <param name="brush">The brush.</param>
        /// <param name="width">The width.</param>
        /// <returns>The Pen</returns>
        public static Pen<TPixel> DashDot(IBrush<TPixel> brush, float width)
            => new Pen<TPixel>(brush, width, DashDotPattern);

        /// <summary>
        /// Create a pen with a 'Dash Dot Dot' drawing patterns
        /// </summary>
        /// <param name="color">The color.</param>
        /// <param name="width">The width.</param>
        /// <returns>The Pen</returns>
        public static Pen<TPixel> DashDotDot(TPixel color, float width)
            => new Pen<TPixel>(color, width, DashDotDotPattern);

        /// <summary>
        /// Create a pen with a 'Dash Dot Dot' drawing patterns
        /// </summary>
        /// <param name="brush">The brush.</param>
        /// <param name="width">The width.</param>
        /// <returns>The Pen</returns>
        public static Pen<TPixel> DashDotDot(IBrush<TPixel> brush, float width)
            => new Pen<TPixel>(brush, width, DashDotDotPattern);
    }
}