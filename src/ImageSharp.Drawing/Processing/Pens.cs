// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Contains a collection of common Pen styles
    /// </summary>
    public static class Pens
    {
        private static readonly float[] DashDotPattern = { 3f, 1f, 1f, 1f };
        private static readonly float[] DashDotDotPattern = { 3f, 1f, 1f, 1f, 1f, 1f };
        private static readonly float[] DottedPattern = { 1f, 1f };
        private static readonly float[] DashedPattern = { 3f, 1f };
        internal static readonly float[] EmptyPattern = new float[0];

        /// <summary>
        /// Create a solid pen with out any drawing patterns
        /// </summary>
        /// <param name="color">The color.</param>
        /// <param name="width">The width.</param>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <returns>The Pen</returns>
        public static Pen<TPixel> Solid<TPixel>(TPixel color, float width)
            where TPixel : struct, IPixel<TPixel>
            => new Pen<TPixel>(color, width);

        /// <summary>
        /// Create a solid pen with out any drawing patterns
        /// </summary>
        /// <param name="brush">The brush.</param>
        /// <param name="width">The width.</param>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <returns>The Pen</returns>
        public static Pen<TPixel> Solid<TPixel>(IBrush<TPixel> brush, float width)
            where TPixel : struct, IPixel<TPixel>
            => new Pen<TPixel>(brush, width);

        /// <summary>
        /// Create a pen with a 'Dash' drawing patterns
        /// </summary>
        /// <param name="color">The color.</param>
        /// <param name="width">The width.</param>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <returns>The Pen</returns>
        public static Pen<TPixel> Dash<TPixel>(TPixel color, float width)
            where TPixel : struct, IPixel<TPixel>
            => new Pen<TPixel>(color, width, DashedPattern);

        /// <summary>
        /// Create a pen with a 'Dash' drawing patterns
        /// </summary>
        /// <param name="brush">The brush.</param>
        /// <param name="width">The width.</param>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <returns>The Pen</returns>
        public static Pen<TPixel> Dash<TPixel>(IBrush<TPixel> brush, float width)
            where TPixel : struct, IPixel<TPixel>
            => new Pen<TPixel>(brush, width, DashedPattern);

        /// <summary>
        /// Create a pen with a 'Dot' drawing patterns
        /// </summary>
        /// <param name="color">The color.</param>
        /// <param name="width">The width.</param>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <returns>The Pen</returns>
        public static Pen<TPixel> Dot<TPixel>(TPixel color, float width)
            where TPixel : struct, IPixel<TPixel>
            => new Pen<TPixel>(color, width, DottedPattern);

        /// <summary>
        /// Create a pen with a 'Dot' drawing patterns
        /// </summary>
        /// <param name="brush">The brush.</param>
        /// <param name="width">The width.</param>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <returns>The Pen</returns>
        public static Pen<TPixel> Dot<TPixel>(IBrush<TPixel> brush, float width)
            where TPixel : struct, IPixel<TPixel>
            => new Pen<TPixel>(brush, width, DottedPattern);

        /// <summary>
        /// Create a pen with a 'Dash Dot' drawing patterns
        /// </summary>
        /// <param name="color">The color.</param>
        /// <param name="width">The width.</param>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <returns>The Pen</returns>
        public static Pen<TPixel> DashDot<TPixel>(TPixel color, float width)
            where TPixel : struct, IPixel<TPixel>
            => new Pen<TPixel>(color, width, DashDotPattern);

        /// <summary>
        /// Create a pen with a 'Dash Dot' drawing patterns
        /// </summary>
        /// <param name="brush">The brush.</param>
        /// <param name="width">The width.</param>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <returns>The Pen</returns>
        public static Pen<TPixel> DashDot<TPixel>(IBrush<TPixel> brush, float width)
            where TPixel : struct, IPixel<TPixel>
            => new Pen<TPixel>(brush, width, DashDotPattern);

        /// <summary>
        /// Create a pen with a 'Dash Dot Dot' drawing patterns
        /// </summary>
        /// <param name="color">The color.</param>
        /// <param name="width">The width.</param>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <returns>The Pen</returns>
        public static Pen<TPixel> DashDotDot<TPixel>(TPixel color, float width)
            where TPixel : struct, IPixel<TPixel>
            => new Pen<TPixel>(color, width, DashDotDotPattern);

        /// <summary>
        /// Create a pen with a 'Dash Dot Dot' drawing patterns
        /// </summary>
        /// <param name="brush">The brush.</param>
        /// <param name="width">The width.</param>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <returns>The Pen</returns>
        public static Pen<TPixel> DashDotDot<TPixel>(IBrush<TPixel> brush, float width)
            where TPixel : struct, IPixel<TPixel>
            => new Pen<TPixel>(brush, width, DashDotDotPattern);
    }
}