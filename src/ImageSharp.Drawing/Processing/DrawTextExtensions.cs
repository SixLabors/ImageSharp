// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.Fonts;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Text;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Adds extensions that allow the drawing of text to the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static class DrawTextExtensions
    {
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
        public static IImageProcessingContext<TPixel> DrawText<TPixel>(this IImageProcessingContext<TPixel> source, string text, Font font, TPixel color, PointF location)
            where TPixel : struct, IPixel<TPixel>
            => source.DrawText(TextGraphicsOptions.Default, text, font, color, location);

        /// <summary>
        /// Draws the text onto the the image filled via the brush.
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="options">The options.</param>
        /// <param name="text">The text.</param>
        /// <param name="font">The font.</param>
        /// <param name="color">The color.</param>
        /// <param name="location">The location.</param>
        /// <returns>
        /// The <see cref="Image{TPixel}" />.
        /// </returns>
        public static IImageProcessingContext<TPixel> DrawText<TPixel>(this IImageProcessingContext<TPixel> source, TextGraphicsOptions options, string text, Font font, TPixel color, PointF location)
            where TPixel : struct, IPixel<TPixel>
            => source.DrawText(options, text, font, Brushes.Solid(color), null, location);

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
        public static IImageProcessingContext<TPixel> DrawText<TPixel>(this IImageProcessingContext<TPixel> source, string text, Font font, IBrush<TPixel> brush, PointF location)
            where TPixel : struct, IPixel<TPixel>
            => source.DrawText(TextGraphicsOptions.Default, text, font, brush, location);

        /// <summary>
        /// Draws the text onto the the image filled via the brush.
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="options">The options.</param>
        /// <param name="text">The text.</param>
        /// <param name="font">The font.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="location">The location.</param>
        /// <returns>
        /// The <see cref="Image{TPixel}" />.
        /// </returns>
        public static IImageProcessingContext<TPixel> DrawText<TPixel>(this IImageProcessingContext<TPixel> source, TextGraphicsOptions options, string text, Font font, IBrush<TPixel> brush, PointF location)
            where TPixel : struct, IPixel<TPixel>
            => source.DrawText(options, text, font, brush, null, location);

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
        public static IImageProcessingContext<TPixel> DrawText<TPixel>(this IImageProcessingContext<TPixel> source, string text, Font font, IPen<TPixel> pen, PointF location)
            where TPixel : struct, IPixel<TPixel>
            => source.DrawText(TextGraphicsOptions.Default, text, font, pen, location);

        /// <summary>
        /// Draws the text onto the the image outlined via the pen.
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="options">The options.</param>
        /// <param name="text">The text.</param>
        /// <param name="font">The font.</param>
        /// <param name="pen">The pen.</param>
        /// <param name="location">The location.</param>
        /// <returns>
        /// The <see cref="Image{TPixel}" />.
        /// </returns>
        public static IImageProcessingContext<TPixel> DrawText<TPixel>(this IImageProcessingContext<TPixel> source, TextGraphicsOptions options, string text, Font font, IPen<TPixel> pen, PointF location)
            where TPixel : struct, IPixel<TPixel>
            => source.DrawText(options, text, font, null, pen, location);

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
        public static IImageProcessingContext<TPixel> DrawText<TPixel>(this IImageProcessingContext<TPixel> source, string text, Font font, IBrush<TPixel> brush, IPen<TPixel> pen, PointF location)
            where TPixel : struct, IPixel<TPixel>
            => source.DrawText(TextGraphicsOptions.Default, text, font, brush, pen, location);

        /// <summary>
        /// Draws the text using the default resolution of <value>72dpi</value> onto the the image filled via the brush then outlined via the pen.
        /// </summary>
        /// <typeparam name="TPixel">The type of the color.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="options">The options.</param>
        /// <param name="text">The text.</param>
        /// <param name="font">The font.</param>
        /// <param name="brush">The brush.</param>
        /// <param name="pen">The pen.</param>
        /// <param name="location">The location.</param>
        /// <returns>
        /// The <see cref="Image{TPixel}" />.
        /// </returns>
        public static IImageProcessingContext<TPixel> DrawText<TPixel>(this IImageProcessingContext<TPixel> source, TextGraphicsOptions options, string text, Font font, IBrush<TPixel> brush, IPen<TPixel> pen, PointF location)
           where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(new DrawTextProcessor<TPixel>(options, text, font, brush, pen, location));
    }
}