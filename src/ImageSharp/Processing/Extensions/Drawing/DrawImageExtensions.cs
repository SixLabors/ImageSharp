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
    /// <param name="foregroundRepeatCount">
    /// The number of times the foreground frames are allowed to loop while applying this operation across successive frames.
    /// A value of 0 means loop indefinitely.
    /// </param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DrawImage(
        this IImageProcessingContext source,
        Image foreground,
        float opacity,
        int foregroundRepeatCount = 0)
    {
        GraphicsOptions options = source.GetGraphicsOptions();
        return DrawImage(source, foreground, options.ColorBlendingMode, options.AlphaCompositionMode, opacity, foregroundRepeatCount);
    }

    /// <summary>
    /// Draws the given image together with the currently processing image by blending their pixels.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="foreground">The image to draw on the currently processing image.</param>
    /// <param name="foregroundRectangle">The rectangle structure that specifies the portion of the image to draw.</param>
    /// <param name="opacity">The opacity of the image to draw. Must be between 0 and 1.</param>
    /// <param name="foregroundRepeatCount">
    /// The number of times the foreground frames are allowed to loop while applying this operation across successive frames.
    /// A value of 0 means loop indefinitely.
    /// </param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DrawImage(
        this IImageProcessingContext source,
        Image foreground,
        Rectangle foregroundRectangle,
        float opacity,
        int foregroundRepeatCount = 0)
    {
        GraphicsOptions options = source.GetGraphicsOptions();
        return DrawImage(source, foreground, foregroundRectangle, options.ColorBlendingMode, options.AlphaCompositionMode, opacity, foregroundRepeatCount);
    }

    /// <summary>
    /// Draws the given image together with the currently processing image by blending their pixels.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="foreground">The image to draw on the currently processing image.</param>
    /// <param name="colorBlending">The color blending mode.</param>
    /// <param name="opacity">The opacity of the image to draw. Must be between 0 and 1.</param>
    /// <param name="foregroundRepeatCount">
    /// The number of times the foreground frames are allowed to loop while applying this operation across successive frames.
    /// A value of 0 means loop indefinitely.
    /// </param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DrawImage(
        this IImageProcessingContext source,
        Image foreground,
        PixelColorBlendingMode colorBlending,
        float opacity,
        int foregroundRepeatCount = 0)
        => DrawImage(source, foreground, Point.Empty, colorBlending, opacity, foregroundRepeatCount);

    /// <summary>
    /// Draws the given image together with the currently processing image by blending their pixels.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="foreground">The image to draw on the currently processing image.</param>
    /// <param name="foregroundRectangle">The rectangle structure that specifies the portion of the image to draw.</param>
    /// <param name="colorBlending">The color blending mode.</param>
    /// <param name="opacity">The opacity of the image to draw. Must be between 0 and 1.</param>
    /// <param name="foregroundRepeatCount">
    /// The number of times the foreground frames are allowed to loop while applying this operation across successive frames.
    /// A value of 0 means loop indefinitely.
    /// </param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DrawImage(
        this IImageProcessingContext source,
        Image foreground,
        Rectangle foregroundRectangle,
        PixelColorBlendingMode colorBlending,
        float opacity,
        int foregroundRepeatCount = 0)
        => DrawImage(source, foreground, Point.Empty, foregroundRectangle, colorBlending, opacity, foregroundRepeatCount);

    /// <summary>
    /// Draws the given image together with the currently processing image by blending their pixels.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="foreground">The image to draw on the currently processing image.</param>
    /// <param name="colorBlending">The color blending mode.</param>
    /// <param name="alphaComposition">The alpha composition mode.</param>
    /// <param name="opacity">The opacity of the image to draw. Must be between 0 and 1.</param>
    /// <param name="foregroundRepeatCount">
    /// The number of times the foreground frames are allowed to loop while applying this operation across successive frames.
    /// A value of 0 means loop indefinitely.
    /// </param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DrawImage(
        this IImageProcessingContext source,
        Image foreground,
        PixelColorBlendingMode colorBlending,
        PixelAlphaCompositionMode alphaComposition,
        float opacity,
        int foregroundRepeatCount = 0)
        => DrawImage(source, foreground, Point.Empty, colorBlending, alphaComposition, opacity, foregroundRepeatCount);

    /// <summary>
    /// Draws the given image together with the currently processing image by blending their pixels.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="foreground">The image to draw on the currently processing image.</param>
    /// <param name="foregroundRectangle">The rectangle structure that specifies the portion of the image to draw.</param>
    /// <param name="colorBlending">The color blending mode.</param>
    /// <param name="alphaComposition">The alpha composition mode.</param>
    /// <param name="opacity">The opacity of the image to draw. Must be between 0 and 1.</param>
    /// <param name="foregroundRepeatCount">
    /// The number of times the foreground frames are allowed to loop while applying this operation across successive frames.
    /// A value of 0 means loop indefinitely.
    /// </param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DrawImage(
        this IImageProcessingContext source,
        Image foreground,
        Rectangle foregroundRectangle,
        PixelColorBlendingMode colorBlending,
        PixelAlphaCompositionMode alphaComposition,
        float opacity,
        int foregroundRepeatCount = 0)
        => DrawImage(source, foreground, Point.Empty, foregroundRectangle, colorBlending, alphaComposition, opacity, foregroundRepeatCount);

    /// <summary>
    /// Draws the given image together with the currently processing image by blending their pixels.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="foreground">The image to draw on the currently processing image.</param>
    /// <param name="options">The options, including the blending type and blending amount.</param>
    /// <param name="foregroundRepeatCount">
    /// The number of times the foreground frames are allowed to loop while applying this operation across successive frames.
    /// A value of 0 means loop indefinitely.
    /// </param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DrawImage(
        this IImageProcessingContext source,
        Image foreground,
        GraphicsOptions options,
        int foregroundRepeatCount = 0)
        => DrawImage(source, foreground, Point.Empty, options, foregroundRepeatCount);

    /// <summary>
    /// Draws the given image together with the currently processing image by blending their pixels.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="foreground">The image to draw on the currently processing image.</param>
    /// <param name="foregroundRectangle">The rectangle structure that specifies the portion of the image to draw.</param>
    /// <param name="options">The options, including the blending type and blending amount.</param>
    /// <param name="foregroundRepeatCount">
    /// The number of times the foreground frames are allowed to loop while applying this operation across successive frames.
    /// A value of 0 means loop indefinitely.
    /// </param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DrawImage(
        this IImageProcessingContext source,
        Image foreground,
        Rectangle foregroundRectangle,
        GraphicsOptions options,
        int foregroundRepeatCount = 0)
        => DrawImage(source, foreground, Point.Empty, foregroundRectangle, options, foregroundRepeatCount);

    /// <summary>
    /// Draws the given image together with the currently processing image by blending their pixels.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="foreground">The image to draw on the currently processing image.</param>
    /// <param name="backgroundLocation">The location on the currently processing image at which to draw.</param>
    /// <param name="opacity">The opacity of the image to draw. Must be between 0 and 1.</param>
    /// <param name="foregroundRepeatCount">
    /// The number of times the foreground frames are allowed to loop while applying this operation across successive frames.
    /// A value of 0 means loop indefinitely.
    /// </param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DrawImage(
        this IImageProcessingContext source,
        Image foreground,
        Point backgroundLocation,
        float opacity,
        int foregroundRepeatCount = 0)
    {
        GraphicsOptions options = source.GetGraphicsOptions();
        return DrawImage(source, foreground, backgroundLocation, options.ColorBlendingMode, options.AlphaCompositionMode, opacity, foregroundRepeatCount);
    }

    /// <summary>
    /// Draws the given image together with the currently processing image by blending their pixels.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="foreground">The image to draw on the currently processing image.</param>
    /// <param name="backgroundLocation">The location on the currently processing image at which to draw.</param>
    /// <param name="foregroundRectangle">The rectangle structure that specifies the portion of the image to draw.</param>
    /// <param name="opacity">The opacity of the image to draw. Must be between 0 and 1.</param>
    /// <param name="foregroundRepeatCount">
    /// The number of times the foreground frames are allowed to loop while applying this operation across successive frames.
    /// A value of 0 means loop indefinitely.
    /// </param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DrawImage(
        this IImageProcessingContext source,
        Image foreground,
        Point backgroundLocation,
        Rectangle foregroundRectangle,
        float opacity,
        int foregroundRepeatCount = 0)
    {
        GraphicsOptions options = source.GetGraphicsOptions();
        return DrawImage(source, foreground, backgroundLocation, foregroundRectangle, options.ColorBlendingMode, options.AlphaCompositionMode, opacity, foregroundRepeatCount);
    }

    /// <summary>
    /// Draws the given image together with the currently processing image by blending their pixels.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="foreground">The image to draw on the currently processing image.</param>
    /// <param name="backgroundLocation">The location on the currently processing image at which to draw.</param>
    /// <param name="colorBlending">The color blending to apply.</param>
    /// <param name="opacity">The opacity of the image to draw. Must be between 0 and 1.</param>
    /// <param name="foregroundRepeatCount">
    /// The number of times the foreground frames are allowed to loop while applying this operation across successive frames.
    /// A value of 0 means loop indefinitely.
    /// </param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DrawImage(
        this IImageProcessingContext source,
        Image foreground,
        Point backgroundLocation,
        PixelColorBlendingMode colorBlending,
        float opacity,
        int foregroundRepeatCount = 0)
        => DrawImage(source, foreground, backgroundLocation, colorBlending, source.GetGraphicsOptions().AlphaCompositionMode, opacity, foregroundRepeatCount);

    /// <summary>
    /// Draws the given image together with the currently processing image by blending their pixels.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="foreground">The image to draw on the currently processing image.</param>
    /// <param name="backgroundLocation">The location on the currently processing image at which to draw.</param>
    /// <param name="foregroundRectangle">The rectangle structure that specifies the portion of the image to draw.</param>
    /// <param name="colorBlending">The color blending to apply.</param>
    /// <param name="opacity">The opacity of the image to draw. Must be between 0 and 1.</param>
    /// <param name="foregroundRepeatCount">
    /// The number of times the foreground frames are allowed to loop while applying this operation across successive frames.
    /// A value of 0 means loop indefinitely.
    /// </param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DrawImage(
        this IImageProcessingContext source,
        Image foreground,
        Point backgroundLocation,
        Rectangle foregroundRectangle,
        PixelColorBlendingMode colorBlending,
        float opacity,
        int foregroundRepeatCount = 0)
        => DrawImage(source, foreground, backgroundLocation, foregroundRectangle, colorBlending, source.GetGraphicsOptions().AlphaCompositionMode, opacity, foregroundRepeatCount);

    /// <summary>
    /// Draws the given image together with the currently processing image by blending their pixels.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="foreground">The image to draw on the currently processing image.</param>
    /// <param name="backgroundLocation">The location on the currently processing image at which to draw.</param>
    /// <param name="options">The options containing the blend mode and opacity.</param>
    /// <param name="foregroundRepeatCount">
    /// The number of times the foreground frames are allowed to loop while applying this operation across successive frames.
    /// A value of 0 means loop indefinitely.
    /// </param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DrawImage(
        this IImageProcessingContext source,
        Image foreground,
        Point backgroundLocation,
        GraphicsOptions options,
        int foregroundRepeatCount = 0)
        => DrawImage(source, foreground, backgroundLocation, options.ColorBlendingMode, options.AlphaCompositionMode, options.BlendPercentage, foregroundRepeatCount);

    /// <summary>
    /// Draws the given image together with the currently processing image by blending their pixels.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="foreground">The image to draw on the currently processing image.</param>
    /// <param name="backgroundLocation">The location on the currently processing image at which to draw.</param>
    /// <param name="foregroundRectangle">The rectangle structure that specifies the portion of the image to draw.</param>
    /// <param name="options">The options containing the blend mode and opacity.</param>
    /// <param name="foregroundRepeatCount">
    /// The number of times the foreground frames are allowed to loop while applying this operation across successive frames.
    /// A value of 0 means loop indefinitely.
    /// </param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DrawImage(
        this IImageProcessingContext source,
        Image foreground,
        Point backgroundLocation,
        Rectangle foregroundRectangle,
        GraphicsOptions options,
        int foregroundRepeatCount = 0)
        => DrawImage(source, foreground, backgroundLocation, foregroundRectangle, options.ColorBlendingMode, options.AlphaCompositionMode, options.BlendPercentage, foregroundRepeatCount);

    /// <summary>
    /// Draws the given image together with the currently processing image by blending their pixels.
    /// </summary>
    /// <param name="source">The current image processing context.</param>
    /// <param name="foreground">The image to draw on the currently processing image.</param>
    /// <param name="backgroundLocation">The location on the currently processing image at which to draw.</param>
    /// <param name="colorBlending">The color blending to apply.</param>
    /// <param name="alphaComposition">The alpha composition mode.</param>
    /// <param name="opacity">The opacity of the image to draw. Must be between 0 and 1.</param>
    /// <param name="foregroundRepeatCount">
    /// The number of times the foreground frames are allowed to loop while applying this operation across successive frames.
    /// A value of 0 means loop indefinitely.
    /// </param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DrawImage(
        this IImageProcessingContext source,
        Image foreground,
        Point backgroundLocation,
        PixelColorBlendingMode colorBlending,
        PixelAlphaCompositionMode alphaComposition,
        float opacity,
        int foregroundRepeatCount = 0)
        => source.ApplyProcessor(new DrawImageProcessor(foreground, backgroundLocation, foreground.Bounds, colorBlending, alphaComposition, opacity, foregroundRepeatCount));

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
    /// <param name="foregroundRepeatCount">
    /// The number of times the foreground frames are allowed to loop while applying this operation across successive frames.
    /// A value of 0 means loop indefinitely.
    /// </param>
    /// <returns>The <see cref="IImageProcessingContext"/>.</returns>
    public static IImageProcessingContext DrawImage(
        this IImageProcessingContext source,
        Image foreground,
        Point backgroundLocation,
        Rectangle foregroundRectangle,
        PixelColorBlendingMode colorBlending,
        PixelAlphaCompositionMode alphaComposition,
        float opacity,
        int foregroundRepeatCount = 0) =>
        source.ApplyProcessor(
            new DrawImageProcessor(foreground, backgroundLocation, foregroundRectangle, colorBlending, alphaComposition, opacity, foregroundRepeatCount),
            foregroundRectangle);
}
