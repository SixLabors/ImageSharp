// <copyright file="Brushes.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Brushes
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;
    using System.Threading.Tasks;

    /// <summary>
    /// A collection of methods for creating brushes
    /// </summary>
    public class Brushes
    {
        /// <summary>
        /// Create as brush that will paint a solid color
        /// </summary>
        /// <param name="color">The color.</param>
        /// <returns>A Brush</returns>
        public static SolidBrush Solid(Color color)
                  => new SolidBrush(color);

        /// <summary>
        /// Create as brush that will paint a Percent10 Hatch Pattern with
        /// in the specified foreground color and a transparent background
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <returns>A Brush</returns>
        public static PatternBrush Percent10(Color foreColor)
            => new PatternBrush(Brushes<Color, uint>.Percent10(foreColor, Color.Transparent));

        /// <summary>
        /// Create as brush that will paint a Percent10 Hatch Pattern with
        /// in the specified foreground and background colors
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <param name="backColor">Color of the background.</param>
        /// <returns>A Brush</returns>
        public static PatternBrush Percent10(Color foreColor, Color backColor)
            => new PatternBrush(Brushes<Color, uint>.Percent10(foreColor, backColor));

        /// <summary>
        /// Create as brush that will paint a Percent20 Hatch Pattern with
        /// in the specified foreground color and a transparent background
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <returns>A Brush</returns>
        public static PatternBrush Percent20(Color foreColor)
            => new PatternBrush(Brushes<Color, uint>.Percent20(foreColor, Color.Transparent));

        /// <summary>
        /// Create as brush that will paint a Percent20 Hatch Pattern with
        /// in the specified foreground and background colors
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <param name="backColor">Color of the background.</param>
        /// <returns>A Brush</returns>
        public static PatternBrush Percent20(Color foreColor, Color backColor)
            => new PatternBrush(Brushes<Color, uint>.Percent20(foreColor, backColor));

        /// <summary>
        /// Create as brush that will paint a Horizontal Hatch Pattern with
        /// in the specified foreground color and a transparent background
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <returns>A Brush</returns>
        public static PatternBrush Horizontal(Color foreColor)
            => new PatternBrush(Brushes<Color, uint>.Horizontal(foreColor, Color.Transparent));

        /// <summary>
        /// Create as brush that will paint a Horizontal Hatch Pattern with
        /// in the specified foreground and background colors
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <param name="backColor">Color of the background.</param>
        /// <returns>A Brush</returns>
        public static PatternBrush Horizontal(Color foreColor, Color backColor)
            => new PatternBrush(Brushes<Color, uint>.Horizontal(foreColor, backColor));

        /// <summary>
        /// Create as brush that will paint a Min Hatch Pattern with
        /// in the specified foreground color and a transparent background
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <returns>A Brush</returns>
        public static PatternBrush Min(Color foreColor)
            => new PatternBrush(Brushes<Color, uint>.Min(foreColor, Color.Transparent));

        /// <summary>
        /// Create as brush that will paint a Min Hatch Pattern with
        /// in the specified foreground and background colors
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <param name="backColor">Color of the background.</param>
        /// <returns>A Brush</returns>
        public static PatternBrush Min(Color foreColor, Color backColor)
            => new PatternBrush(Brushes<Color, uint>.Min(foreColor, backColor));

        /// <summary>
        /// Create as brush that will paint a Vertical Hatch Pattern with
        /// in the specified foreground color and a transparent background
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <returns>A Brush</returns>
        public static PatternBrush Vertical(Color foreColor)
            => new PatternBrush(Brushes<Color, uint>.Vertical(foreColor, Color.Transparent));

        /// <summary>
        /// Create as brush that will paint a Vertical Hatch Pattern with
        /// in the specified foreground and background colors
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <param name="backColor">Color of the background.</param>
        /// <returns>A Brush</returns>
        public static PatternBrush Vertical(Color foreColor, Color backColor)
            => new PatternBrush(Brushes<Color, uint>.Vertical(foreColor, backColor));

        /// <summary>
        /// Create as brush that will paint a Forward Diagonal Hatch Pattern with
        /// in the specified foreground color and a transparent background
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <returns>A Brush</returns>
        public static PatternBrush ForwardDiagonal(Color foreColor)
            => new PatternBrush(Brushes<Color, uint>.ForwardDiagonal(foreColor, Color.Transparent));

        /// <summary>
        /// Create as brush that will paint a Forward Diagonal Hatch Pattern with
        /// in the specified foreground and background colors
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <param name="backColor">Color of the background.</param>
        /// <returns>A Brush</returns>
        public static PatternBrush ForwardDiagonal(Color foreColor, Color backColor)
            => new PatternBrush(Brushes<Color, uint>.ForwardDiagonal(foreColor, backColor));

        /// <summary>
        /// Create as brush that will paint a Backward Diagonal Hatch Pattern with
        /// in the specified foreground color and a transparent background
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <returns>A Brush</returns>
        public static PatternBrush BackwardDiagonal(Color foreColor)
            => new PatternBrush(Brushes<Color, uint>.BackwardDiagonal(foreColor, Color.Transparent));

        /// <summary>
        /// Create as brush that will paint a Backward Diagonal Hatch Pattern with
        /// in the specified foreground and background colors
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <param name="backColor">Color of the background.</param>
        /// <returns>A Brush</returns>
        public static PatternBrush BackwardDiagonal(Color foreColor, Color backColor)
            => new PatternBrush(Brushes<Color, uint>.BackwardDiagonal(foreColor, backColor));
    }

    /// <summary>
    /// A collection of methods for creating brushes.
    /// </summary>
    /// <typeparam name="TColor">The type of the color.</typeparam>
    /// <typeparam name="TPacked">The type of the packed.</typeparam>
    /// <returns>A Brush</returns>
    public partial class Brushes<TColor, TPacked>
        where TColor : struct, IPackedPixel<TPacked>
        where TPacked : struct
    {
        // note 2d arrays when configured using initalizer look inverted
        // ---> Y axis
        // ^
        // | X - axis
        // |
        private static readonly bool[,] Percent10Pattern = new bool[,]
        {
            { true, false, false, false },
            { false, false, false, false },
            { false, false, true, false },
            { false, false, false, false }
        };

        private static readonly bool[,] Percent20Pattern = new bool[,]
        {
            { true, false, true, false },
            { false, false, false, false },
            { false, true, false, true },
            { false, false, false, false }
        };

        private static readonly bool[,] HorizontalPattern = new bool[,]
        {
            { false, true, false, false },
        };

        private static readonly bool[,] MinPattern = new bool[,]
        {
            { false, false, false, true },
        };

        private static readonly bool[,] VerticalPattern = new bool[,]
        {
            { false },
            { true },
            { false },
            { false }
        };

        private static readonly bool[,] ForwardDiagonalPattern = new bool[,]
        {
            { true, false, false, false },
            { false, true, false, false },
            { false, false, true, false },
            { false, false, false, true }
        };

        private static readonly bool[,] BackwardDiagonalPattern = new bool[,]
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
