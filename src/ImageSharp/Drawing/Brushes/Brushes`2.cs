// <copyright file="Brushes`2.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

using System;

namespace ImageSharp.Drawing.Brushes
{
    /// <summary>
    /// A collection of methods for creating generic brushes.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    /// <returns>A Brush</returns>
    public class Brushes<TColor, TPacked>
        where TColor : struct, IPackedPixel<TPacked>
        where TPacked : struct, IEquatable<TPacked>
    {
        /// <summary>
        /// Percent10 Hatch Pattern
        /// </summary>
        /// note 2d arrays when configured using initalizer look inverted
        /// ---> Y axis
        /// ^
        /// | X - axis
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
            { true, false, true, false },
            { false, false, false, false },
            { false, true, false, true },
            { false, false, false, false }
        };

        /// <summary>
        /// Horizontal Hatch Pattern
        /// </summary>
        private static readonly bool[,] HorizontalPattern =
        {
            { false, true, false, false },
        };

        /// <summary>
        /// Min Pattern
        /// </summary>
        private static readonly bool[,] MinPattern =
        {
            { false, false, false, true },
        };

        /// <summary>
        /// Vertical Pattern
        /// </summary>
        private static readonly bool[,] VerticalPattern =
        {
            { false },
            { true },
            { false },
            { false }
        };

        /// <summary>
        /// Forward Diagonal Pattern
        /// </summary>
        private static readonly bool[,] ForwardDiagonalPattern =
        {
            { true, false, false, false },
            { false, true, false, false },
            { false, false, true, false },
            { false, false, false, true }
        };

        /// <summary>
        /// Backward Diagonal Pattern
        /// </summary>
        private static readonly bool[,] BackwardDiagonalPattern =
        {
            { false, false, false, true },
            { false, false, true, false },
            { false, true, false, false },
            { true,  false, false, false }
        };

        /// <summary>
        /// Create as brush that will paint a solid color
        /// </summary>
        /// <param name="color">The color.</param>
        /// <returns>A Brush</returns>
        public static SolidBrush<TColor, TPacked> Solid(TColor color)
            => new SolidBrush<TColor, TPacked>(color);

        /// <summary>
        /// Create as brush that will paint a Percent10 Hatch Pattern within the specified colors
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <param name="backColor">Color of the background.</param>
        /// <returns>A Brush</returns>
        public static PatternBrush<TColor, TPacked> Percent10(TColor foreColor, TColor backColor)
            => new PatternBrush<TColor, TPacked>(foreColor, backColor, Percent10Pattern);

        /// <summary>
        /// Create as brush that will paint a Percent20 Hatch Pattern within the specified colors
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <param name="backColor">Color of the background.</param>
        /// <returns>A Brush</returns>
        public static PatternBrush<TColor, TPacked> Percent20(TColor foreColor, TColor backColor)
            => new PatternBrush<TColor, TPacked>(foreColor, backColor, Percent20Pattern);

        /// <summary>
        /// Create as brush that will paint a Horizontal Hatch Pattern within the specified colors
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <param name="backColor">Color of the background.</param>
        /// <returns>A Brush</returns>
        public static PatternBrush<TColor, TPacked> Horizontal(TColor foreColor, TColor backColor)
            => new PatternBrush<TColor, TPacked>(foreColor, backColor, HorizontalPattern);

        /// <summary>
        /// Create as brush that will paint a Min Hatch Pattern within the specified colors
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <param name="backColor">Color of the background.</param>
        /// <returns>A Brush</returns>
        public static PatternBrush<TColor, TPacked> Min(TColor foreColor, TColor backColor)
            => new PatternBrush<TColor, TPacked>(foreColor, backColor, MinPattern);

        /// <summary>
        /// Create as brush that will paint a Vertical Hatch Pattern within the specified colors
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <param name="backColor">Color of the background.</param>
        /// <returns>A Brush</returns>
        public static PatternBrush<TColor, TPacked> Vertical(TColor foreColor, TColor backColor)
            => new PatternBrush<TColor, TPacked>(foreColor, backColor, VerticalPattern);

        /// <summary>
        /// Create as brush that will paint a Forward Diagonal Hatch Pattern within the specified colors
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <param name="backColor">Color of the background.</param>
        /// <returns>A Brush</returns>
        public static PatternBrush<TColor, TPacked> ForwardDiagonal(TColor foreColor, TColor backColor)
            => new PatternBrush<TColor, TPacked>(foreColor, backColor, ForwardDiagonalPattern);

        /// <summary>
        /// Create as brush that will paint a Backward Diagonal Hatch Pattern within the specified colors
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <param name="backColor">Color of the background.</param>
        /// <returns>A Brush</returns>
        public static PatternBrush<TColor, TPacked> BackwardDiagonal(TColor foreColor, TColor backColor)
            => new PatternBrush<TColor, TPacked>(foreColor, backColor, BackwardDiagonalPattern);
    }
}
