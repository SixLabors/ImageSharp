// <copyright file="DrawPath.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using Drawing;
    using Drawing.Brushes;
    using Drawing.Pens;
    using ImageSharp.PixelFormats;
    using SixLabors.Shapes;

    /// <summary>
    /// Extension methods for the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Draws the outline of the polygon with the provided pen.
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="pen">The pen.</param>
        /// <param name="path">The path.</param>
        /// <param name="options">The options.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Draw<TPixel>(this Image<TPixel> source, IPen<TPixel> pen, IPath path, GraphicsOptions options)
           where TPixel : struct, IPixel<TPixel>
        {
            return source.Fill(pen.StrokeFill, new ShapePath(path, pen), options);
        }

        /// <summary>
        /// Draws the outline of the polygon with the provided pen.
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="pen">The pen.</param>
        /// <param name="path">The path.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Draw<TPixel>(this Image<TPixel> source, IPen<TPixel> pen, IPath path)
           where TPixel : struct, IPixel<TPixel>
        {
            return source.Draw(pen, path, GraphicsOptions.Default);
        }

        /// <summary>
        /// Draws the outline of the polygon with the provided brush at the provided thickness.
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="thickness">The thickness.</param>
        /// <param name="path">The shape.</param>
        /// <param name="options">The options.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Draw<TPixel>(this Image<TPixel> source, IBrush<TPixel> brush, float thickness, IPath path, GraphicsOptions options)
           where TPixel : struct, IPixel<TPixel>
        {
            return source.Draw(new Pen<TPixel>(brush, thickness), path, options);
        }

        /// <summary>
        /// Draws the outline of the polygon with the provided brush at the provided thickness.
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="thickness">The thickness.</param>
        /// <param name="path">The path.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Draw<TPixel>(this Image<TPixel> source, IBrush<TPixel> brush, float thickness, IPath path)
           where TPixel : struct, IPixel<TPixel>
        {
            return source.Draw(new Pen<TPixel>(brush, thickness), path);
        }

        /// <summary>
        /// Draws the outline of the polygon with the provided brush at the provided thickness.
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color.</param>
        /// <param name="thickness">The thickness.</param>
        /// <param name="path">The path.</param>
        /// <param name="options">The options.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Draw<TPixel>(this Image<TPixel> source, TPixel color, float thickness, IPath path, GraphicsOptions options)
           where TPixel : struct, IPixel<TPixel>
        {
            return source.Draw(new SolidBrush<TPixel>(color), thickness, path, options);
        }

        /// <summary>
        /// Draws the outline of the polygon with the provided brush at the provided thickness.
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="color">The color.</param>
        /// <param name="thickness">The thickness.</param>
        /// <param name="path">The path.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Draw<TPixel>(this Image<TPixel> source, TPixel color, float thickness, IPath path)
           where TPixel : struct, IPixel<TPixel>
        {
            return source.Draw(new SolidBrush<TPixel>(color), thickness, path);
        }
    }
}
