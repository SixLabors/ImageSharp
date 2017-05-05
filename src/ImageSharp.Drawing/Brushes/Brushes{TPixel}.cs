// <copyright file="Brushes{TPixel}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Brushes
{
    using ImageSharp.PixelFormats;

    /// <summary>
    /// A collection of methods for creating generic brushes.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <returns>A New <see cref="PatternBrush{TPixel}"/></returns>
    public class Brushes<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Percent10 Hatch Pattern
        /// </summary>
        /// ---> x axis
        /// ^
        /// | y - axis
        /// |
        /// see PatternBrush for details about how to make new patterns work
        private static readonly bool[,] Percent10Pattern =
        {
            { true, false, false, false },
            { false, false, false, false },
            { false, false, true, false },
            { false, false, false, false }
        };

        /// <summary>
        /// Percent20 pattern.
        /// </summary>
        private static readonly bool[,] Percent20Pattern =
        {
            { true,  false, false, false },
            { false, false, true,  false },
            { true,  false, false, false },
            { false, false, true,  false }
        };

        /// <summary>
        /// Horizontal Hatch Pattern
        /// </summary>
        private static readonly bool[,] HorizontalPattern =
        {
            { false },
            { true },
            { false },
            { false }
        };

        /// <summary>
        /// Min Pattern
        /// </summary>
        private static readonly bool[,] MinPattern =
        {
            { false },
            { false },
            { false },
            { true }
        };

        /// <summary>
        /// Vertical Pattern
        /// </summary>
        private static readonly bool[,] VerticalPattern =
        {
            { false, true, false, false },
        };

        /// <summary>
        /// Forward Diagonal Pattern
        /// </summary>
        private static readonly bool[,] ForwardDiagonalPattern =
        {
            { false, false, false, true },
            { false, false, true, false },
            { false, true, false, false },
            { true,  false, false, false }
        };

        /// <summary>
        /// Backward Diagonal Pattern
        /// </summary>
        private static readonly bool[,] BackwardDiagonalPattern =
        {
            { true, false, false, false },
            { false, true, false, false },
            { false, false, true, false },
            { false, false, false, true }
        };

        /// <summary>
        /// Create as brush that will paint a solid color
        /// </summary>
        /// <param name="color">The color.</param>
        /// <returns>A New <see cref="PatternBrush{TPixel}"/></returns>
        public static SolidBrush<TPixel> Solid(TPixel color)
            => new SolidBrush<TPixel>(color);

        /// <summary>
        /// Create as brush that will paint a Percent10 Hatch Pattern with the specified colors
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <returns>A New <see cref="PatternBrush{TPixel}"/></returns>
        public static PatternBrush<TPixel> Percent10(TPixel foreColor)
            => new PatternBrush<TPixel>(foreColor, NamedColors<TPixel>.Transparent, Percent10Pattern);

        /// <summary>
        /// Create as brush that will paint a Percent10 Hatch Pattern with the specified colors
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <param name="backColor">Color of the background.</param>
        /// <returns>A New <see cref="PatternBrush{TPixel}"/></returns>
        public static PatternBrush<TPixel> Percent10(TPixel foreColor, TPixel backColor)
            => new PatternBrush<TPixel>(foreColor, backColor, Percent10Pattern);

        /// <summary>
        /// Create as brush that will paint a Percent20 Hatch Pattern with the specified foreground color and a
        /// transparent background.
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <returns>A New <see cref="PatternBrush{TPixel}"/></returns>
        public static PatternBrush<TPixel> Percent20(TPixel foreColor)
            => new PatternBrush<TPixel>(foreColor, NamedColors<TPixel>.Transparent, Percent20Pattern);

        /// <summary>
        /// Create as brush that will paint a Percent20 Hatch Pattern with the specified colors
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <param name="backColor">Color of the background.</param>
        /// <returns>A New <see cref="PatternBrush{TPixel}"/></returns>
        public static PatternBrush<TPixel> Percent20(TPixel foreColor, TPixel backColor)
            => new PatternBrush<TPixel>(foreColor, backColor, Percent20Pattern);

        /// <summary>
        /// Create as brush that will paint a Horizontal Hatch Pattern with the specified foreground color and a
        /// transparent background.
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <returns>A New <see cref="PatternBrush{TPixel}"/></returns>
        public static PatternBrush<TPixel> Horizontal(TPixel foreColor)
            => new PatternBrush<TPixel>(foreColor, NamedColors<TPixel>.Transparent, HorizontalPattern);

        /// <summary>
        /// Create as brush that will paint a Horizontal Hatch Pattern with the specified colors
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <param name="backColor">Color of the background.</param>
        /// <returns>A New <see cref="PatternBrush{TPixel}"/></returns>
        public static PatternBrush<TPixel> Horizontal(TPixel foreColor, TPixel backColor)
            => new PatternBrush<TPixel>(foreColor, backColor, HorizontalPattern);

        /// <summary>
        /// Create as brush that will paint a Min Hatch Pattern with the specified foreground color and a
        /// transparent background.
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <returns>A New <see cref="PatternBrush{TPixel}"/></returns>
        public static PatternBrush<TPixel> Min(TPixel foreColor)
            => new PatternBrush<TPixel>(foreColor, NamedColors<TPixel>.Transparent, MinPattern);

        /// <summary>
        /// Create as brush that will paint a Min Hatch Pattern with the specified colors
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <param name="backColor">Color of the background.</param>
        /// <returns>A New <see cref="PatternBrush{TPixel}"/></returns>
        public static PatternBrush<TPixel> Min(TPixel foreColor, TPixel backColor)
            => new PatternBrush<TPixel>(foreColor, backColor, MinPattern);

        /// <summary>
        /// Create as brush that will paint a Vertical Hatch Pattern with the specified foreground color and a
        /// transparent background.
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <returns>A New <see cref="PatternBrush{TPixel}"/></returns>
        public static PatternBrush<TPixel> Vertical(TPixel foreColor)
            => new PatternBrush<TPixel>(foreColor, NamedColors<TPixel>.Transparent, VerticalPattern);

        /// <summary>
        /// Create as brush that will paint a Vertical Hatch Pattern with the specified colors
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <param name="backColor">Color of the background.</param>
        /// <returns>A New <see cref="PatternBrush{TPixel}"/></returns>
        public static PatternBrush<TPixel> Vertical(TPixel foreColor, TPixel backColor)
            => new PatternBrush<TPixel>(foreColor, backColor, VerticalPattern);

        /// <summary>
        /// Create as brush that will paint a Forward Diagonal Hatch Pattern with the specified foreground color and a
        /// transparent background.
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <returns>A New <see cref="PatternBrush{TPixel}"/></returns>
        public static PatternBrush<TPixel> ForwardDiagonal(TPixel foreColor)
            => new PatternBrush<TPixel>(foreColor, NamedColors<TPixel>.Transparent, ForwardDiagonalPattern);

        /// <summary>
        /// Create as brush that will paint a Forward Diagonal Hatch Pattern with the specified colors
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <param name="backColor">Color of the background.</param>
        /// <returns>A New <see cref="PatternBrush{TPixel}"/></returns>
        public static PatternBrush<TPixel> ForwardDiagonal(TPixel foreColor, TPixel backColor)
            => new PatternBrush<TPixel>(foreColor, backColor, ForwardDiagonalPattern);

        /// <summary>
        /// Create as brush that will paint a Backward Diagonal Hatch Pattern with the specified foreground color and a
        /// transparent background.
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <returns>A New <see cref="PatternBrush{TPixel}"/></returns>
        public static PatternBrush<TPixel> BackwardDiagonal(TPixel foreColor)
            => new PatternBrush<TPixel>(foreColor, NamedColors<TPixel>.Transparent, BackwardDiagonalPattern);

        /// <summary>
        /// Create as brush that will paint a Backward Diagonal Hatch Pattern with the specified colors
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <param name="backColor">Color of the background.</param>
        /// <returns>A New <see cref="PatternBrush{TPixel}"/></returns>
        public static PatternBrush<TPixel> BackwardDiagonal(TPixel foreColor, TPixel backColor)
            => new PatternBrush<TPixel>(foreColor, backColor, BackwardDiagonalPattern);
    }
}