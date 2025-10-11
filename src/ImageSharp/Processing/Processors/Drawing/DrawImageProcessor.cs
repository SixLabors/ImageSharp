// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Drawing;

/// <summary>
/// Combines two images together by blending the pixels.
/// </summary>
public class DrawImageProcessor : IImageProcessor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DrawImageProcessor"/> class.
    /// </summary>
    /// <param name="foreground">The image to blend.</param>
    /// <param name="backgroundLocation">The location to draw the foreground image on the background.</param>
    /// <param name="colorBlendingMode">The blending mode to use when drawing the image.</param>
    /// <param name="alphaCompositionMode">The Alpha blending mode to use when drawing the image.</param>
    /// <param name="opacity">The opacity of the image to blend.</param>
    /// <param name="repeatCount">The loop count. The number of times to loop the animation. 0 means infinitely.</param>
    public DrawImageProcessor(
        Image foreground,
        Point backgroundLocation,
        PixelColorBlendingMode colorBlendingMode,
        PixelAlphaCompositionMode alphaCompositionMode,
        float opacity,
        int repeatCount)
        : this(foreground, backgroundLocation, foreground.Bounds, colorBlendingMode, alphaCompositionMode, opacity, repeatCount)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DrawImageProcessor"/> class.
    /// </summary>
    /// <param name="foreground">The image to blend.</param>
    /// <param name="backgroundLocation">The location to draw the foreground image on the background.</param>
    /// <param name="foregroundRectangle">The rectangular portion of the foreground image to draw.</param>
    /// <param name="colorBlendingMode">The blending mode to use when drawing the image.</param>
    /// <param name="alphaCompositionMode">The Alpha blending mode to use when drawing the image.</param>
    /// <param name="opacity">The opacity of the image to blend.</param>
    /// <param name="repeatCount">The loop count. The number of times to loop the animation. 0 means infinitely.</param>
    public DrawImageProcessor(
        Image foreground,
        Point backgroundLocation,
        Rectangle foregroundRectangle,
        PixelColorBlendingMode colorBlendingMode,
        PixelAlphaCompositionMode alphaCompositionMode,
        float opacity,
        int repeatCount)
    {
        this.ForeGround = foreground;
        this.BackgroundLocation = backgroundLocation;
        this.ForegroundRectangle = foregroundRectangle;
        this.ColorBlendingMode = colorBlendingMode;
        this.AlphaCompositionMode = alphaCompositionMode;
        this.Opacity = opacity;
        this.RepeatCount = repeatCount;
    }

    /// <summary>
    /// Gets the image to blend.
    /// </summary>
    public Image ForeGround { get; }

    /// <summary>
    /// Gets the location to draw the foreground image on the background.
    /// </summary>
    public Point BackgroundLocation { get; }

    /// <summary>
    /// Gets the rectangular portion of the foreground image to draw.
    /// </summary>
    public Rectangle ForegroundRectangle { get; }

    /// <summary>
    /// Gets the blending mode to use when drawing the image.
    /// </summary>
    public PixelColorBlendingMode ColorBlendingMode { get; }

    /// <summary>
    /// Gets the Alpha blending mode to use when drawing the image.
    /// </summary>
    public PixelAlphaCompositionMode AlphaCompositionMode { get; }

    /// <summary>
    /// Gets the opacity of the image to blend.
    /// </summary>
    public float Opacity { get; }

    /// <summary>
    /// Gets the loop count. The number of times to loop the animation. 0 means infinitely.
    /// </summary>
    public int RepeatCount { get; }

    /// <inheritdoc />
    public IImageProcessor<TPixelBg> CreatePixelSpecificProcessor<TPixelBg>(Configuration configuration, Image<TPixelBg> source, Rectangle sourceRectangle)
        where TPixelBg : unmanaged, IPixel<TPixelBg>
    {
        ProcessorFactoryVisitor<TPixelBg> visitor = new(configuration, this, source);
        this.ForeGround.AcceptVisitor(visitor);
        return visitor.Result!;
    }

    private class ProcessorFactoryVisitor<TPixelBg> : IImageVisitor
        where TPixelBg : unmanaged, IPixel<TPixelBg>
    {
        private readonly Configuration configuration;
        private readonly DrawImageProcessor definition;
        private readonly Image<TPixelBg> source;

        public ProcessorFactoryVisitor(
            Configuration configuration,
            DrawImageProcessor definition,
            Image<TPixelBg> source)
        {
            this.configuration = configuration;
            this.definition = definition;
            this.source = source;
        }

        public IImageProcessor<TPixelBg>? Result { get; private set; }

        public void Visit<TPixelFg>(Image<TPixelFg> image)
            where TPixelFg : unmanaged, IPixel<TPixelFg>
            => this.Result = new DrawImageProcessor<TPixelBg, TPixelFg>(
                this.configuration,
                image,
                this.source,
                this.definition.BackgroundLocation,
                this.definition.ForegroundRectangle,
                this.definition.ColorBlendingMode,
                this.definition.AlphaCompositionMode,
                this.definition.Opacity,
                this.definition.RepeatCount);
    }
}
