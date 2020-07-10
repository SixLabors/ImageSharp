// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Drawing;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Adds extensions that allow the drawing of images to the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static class DrawImageExtensions
    {
        /// <summary>
        /// Draws the given image together with the current one by blending their pixels.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="image">The image to blend with the currently processing image.</param>
        /// <param name="opacity">The opacity of the image to blend. Must be between 0 and 1.</param>
        /// <returns>The <see cref="Image{TPixelDst}"/>.</returns>
        public static IImageProcessingContext DrawImage(
            this IImageProcessingContext source,
            Image image,
            float opacity)
        {
            var options = source.GetGraphicsOptions();
            return source.ApplyProcessor(
                new DrawImageProcessor(
                image,
                Point.Empty,
                options.ColorBlendingMode,
                options.AlphaCompositionMode,
                opacity));
        }

        /// <summary>
        /// Draws the given image together with the current one by blending their pixels.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="image">The image to blend with the currently processing image.</param>
        /// <param name="colorBlending">The blending mode.</param>
        /// <param name="opacity">The opacity of the image to blend. Must be between 0 and 1.</param>
        /// <returns>The <see cref="Image{TPixelDst}"/>.</returns>
        public static IImageProcessingContext DrawImage(
            this IImageProcessingContext source,
            Image image,
            PixelColorBlendingMode colorBlending,
            float opacity) =>
            source.ApplyProcessor(
                new DrawImageProcessor(
                    image,
                    Point.Empty,
                    colorBlending,
                    source.GetGraphicsOptions().AlphaCompositionMode,
                    opacity));

        /// <summary>
        /// Draws the given image together with the current one by blending their pixels.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="image">The image to blend with the currently processing image.</param>
        /// <param name="colorBlending">The color blending mode.</param>
        /// <param name="alphaComposition">The alpha composition mode.</param>
        /// <param name="opacity">The opacity of the image to blend. Must be between 0 and 1.</param>
        /// <returns>The <see cref="Image{TPixelDst}"/>.</returns>
        public static IImageProcessingContext DrawImage(
            this IImageProcessingContext source,
            Image image,
            PixelColorBlendingMode colorBlending,
            PixelAlphaCompositionMode alphaComposition,
            float opacity) =>
            source.ApplyProcessor(new DrawImageProcessor(image, Point.Empty, colorBlending, alphaComposition, opacity));

        /// <summary>
        /// Draws the given image together with the current one by blending their pixels.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="image">The image to blend with the currently processing image.</param>
        /// <param name="options">The options, including the blending type and blending amount.</param>
        /// <returns>The <see cref="Image{TPixelDst}"/>.</returns>
        public static IImageProcessingContext DrawImage(
            this IImageProcessingContext source,
            Image image,
            GraphicsOptions options) =>
            source.ApplyProcessor(
                new DrawImageProcessor(
                    image,
                    Point.Empty,
                    options.ColorBlendingMode,
                    options.AlphaCompositionMode,
                    options.BlendPercentage));

        /// <summary>
        /// Draws the given image together with the current one by blending their pixels.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="image">The image to blend with the currently processing image.</param>
        /// <param name="location">The location to draw the blended image.</param>
        /// <param name="opacity">The opacity of the image to blend. Must be between 0 and 1.</param>
        /// <returns>The <see cref="Image{TPixelDst}"/>.</returns>
        public static IImageProcessingContext DrawImage(
            this IImageProcessingContext source,
            Image image,
            Point location,
            float opacity)
        {
            var options = source.GetGraphicsOptions();
            return source.ApplyProcessor(
                new DrawImageProcessor(
                image,
                location,
                options.ColorBlendingMode,
                options.AlphaCompositionMode,
                opacity));
        }

        /// <summary>
        /// Draws the given image together with the current one by blending their pixels.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="image">The image to blend with the currently processing image.</param>
        /// <param name="location">The location to draw the blended image.</param>
        /// <param name="colorBlending">The color blending to apply.</param>
        /// <param name="opacity">The opacity of the image to blend. Must be between 0 and 1.</param>
        /// <returns>The <see cref="Image{TPixelDst}"/>.</returns>
        public static IImageProcessingContext DrawImage(
            this IImageProcessingContext source,
            Image image,
            Point location,
            PixelColorBlendingMode colorBlending,
            float opacity) =>
            source.ApplyProcessor(
                new DrawImageProcessor(
                    image,
                    location,
                    colorBlending,
                    source.GetGraphicsOptions().AlphaCompositionMode,
                    opacity));

        /// <summary>
        /// Draws the given image together with the current one by blending their pixels.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="image">The image to blend with the currently processing image.</param>
        /// <param name="location">The location to draw the blended image.</param>
        /// <param name="colorBlending">The color blending to apply.</param>
        /// <param name="alphaComposition">The alpha composition mode.</param>
        /// <param name="opacity">The opacity of the image to blend. Must be between 0 and 1.</param>
        /// <returns>The <see cref="Image{TPixelDst}"/>.</returns>
        public static IImageProcessingContext DrawImage(
            this IImageProcessingContext source,
            Image image,
            Point location,
            PixelColorBlendingMode colorBlending,
            PixelAlphaCompositionMode alphaComposition,
            float opacity) =>
            source.ApplyProcessor(new DrawImageProcessor(image, location, colorBlending, alphaComposition, opacity));

        /// <summary>
        /// Draws the given image together with the current one by blending their pixels.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="image">The image to blend with the currently processing image.</param>
        /// <param name="location">The location to draw the blended image.</param>
        /// <param name="options">The options containing the blend mode and opacity.</param>
        /// <returns>The <see cref="Image{TPixelDst}"/>.</returns>
        public static IImageProcessingContext DrawImage(
            this IImageProcessingContext source,
            Image image,
            Point location,
            GraphicsOptions options) =>
            source.ApplyProcessor(
                new DrawImageProcessor(
                    image,
                    location,
                    options.ColorBlendingMode,
                    options.AlphaCompositionMode,
                    options.BlendPercentage));
    }
}
