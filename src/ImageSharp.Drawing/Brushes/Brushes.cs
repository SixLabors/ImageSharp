// <copyright file="Brushes.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Brushes
{
    /// <summary>
    /// A collection of methods for creating brushes. Brushes use <see cref="Color32"/> for painting.
    /// </summary>
    public class Brushes
    {
        /// <summary>
        /// Create as brush that will paint a solid color
        /// </summary>
        /// <param name="color">The color.</param>
        /// <returns>A Brush</returns>
        public static SolidBrush Solid(Color32 color)
                  => new SolidBrush(color);

        /// <summary>
        /// Create as brush that will paint a Percent10 Hatch Pattern with
        /// in the specified foreground color and a transparent background
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <returns>A Brush</returns>
        public static PatternBrush Percent10(Color32 foreColor)
            => new PatternBrush(Brushes<Color32>.Percent10(foreColor, Color32.Transparent));

        /// <summary>
        /// Create as brush that will paint a Percent10 Hatch Pattern with
        /// in the specified foreground and background colors
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <param name="backColor">Color of the background.</param>
        /// <returns>A Brush</returns>
        public static PatternBrush Percent10(Color32 foreColor, Color32 backColor)
            => new PatternBrush(Brushes<Color32>.Percent10(foreColor, backColor));

        /// <summary>
        /// Create as brush that will paint a Percent20 Hatch Pattern with
        /// in the specified foreground color and a transparent background
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <returns>A Brush</returns>
        public static PatternBrush Percent20(Color32 foreColor)
            => new PatternBrush(Brushes<Color32>.Percent20(foreColor, Color32.Transparent));

        /// <summary>
        /// Create as brush that will paint a Percent20 Hatch Pattern with
        /// in the specified foreground and background colors
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <param name="backColor">Color of the background.</param>
        /// <returns>A Brush</returns>
        public static PatternBrush Percent20(Color32 foreColor, Color32 backColor)
            => new PatternBrush(Brushes<Color32>.Percent20(foreColor, backColor));

        /// <summary>
        /// Create as brush that will paint a Horizontal Hatch Pattern with
        /// in the specified foreground color and a transparent background
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <returns>A Brush</returns>
        public static PatternBrush Horizontal(Color32 foreColor)
            => new PatternBrush(Brushes<Color32>.Horizontal(foreColor, Color32.Transparent));

        /// <summary>
        /// Create as brush that will paint a Horizontal Hatch Pattern with
        /// in the specified foreground and background colors
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <param name="backColor">Color of the background.</param>
        /// <returns>A Brush</returns>
        public static PatternBrush Horizontal(Color32 foreColor, Color32 backColor)
            => new PatternBrush(Brushes<Color32>.Horizontal(foreColor, backColor));

        /// <summary>
        /// Create as brush that will paint a Min Hatch Pattern with
        /// in the specified foreground color and a transparent background
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <returns>A Brush</returns>
        public static PatternBrush Min(Color32 foreColor)
            => new PatternBrush(Brushes<Color32>.Min(foreColor, Color32.Transparent));

        /// <summary>
        /// Create as brush that will paint a Min Hatch Pattern with
        /// in the specified foreground and background colors
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <param name="backColor">Color of the background.</param>
        /// <returns>A Brush</returns>
        public static PatternBrush Min(Color32 foreColor, Color32 backColor)
            => new PatternBrush(Brushes<Color32>.Min(foreColor, backColor));

        /// <summary>
        /// Create as brush that will paint a Vertical Hatch Pattern with
        /// in the specified foreground color and a transparent background
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <returns>A Brush</returns>
        public static PatternBrush Vertical(Color32 foreColor)
            => new PatternBrush(Brushes<Color32>.Vertical(foreColor, Color32.Transparent));

        /// <summary>
        /// Create as brush that will paint a Vertical Hatch Pattern with
        /// in the specified foreground and background colors
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <param name="backColor">Color of the background.</param>
        /// <returns>A Brush</returns>
        public static PatternBrush Vertical(Color32 foreColor, Color32 backColor)
            => new PatternBrush(Brushes<Color32>.Vertical(foreColor, backColor));

        /// <summary>
        /// Create as brush that will paint a Forward Diagonal Hatch Pattern with
        /// in the specified foreground color and a transparent background
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <returns>A Brush</returns>
        public static PatternBrush ForwardDiagonal(Color32 foreColor)
            => new PatternBrush(Brushes<Color32>.ForwardDiagonal(foreColor, Color32.Transparent));

        /// <summary>
        /// Create as brush that will paint a Forward Diagonal Hatch Pattern with
        /// in the specified foreground and background colors
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <param name="backColor">Color of the background.</param>
        /// <returns>A Brush</returns>
        public static PatternBrush ForwardDiagonal(Color32 foreColor, Color32 backColor)
            => new PatternBrush(Brushes<Color32>.ForwardDiagonal(foreColor, backColor));

        /// <summary>
        /// Create as brush that will paint a Backward Diagonal Hatch Pattern with
        /// in the specified foreground color and a transparent background
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <returns>A Brush</returns>
        public static PatternBrush BackwardDiagonal(Color32 foreColor)
            => new PatternBrush(Brushes<Color32>.BackwardDiagonal(foreColor, Color32.Transparent));

        /// <summary>
        /// Create as brush that will paint a Backward Diagonal Hatch Pattern with
        /// in the specified foreground and background colors
        /// </summary>
        /// <param name="foreColor">Color of the foreground.</param>
        /// <param name="backColor">Color of the background.</param>
        /// <returns>A Brush</returns>
        public static PatternBrush BackwardDiagonal(Color32 foreColor, Color32 backColor)
            => new PatternBrush(Brushes<Color32>.BackwardDiagonal(foreColor, backColor));
    }
}