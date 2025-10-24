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
    /// Draws the given image together with the currently processing image by blending their pixels.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="foreground">The image to draw on the currently processing image.</param>
    /// <param name="opacity">The opacity of the image to draw. Must be between 0 and 1.</param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DrawImage(
        this IImageProcessingContext source,
        Image foreground,
        float opacity,
        int repeatCount)
    {
        GraphicsOptions options = source.GetGraphicsOptions();
        return DrawImage(source, foreground, options.ColorBlendingMode, options.AlphaCompositionMode, opacity, repeatCount);
    }

    /// <summary>
    /// Draws the given image together with the currently processing image by blending their pixels.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="foreground">The image to draw on the currently processing image.</param>
    /// <param name="foregroundRectangle">The rectangle structure that specifies the portion of the image to draw.</param>
    /// <param name="opacity">The opacity of the image to draw. Must be between 0 and 1.</param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DrawImage(
        this IImageProcessingContext source,
        Image foreground,
        Rectangle foregroundRectangle,
        float opacity,
        int repeatCount)
    {
        GraphicsOptions options = source.GetGraphicsOptions();
        return DrawImage(source, foreground, foregroundRectangle, options.ColorBlendingMode, options.AlphaCompositionMode, opacity, repeatCount);
    }

    /// <summary>
    /// Draws the given image together with the currently processing image by blending their pixels.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="foreground">The image to draw on the currently processing image.</param>
    /// <param name="colorBlending">The color blending mode.</param>
    /// <param name="opacity">The opacity of the image to draw. Must be between 0 and 1.</param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DrawImage(
        this IImageProcessingContext source,
        Image foreground,
        PixelColorBlendingMode colorBlending,
        float opacity,
        int repeatCount)
        => DrawImage(source, foreground, Point.Empty, colorBlending, opacity, repeatCount);

    /// <summary>
    /// Draws the given image together with the currently processing image by blending their pixels.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="foreground">The image to draw on the currently processing image.</param>
    /// <param name="foregroundRectangle">The rectangle structure that specifies the portion of the image to draw.</param>
    /// <param name="colorBlending">The color blending mode.</param>
    /// <param name="opacity">The opacity of the image to draw. Must be between 0 and 1.</param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DrawImage(
        this IImageProcessingContext source,
        Image foreground,
        Rectangle foregroundRectangle,
        PixelColorBlendingMode colorBlending,
        float opacity,
        int repeatCount)
        => DrawImage(source, foreground, Point.Empty, foregroundRectangle, colorBlending, opacity, repeatCount);

    /// <summary>
    /// Draws the given image together with the currently processing image by blending their pixels.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="foreground">The image to draw on the currently processing image.</param>
    /// <param name="colorBlending">The color blending mode.</param>
    /// <param name="alphaComposition">The alpha composition mode.</param>
    /// <param name="opacity">The opacity of the image to draw. Must be between 0 and 1.</param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DrawImage(
        this IImageProcessingContext source,
        Image foreground,
        PixelColorBlendingMode colorBlending,
        PixelAlphaCompositionMode alphaComposition,
        float opacity,
        int repeatCount)
        => DrawImage(source, foreground, Point.Empty, colorBlending, alphaComposition, opacity, repeatCount);

    /// <summary>
    /// Draws the given image together with the currently processing image by blending their pixels.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="foreground">The image to draw on the currently processing image.</param>
    /// <param name="foregroundRectangle">The rectangle structure that specifies the portion of the image to draw.</param>
    /// <param name="colorBlending">The color blending mode.</param>
    /// <param name="alphaComposition">The alpha composition mode.</param>
    /// <param name="opacity">The opacity of the image to draw. Must be between 0 and 1.</param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DrawImage(
        this IImageProcessingContext source,
        Image foreground,
        Rectangle foregroundRectangle,
        PixelColorBlendingMode colorBlending,
        PixelAlphaCompositionMode alphaComposition,
        float opacity,
        int repeatCount)
        => DrawImage(source, foreground, Point.Empty, foregroundRectangle, colorBlending, alphaComposition, opacity, repeatCount);

    /// <summary>
    /// Draws the given image together with the currently processing image by blending their pixels.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="foreground">The image to draw on the currently processing image.</param>
    /// <param name="options">The options, including the blending type and blending amount.</param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DrawImage(
        this IImageProcessingContext source,
        Image foreground,
        GraphicsOptions options,
        int repeatCount)
        => DrawImage(source, foreground, Point.Empty, options, repeatCount);

    /// <summary>
    /// Draws the given image together with the currently processing image by blending their pixels.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="foreground">The image to draw on the currently processing image.</param>
    /// <param name="foregroundRectangle">The rectangle structure that specifies the portion of the image to draw.</param>
    /// <param name="options">The options, including the blending type and blending amount.</param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DrawImage(
        this IImageProcessingContext source,
        Image foreground,
        Rectangle foregroundRectangle,
        GraphicsOptions options,
        int repeatCount)
        => DrawImage(source, foreground, Point.Empty, foregroundRectangle, options, repeatCount);

    /// <summary>
    /// Draws the given image together with the currently processing image by blending their pixels.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="foreground">The image to draw on the currently processing image.</param>
    /// <param name="backgroundLocation">The location on the currently processing image at which to draw.</param>
    /// <param name="opacity">The opacity of the image to draw. Must be between 0 and 1.</param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DrawImage(
        this IImageProcessingContext source,
        Image foreground,
        Point backgroundLocation,
        float opacity,
        int repeatCount)
    {
        GraphicsOptions options = source.GetGraphicsOptions();
        return DrawImage(source, foreground, backgroundLocation, options.ColorBlendingMode, options.AlphaCompositionMode, opacity, repeatCount);
    }

    /// <summary>
    /// Draws the given image together with the currently processing image by blending their pixels.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="foreground">The image to draw on the currently processing image.</param>
    /// <param name="backgroundLocation">The location on the currently processing image at which to draw.</param>
    /// <param name="foregroundRectangle">The rectangle structure that specifies the portion of the image to draw.</param>
    /// <param name="opacity">The opacity of the image to draw. Must be between 0 and 1.</param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DrawImage(
        this IImageProcessingContext source,
        Image foreground,
        Point backgroundLocation,
        Rectangle foregroundRectangle,
        float opacity,
        int repeatCount)
    {
        GraphicsOptions options = source.GetGraphicsOptions();
        return DrawImage(source, foreground, backgroundLocation, foregroundRectangle, options.ColorBlendingMode, options.AlphaCompositionMode, opacity, repeatCount);
    }

    /// <summary>
    /// Draws the given image together with the currently processing image by blending their pixels.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="foreground">The image to draw on the currently processing image.</param>
    /// <param name="backgroundLocation">The location on the currently processing image at which to draw.</param>
    /// <param name="colorBlending">The color blending to apply.</param>
    /// <param name="opacity">The opacity of the image to draw. Must be between 0 and 1.</param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DrawImage(
        this IImageProcessingContext source,
        Image foreground,
        Point backgroundLocation,
        PixelColorBlendingMode colorBlending,
        float opacity,
        int repeatCount)
        => DrawImage(source, foreground, backgroundLocation, colorBlending, source.GetGraphicsOptions().AlphaCompositionMode, opacity, repeatCount);

    /// <summary>
    /// Draws the given image together with the currently processing image by blending their pixels.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="foreground">The image to draw on the currently processing image.</param>
    /// <param name="backgroundLocation">The location on the currently processing image at which to draw.</param>
    /// <param name="foregroundRectangle">The rectangle structure that specifies the portion of the image to draw.</param>
    /// <param name="colorBlending">The color blending to apply.</param>
    /// <param name="opacity">The opacity of the image to draw. Must be between 0 and 1.</param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DrawImage(
        this IImageProcessingContext source,
        Image foreground,
        Point backgroundLocation,
        Rectangle foregroundRectangle,
        PixelColorBlendingMode colorBlending,
        float opacity,
        int repeatCount)
        => DrawImage(source, foreground, backgroundLocation, foregroundRectangle, colorBlending, source.GetGraphicsOptions().AlphaCompositionMode, opacity, repeatCount);

    /// <summary>
    /// Draws the given image together with the currently processing image by blending their pixels.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="foreground">The image to draw on the currently processing image.</param>
    /// <param name="backgroundLocation">The location on the currently processing image at which to draw.</param>
    /// <param name="options">The options containing the blend mode and opacity.</param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DrawImage(
        this IImageProcessingContext source,
        Image foreground,
        Point backgroundLocation,
        GraphicsOptions options,
        int repeatCount)
        => DrawImage(source, foreground, backgroundLocation, options.ColorBlendingMode, options.AlphaCompositionMode, options.BlendPercentage, repeatCount);

    /// <summary>
    /// Draws the given image together with the currently processing image by blending their pixels.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="foreground">The image to draw on the currently processing image.</param>
    /// <param name="backgroundLocation">The location on the currently processing image at which to draw.</param>
    /// <param name="foregroundRectangle">The rectangle structure that specifies the portion of the image to draw.</param>
    /// <param name="options">The options containing the blend mode and opacity.</param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DrawImage(
        this IImageProcessingContext source,
        Image foreground,
        Point backgroundLocation,
        Rectangle foregroundRectangle,
        GraphicsOptions options,
        int repeatCount)
        => DrawImage(source, foreground, backgroundLocation, foregroundRectangle, options.ColorBlendingMode, options.AlphaCompositionMode, options.BlendPercentage, repeatCount);

    /// <summary>
    /// Draws the given image together with the currently processing image by blending their pixels.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="foreground">The image to draw on the currently processing image.</param>
    /// <param name="backgroundLocation">The location on the currently processing image at which to draw.</param>
    /// <param name="colorBlending">The color blending to apply.</param>
    /// <param name="alphaComposition">The alpha composition mode.</param>
    /// <param name="opacity">The opacity of the image to draw. Must be between 0 and 1.</param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DrawImage(
        this IImageProcessingContext source,
        Image foreground,
        Point backgroundLocation,
        PixelColorBlendingMode colorBlending,
        PixelAlphaCompositionMode alphaComposition,
        float opacity,
        int repeatCount)
        => source.ApplyProcessor(new DrawImageProcessor(foreground, backgroundLocation, foreground.Bounds, colorBlending, alphaComposition, opacity, repeatCount));

    /// <summary>
    /// Draws the given image together with the currently processing image by blending their pixels.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="foreground">The image to draw on the currently processing image.</param>
    /// <param name="backgroundLocation">The location on the currently processing image at which to draw.</param>
    /// <param name="foregroundRectangle">The rectangle structure that specifies the portion of the image to draw.</param>
    /// <param name="colorBlending">The color blending to apply.</param>
    /// <param name="alphaComposition">The alpha composition mode.</param>
    /// <param name="opacity">The opacity of the image to draw. Must be between 0 and 1.</param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DrawImage(
        this IImageProcessingContext source,
        Image foreground,
        Point backgroundLocation,
        Rectangle foregroundRectangle,
        PixelColorBlendingMode colorBlending,
        PixelAlphaCompositionMode alphaComposition,
        float opacity,
        int repeatCount) =>
        source.ApplyProcessor(
            new DrawImageProcessor(foreground, backgroundLocation, foregroundRectangle, colorBlending, alphaComposition, opacity, repeatCount),
            foregroundRectangle);
}
