// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Drawing;

namespace SixLabors.ImageSharp.Processing;

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
        GraphicsOptions options = source.GetGraphicsOptions();
        return DrawImage(source, image, options.ColorBlendingMode, options.AlphaCompositionMode, opacity);
    }

    /// <summary>
    /// Draws the given image together with the current one by blending their pixels.
    /// </summary>
    /// <param name="source">The image this method extends.</param>
    /// <param name="image">The image to blend with the currently processing image.</param>
    /// <param name="rectangle">The <see cref="Rectangle"/> structure that specifies the portion of the image to draw.</param>
    /// <param name="opacity">The opacity of the image to blend. Must be between 0 and 1.</param>
    /// <returns>The <see cref="Image{TPixelDst}"/>.</returns>
    public static IImageProcessingContext DrawImage(
        this IImageProcessingContext source,
        Image image,
        Rectangle rectangle,
        float opacity)
    {
        GraphicsOptions options = source.GetGraphicsOptions();
        return DrawImage(source, image, rectangle, options.ColorBlendingMode, options.AlphaCompositionMode, opacity);
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
        float opacity)
        => DrawImage(source, image, Point.Empty, colorBlending, opacity);

    /// <summary>
    /// Draws the given image together with the current one by blending their pixels.
    /// </summary>
    /// <param name="source">The image this method extends.</param>
    /// <param name="image">The image to blend with the currently processing image.</param>
    /// <param name="rectangle">The <see cref="Rectangle"/> structure that specifies the portion of the image to draw.</param>
    /// <param name="colorBlending">The blending mode.</param>
    /// <param name="opacity">The opacity of the image to blend. Must be between 0 and 1.</param>
    /// <returns>The <see cref="Image{TPixelDst}"/>.</returns>
    public static IImageProcessingContext DrawImage(
        this IImageProcessingContext source,
        Image image,
        Rectangle rectangle,
        PixelColorBlendingMode colorBlending,
        float opacity)
        => DrawImage(source, image, rectangle, colorBlending, source.GetGraphicsOptions().AlphaCompositionMode, opacity);

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
        float opacity)
        => DrawImage(source, image, Point.Empty, colorBlending, alphaComposition, opacity);

    /// <summary>
    /// Draws the given image together with the current one by blending their pixels.
    /// </summary>
    /// <param name="source">The image this method extends.</param>
    /// <param name="image">The image to blend with the currently processing image.</param>
    /// <param name="rectangle">The <see cref="Rectangle"/> structure that specifies the portion of the image to draw.</param>
    /// <param name="colorBlending">The color blending mode.</param>
    /// <param name="alphaComposition">The alpha composition mode.</param>
    /// <param name="opacity">The opacity of the image to blend. Must be between 0 and 1.</param>
    /// <returns>The <see cref="Image{TPixelDst}"/>.</returns>
    public static IImageProcessingContext DrawImage(
        this IImageProcessingContext source,
        Image image,
        Rectangle rectangle,
        PixelColorBlendingMode colorBlending,
        PixelAlphaCompositionMode alphaComposition,
        float opacity)
        => DrawImage(source, image, Point.Empty, rectangle, colorBlending, alphaComposition, opacity);

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
        GraphicsOptions options)
        => DrawImage(source, image, Point.Empty, options);

    /// <summary>
    /// Draws the given image together with the current one by blending their pixels.
    /// </summary>
    /// <param name="source">The image this method extends.</param>
    /// <param name="image">The image to blend with the currently processing image.</param>
    /// <param name="rectangle">The <see cref="Rectangle"/> structure that specifies the portion of the image to draw.</param>
    /// <param name="options">The options, including the blending type and blending amount.</param>
    /// <returns>The <see cref="Image{TPixelDst}"/>.</returns>
    public static IImageProcessingContext DrawImage(
        this IImageProcessingContext source,
        Image image,
        Rectangle rectangle,
        GraphicsOptions options)
        => DrawImage(source, image, Point.Empty, rectangle, options);

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
        GraphicsOptions options = source.GetGraphicsOptions();
        return DrawImage(source, image, location, options.ColorBlendingMode, options.AlphaCompositionMode, opacity);
    }

    /// <summary>
    /// Draws the given image together with the current one by blending their pixels.
    /// </summary>
    /// <param name="source">The image this method extends.</param>
    /// <param name="image">The image to blend with the currently processing image.</param>
    /// <param name="location">The location to draw the blended image.</param>
    /// <param name="rectangle">The <see cref="Rectangle"/> structure that specifies the portion of the image to draw.</param>
    /// <param name="opacity">The opacity of the image to blend. Must be between 0 and 1.</param>
    /// <returns>The <see cref="Image{TPixelDst}"/>.</returns>
    public static IImageProcessingContext DrawImage(
        this IImageProcessingContext source,
        Image image,
        Point location,
        Rectangle rectangle,
        float opacity)
    {
        GraphicsOptions options = source.GetGraphicsOptions();
        return DrawImage(source, image, location, rectangle, options.ColorBlendingMode, options.AlphaCompositionMode, opacity);
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
        float opacity)
        => DrawImage(source, image, location, colorBlending, source.GetGraphicsOptions().AlphaCompositionMode, opacity);

    /// <summary>
    /// Draws the given image together with the current one by blending their pixels.
    /// </summary>
    /// <param name="source">The image this method extends.</param>
    /// <param name="image">The image to blend with the currently processing image.</param>
    /// <param name="location">The location to draw the blended image.</param>
    /// <param name="rectangle">The <see cref="Rectangle"/> structure that specifies the portion of the image to draw.</param>
    /// <param name="colorBlending">The color blending to apply.</param>
    /// <param name="opacity">The opacity of the image to blend. Must be between 0 and 1.</param>
    /// <returns>The <see cref="Image{TPixelDst}"/>.</returns>
    public static IImageProcessingContext DrawImage(
        this IImageProcessingContext source,
        Image image,
        Point location,
        Rectangle rectangle,
        PixelColorBlendingMode colorBlending,
        float opacity)
        => DrawImage(source, image, location, rectangle, colorBlending, source.GetGraphicsOptions().AlphaCompositionMode, opacity);

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
        GraphicsOptions options)
        => DrawImage(source, image, location, options.ColorBlendingMode, options.AlphaCompositionMode, options.BlendPercentage);

    /// <summary>
    /// Draws the given image together with the current one by blending their pixels.
    /// </summary>
    /// <param name="source">The image this method extends.</param>
    /// <param name="image">The image to blend with the currently processing image.</param>
    /// <param name="location">The location to draw the blended image.</param>
    /// <param name="rectangle">The <see cref="Rectangle"/> structure that specifies the portion of the image to draw.</param>
    /// <param name="options">The options containing the blend mode and opacity.</param>
    /// <returns>The <see cref="Image{TPixelDst}"/>.</returns>
    public static IImageProcessingContext DrawImage(
        this IImageProcessingContext source,
        Image image,
        Point location,
        Rectangle rectangle,
        GraphicsOptions options)
        => DrawImage(source, image, location, rectangle, options.ColorBlendingMode, options.AlphaCompositionMode, options.BlendPercentage);

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
        float opacity)
        => source.ApplyProcessor(new DrawImageProcessor(image, location, colorBlending, alphaComposition, opacity));

    /// <summary>
    /// Draws the given image together with the current one by blending their pixels.
    /// </summary>
    /// <param name="source">The image this method extends.</param>
    /// <param name="image">The image to blend with the currently processing image.</param>
    /// <param name="location">The location to draw the blended image.</param>
    /// <param name="rectangle">The <see cref="Rectangle"/> structure that specifies the portion of the image to draw.</param>
    /// <param name="colorBlending">The color blending to apply.</param>
    /// <param name="alphaComposition">The alpha composition mode.</param>
    /// <param name="opacity">The opacity of the image to blend. Must be between 0 and 1.</param>
    /// <returns>The <see cref="Image{TPixelDst}"/>.</returns>
    public static IImageProcessingContext DrawImage(
        this IImageProcessingContext source,
        Image image,
        Point location,
        Rectangle rectangle,
        PixelColorBlendingMode colorBlending,
        PixelAlphaCompositionMode alphaComposition,
        float opacity) =>
        source.ApplyProcessor(
            new DrawImageProcessor(image, location, colorBlending, alphaComposition, opacity),
            rectangle);
}
