// <copyright file="DrawText.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System.Numerics;

    using Drawing;
    using Drawing.Brushes;
    using Drawing.Pens;
    using ImageSharp.PixelFormats;
    using SixLabors.Fonts;
    using SixLabors.Primitives;
    using SixLabors.Shapes;

    /// <summary>
    /// Extension methods for the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        private static readonly int DefaultTextDpi = 72;

        /// <summary>
        /// Draws the text onto the the image filled via the brush.
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="text">The text.</param>
        /// <param name="font">The font.</param>
        /// <param name="color">The color.</param>
        /// <param name="location">The location.</param>
        /// <returns>
        /// The <see cref="Image{TPixel}" />.
        /// </returns>
        public static Image<TPixel> DrawText<TPixel>(this Image<TPixel> source, string text, Font font, TPixel color, PointF location)
           where TPixel : struct, IPixel<TPixel>
        {
            return source.DrawText(text, font, color, location, TextGraphicsOptions.Default);
        }

        /// <summary>
        /// Draws the text onto the the image filled via the brush.
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="text">The text.</param>
        /// <param name="font">The font.</param>
        /// <param name="color">The color.</param>
        /// <param name="location">The location.</param>
        /// <param name="options">The options.</param>
        /// <returns>
        /// The <see cref="Image{TPixel}" />.
        /// </returns>
        public static Image<TPixel> DrawText<TPixel>(this Image<TPixel> source, string text, Font font, TPixel color, PointF location, TextGraphicsOptions options)
           where TPixel : struct, IPixel<TPixel>
        {
            return source.DrawText(text, font, Brushes.Solid(color), null, location, options);
        }

        /// <summary>
        /// Draws the text onto the the image filled via the brush.
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="text">The text.</param>
        /// <param name="font">The font.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="location">The location.</param>
        /// <returns>
        /// The <see cref="Image{TPixel}" />.
        /// </returns>
        public static Image<TPixel> DrawText<TPixel>(this Image<TPixel> source, string text, Font font, IBrush<TPixel> brush, PointF location)
           where TPixel : struct, IPixel<TPixel>
        {
            return source.DrawText(text, font, brush, location, TextGraphicsOptions.Default);
        }

        /// <summary>
        /// Draws the text onto the the image filled via the brush.
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="text">The text.</param>
        /// <param name="font">The font.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="location">The location.</param>
        /// <param name="options">The options.</param>
        /// <returns>
        /// The <see cref="Image{TPixel}" />.
        /// </returns>
        public static Image<TPixel> DrawText<TPixel>(this Image<TPixel> source, string text, Font font, IBrush<TPixel> brush, PointF location, TextGraphicsOptions options)
           where TPixel : struct, IPixel<TPixel>
        {
            return source.DrawText(text, font, brush, null, location, options);
        }

        /// <summary>
        /// Draws the text onto the the image outlined via the pen.
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="text">The text.</param>
        /// <param name="font">The font.</param>
        /// <param name="pen">The pen.</param>
        /// <param name="location">The location.</param>
        /// <returns>
        /// The <see cref="Image{TPixel}" />.
        /// </returns>
        public static Image<TPixel> DrawText<TPixel>(this Image<TPixel> source, string text, Font font, IPen<TPixel> pen, PointF location)
           where TPixel : struct, IPixel<TPixel>
        {
            return source.DrawText(text, font, pen, location, TextGraphicsOptions.Default);
        }

        /// <summary>
        /// Draws the text onto the the image outlined via the pen.
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="text">The text.</param>
        /// <param name="font">The font.</param>
        /// <param name="pen">The pen.</param>
        /// <param name="location">The location.</param>
        /// <param name="options">The options.</param>
        /// <returns>
        /// The <see cref="Image{TPixel}" />.
        /// </returns>
        public static Image<TPixel> DrawText<TPixel>(this Image<TPixel> source, string text, Font font, IPen<TPixel> pen, PointF location, TextGraphicsOptions options)
           where TPixel : struct, IPixel<TPixel>
        {
            return source.DrawText(text, font, null, pen, location, options);
        }

        /// <summary>
        /// Draws the text onto the the image filled via the brush then outlined via the pen.
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="text">The text.</param>
        /// <param name="font">The font.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="pen">The pen.</param>
        /// <param name="location">The location.</param>
        /// <returns>
        /// The <see cref="Image{TPixel}" />.
        /// </returns>
        public static Image<TPixel> DrawText<TPixel>(this Image<TPixel> source, string text, Font font, IBrush<TPixel> brush, IPen<TPixel> pen, PointF location)
           where TPixel : struct, IPixel<TPixel>
        {
            return source.DrawText(text, font, brush, pen, location, TextGraphicsOptions.Default);
        }

        /// <summary>
        /// Draws the text onto the the image filled via the brush then outlined via the pen.
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="text">The text.</param>
        /// <param name="font">The font.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="pen">The pen.</param>
        /// <param name="location">The location.</param>
        /// <param name="options">The options.</param>
        /// <returns>
        /// The <see cref="Image{TPixel}" />.
        /// </returns>
        public static Image<TPixel> DrawText<TPixel>(this Image<TPixel> source, string text, Font font, IBrush<TPixel> brush, IPen<TPixel> pen, PointF location, TextGraphicsOptions options)
           where TPixel : struct, IPixel<TPixel>
        {
            float dpiX = DefaultTextDpi;
            float dpiY = DefaultTextDpi;
            if (options.UseImageResolution)
            {
                dpiX = (float)source.MetaData.HorizontalResolution;
                dpiY = (float)source.MetaData.VerticalResolution;
            }

            var style = new RendererOptions(font, dpiX, dpiY, location)
            {
                ApplyKerning = options.ApplyKerning,
                TabWidth = options.TabWidth,
                WrappingWidth = options.WrapTextWidth,
                HorizontalAlignment = options.HorizontalAlignment,
                VerticalAlignment = options.VerticalAlignment
            };

            IPathCollection glyphs = TextBuilder.GenerateGlyphs(text, style);

            var pathOptions = (GraphicsOptions)options;
            if (brush != null)
            {
                source.Fill(brush, glyphs, pathOptions);
            }

            if (pen != null)
            {
                source.Draw(pen, glyphs, pathOptions);
            }

            return source;
        }
    }
}
