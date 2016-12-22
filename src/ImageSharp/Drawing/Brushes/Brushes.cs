// <copyright file="Brushes.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Brushes
{
    /// <summary>
    /// A collection of methods for creating brushes. Brushes use <see cref="Color"/> for painting.
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
            => new PatternBrush(Brushes<Color>.Percent10(foreColor, Color.Transparent));

        /// <summary>
        /// Create as brush that will paint a Percent10 Hatch Pattern with
        /// in the specified foreground and background colors
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <param name="backColor">Color of the background.</param>
        /// <returns>A Brush</returns>
        public static PatternBrush Percent10(Color foreColor, Color backColor)
            => new PatternBrush(Brushes<Color>.Percent10(foreColor, backColor));

        /// <summary>
        /// Create as brush that will paint a Percent20 Hatch Pattern with
        /// in the specified foreground color and a transparent background
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <returns>A Brush</returns>
        public static PatternBrush Percent20(Color foreColor)
            => new PatternBrush(Brushes<Color>.Percent20(foreColor, Color.Transparent));

        /// <summary>
        /// Create as brush that will paint a Percent20 Hatch Pattern with
        /// in the specified foreground and background colors
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <param name="backColor">Color of the background.</param>
        /// <returns>A Brush</returns>
        public static PatternBrush Percent20(Color foreColor, Color backColor)
            => new PatternBrush(Brushes<Color>.Percent20(foreColor, backColor));

        /// <summary>
        /// Create as brush that will paint a Horizontal Hatch Pattern with
        /// in the specified foreground color and a transparent background
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <returns>A Brush</returns>
        public static PatternBrush Horizontal(Color foreColor)
            => new PatternBrush(Brushes<Color>.Horizontal(foreColor, Color.Transparent));

        /// <summary>
        /// Create as brush that will paint a Horizontal Hatch Pattern with
        /// in the specified foreground and background colors
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <param name="backColor">Color of the background.</param>
        /// <returns>A Brush</returns>
        public static PatternBrush Horizontal(Color foreColor, Color backColor)
            => new PatternBrush(Brushes<Color>.Horizontal(foreColor, backColor));

        /// <summary>
        /// Create as brush that will paint a Min Hatch Pattern with
        /// in the specified foreground color and a transparent background
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <returns>A Brush</returns>
        public static PatternBrush Min(Color foreColor)
            => new PatternBrush(Brushes<Color>.Min(foreColor, Color.Transparent));

        /// <summary>
        /// Create as brush that will paint a Min Hatch Pattern with
        /// in the specified foreground and background colors
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <param name="backColor">Color of the background.</param>
        /// <returns>A Brush</returns>
        public static PatternBrush Min(Color foreColor, Color backColor)
            => new PatternBrush(Brushes<Color>.Min(foreColor, backColor));

        /// <summary>
        /// Create as brush that will paint a Vertical Hatch Pattern with
        /// in the specified foreground color and a transparent background
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <returns>A Brush</returns>
        public static PatternBrush Vertical(Color foreColor)
            => new PatternBrush(Brushes<Color>.Vertical(foreColor, Color.Transparent));

        /// <summary>
        /// Create as brush that will paint a Vertical Hatch Pattern with
        /// in the specified foreground and background colors
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <param name="backColor">Color of the background.</param>
        /// <returns>A Brush</returns>
        public static PatternBrush Vertical(Color foreColor, Color backColor)
            => new PatternBrush(Brushes<Color>.Vertical(foreColor, backColor));

        /// <summary>
        /// Create as brush that will paint a Forward Diagonal Hatch Pattern with
        /// in the specified foreground color and a transparent background
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <returns>A Brush</returns>
        public static PatternBrush ForwardDiagonal(Color foreColor)
            => new PatternBrush(Brushes<Color>.ForwardDiagonal(foreColor, Color.Transparent));

        /// <summary>
        /// Create as brush that will paint a Forward Diagonal Hatch Pattern with
        /// in the specified foreground and background colors
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <param name="backColor">Color of the background.</param>
        /// <returns>A Brush</returns>
        public static PatternBrush ForwardDiagonal(Color foreColor, Color backColor)
            => new PatternBrush(Brushes<Color>.ForwardDiagonal(foreColor, backColor));

        /// <summary>
        /// Create as brush that will paint a Backward Diagonal Hatch Pattern with
        /// in the specified foreground color and a transparent background
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <returns>A Brush</returns>
        public static PatternBrush BackwardDiagonal(Color foreColor)
            => new PatternBrush(Brushes<Color>.BackwardDiagonal(foreColor, Color.Transparent));

        /// <summary>
        /// Create as brush that will paint a Backward Diagonal Hatch Pattern with
        /// in the specified foreground and background colors
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <param name="backColor">Color of the background.</param>
        /// <returns>A Brush</returns>
        public static PatternBrush BackwardDiagonal(Color foreColor, Color backColor)
            => new PatternBrush(Brushes<Color>.BackwardDiagonal(foreColor, backColor));
    }
}