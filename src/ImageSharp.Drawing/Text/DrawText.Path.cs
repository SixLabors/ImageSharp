// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.Fonts;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Brushes;
using SixLabors.ImageSharp.Drawing.Pens;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Shapes;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Extension methods for the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Draws the text onto the the image filled via the brush.
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="text">The text.</param>
        /// <param name="font">The font.</param>
        /// <param name="color">The color.</param>
        /// <param name="path">The path.</param>
        /// <returns>
        /// The <see cref="Image{TPixel}" />.
        /// </returns>
        public static IImageProcessingContext<TPixel> DrawText<TPixel>(this IImageProcessingContext<TPixel> source, string text, Font font, TPixel color, IPath path)
           where TPixel : struct, IPixel<TPixel>
        {
            return source.DrawText(text, font, color, path, TextGraphicsOptions.Default);
        }

        /// <summary>
        /// Draws the text onto the the image filled via the brush.
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="text">The text.</param>
        /// <param name="font">The font.</param>
        /// <param name="color">The color.</param>
        /// <param name="path">The path.</param>
        /// <param name="options">The options.</param>
        /// <returns>
        /// The <see cref="Image{TPixel}" />.
        /// </returns>
        public static IImageProcessingContext<TPixel> DrawText<TPixel>(this IImageProcessingContext<TPixel> source, string text, Font font, TPixel color, IPath path, TextGraphicsOptions options)
           where TPixel : struct, IPixel<TPixel>
        {
            return source.DrawText(text, font, Brushes.Solid(color), null, path, options);
        }

        /// <summary>
        /// Draws the text onto the the image filled via the brush.
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="text">The text.</param>
        /// <param name="font">The font.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="path">The location.</param>
        /// <returns>
        /// The <see cref="Image{TPixel}" />.
        /// </returns>
        public static IImageProcessingContext<TPixel> DrawText<TPixel>(this IImageProcessingContext<TPixel> source, string text, Font font, IBrush<TPixel> brush, IPath path)
           where TPixel : struct, IPixel<TPixel>
        {
            return source.DrawText(text, font, brush, path, TextGraphicsOptions.Default);
        }

        /// <summary>
        /// Draws the text onto the the image filled via the brush.
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="text">The text.</param>
        /// <param name="font">The font.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="path">The path.</param>
        /// <param name="options">The options.</param>
        /// <returns>
        /// The <see cref="Image{TPixel}" />.
        /// </returns>
        public static IImageProcessingContext<TPixel> DrawText<TPixel>(this IImageProcessingContext<TPixel> source, string text, Font font, IBrush<TPixel> brush, IPath path, TextGraphicsOptions options)
           where TPixel : struct, IPixel<TPixel>
        {
            return source.DrawText(text, font, brush, null, path, options);
        }

        /// <summary>
        /// Draws the text onto the the image outlined via the pen.
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="text">The text.</param>
        /// <param name="font">The font.</param>
        /// <param name="pen">The pen.</param>
        /// <param name="path">The path.</param>
        /// <returns>
        /// The <see cref="Image{TPixel}" />.
        /// </returns>
        public static IImageProcessingContext<TPixel> DrawText<TPixel>(this IImageProcessingContext<TPixel> source, string text, Font font, IPen<TPixel> pen, IPath path)
           where TPixel : struct, IPixel<TPixel>
        {
            return source.DrawText(text, font, pen, path, TextGraphicsOptions.Default);
        }

        /// <summary>
        /// Draws the text onto the the image outlined via the pen.
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="text">The text.</param>
        /// <param name="font">The font.</param>
        /// <param name="pen">The pen.</param>
        /// <param name="path">The path.</param>
        /// <param name="options">The options.</param>
        /// <returns>
        /// The <see cref="Image{TPixel}" />.
        /// </returns>
        public static IImageProcessingContext<TPixel> DrawText<TPixel>(this IImageProcessingContext<TPixel> source, string text, Font font, IPen<TPixel> pen, IPath path, TextGraphicsOptions options)
           where TPixel : struct, IPixel<TPixel>
        {
            return source.DrawText(text, font, null, pen, path, options);
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
        /// <param name="path">The path.</param>
        /// <returns>
        /// The <see cref="Image{TPixel}" />.
        /// </returns>
        public static IImageProcessingContext<TPixel> DrawText<TPixel>(this IImageProcessingContext<TPixel> source, string text, Font font, IBrush<TPixel> brush, IPen<TPixel> pen, IPath path)
           where TPixel : struct, IPixel<TPixel>
        {
            return source.DrawText(text, font, brush, pen, path, TextGraphicsOptions.Default);
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
        /// <param name="path">The path.</param>
        /// <param name="options">The options.</param>
        /// <returns>
        /// The <see cref="Image{TPixel}" />.
        /// </returns>
        public static IImageProcessingContext<TPixel> DrawText<TPixel>(this IImageProcessingContext<TPixel> source, string text, Font font, IBrush<TPixel> brush, IPen<TPixel> pen, IPath path, TextGraphicsOptions options)
           where TPixel : struct, IPixel<TPixel>
        {
            float dpiX = DefaultTextDpi;
            float dpiY = DefaultTextDpi;

            var style = new RendererOptions(font, dpiX, dpiY)
            {
                ApplyKerning = options.ApplyKerning,
                TabWidth = options.TabWidth,
                WrappingWidth = options.WrapTextWidth,
                HorizontalAlignment = options.HorizontalAlignment,
                VerticalAlignment = options.VerticalAlignment
            };

            IPathCollection glyphs = TextBuilder.GenerateGlyphs(text, path, style);

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
