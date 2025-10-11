// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Drawing;

/// <summary>
/// Combines two images together by blending the pixels.
/// </summary>
/// <typeparam name="TPixelBg">The pixel format of destination image.</typeparam>
/// <typeparam name="TPixelFg">The pixel format of source image.</typeparam>
internal class DrawImageProcessor<TPixelBg, TPixelFg> : ImageProcessor<TPixelBg>
    where TPixelBg : unmanaged, IPixel<TPixelBg>
    where TPixelFg : unmanaged, IPixel<TPixelFg>
{
    private int currentFrameLoop;

    /// <summary>
    /// Initializes a new instance of the <see cref="DrawImageProcessor{TPixelBg, TPixelFg}"/> class.
    /// </summary>
    /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
    /// <param name="foregroundImage">The foreground <see cref="Image{TPixelFg}"/> to blend with the currently processing image.</param>
    /// <param name="backgroundImage">The source <see cref="Image{TPixelBg}"/> for the current processor instance.</param>
    /// <param name="backgroundLocation">The location to draw the blended image.</param>
    /// <param name="foregroundRectangle">The source area to process for the current processor instance.</param>
    /// <param name="colorBlendingMode">The blending mode to use when drawing the image.</param>
    /// <param name="alphaCompositionMode">The alpha blending mode to use when drawing the image.</param>
    /// <param name="opacity">The opacity of the image to blend. Must be between 0 and 1.</param>
    /// <param name="repeatCount">The loop count. The number of times to loop the animation. 0 means infinitely.</param>
    public DrawImageProcessor(
        Configuration configuration,
        Image<TPixelFg> foregroundImage,
        Image<TPixelBg> backgroundImage,
        Point backgroundLocation,
        Rectangle foregroundRectangle,
        PixelColorBlendingMode colorBlendingMode,
        PixelAlphaCompositionMode alphaCompositionMode,
        float opacity,
        int repeatCount)
        : base(configuration, backgroundImage, backgroundImage.Bounds)
    {
        Guard.MustBeGreaterThanOrEqualTo(repeatCount, 0, nameof(repeatCount));
        Guard.MustBeBetweenOrEqualTo(opacity, 0, 1, nameof(opacity));

        this.ForegroundImage = foregroundImage;
        this.ForegroundRectangle = foregroundRectangle;
        this.Opacity = opacity;
        this.Blender = PixelOperations<TPixelBg>.Instance.GetPixelBlender(colorBlendingMode, alphaCompositionMode);
        this.BackgroundLocation = backgroundLocation;
    }

    /// <summary>
    /// Gets the image to blend
    /// </summary>
    public Image<TPixelFg> ForegroundImage { get; }

    /// <summary>
    /// Gets the rectangular portion of the foreground image to draw.
    /// </summary>
    public Rectangle ForegroundRectangle { get; }

    /// <summary>
    /// Gets the opacity of the image to blend
    /// </summary>
    public float Opacity { get; }

    /// <summary>
    /// Gets the pixel blender
    /// </summary>
    public PixelBlender<TPixelBg> Blender { get; }

    /// <summary>
    /// Gets the location to draw the blended image
    /// </summary>
    public Point BackgroundLocation { get; }

    /// <summary>
    /// Gets the loop count. The number of times to loop the animation. 0 means infinitely.
    /// </summary>
    public int RepeatCount { get; }

    /// <inheritdoc/>
    protected override void OnFrameApply(ImageFrame<TPixelBg> source)
    {
        // Align the bounds so that both the source and targets are the same width and height for blending.
        // We ensure that negative locations are subtracted from both bounds so that foreground images can partially overlap.
        Rectangle foregroundRectangle = this.ForegroundRectangle;

        // Sanitize the location so that we don't try and sample outside the image.
        int left = this.BackgroundLocation.X;
        int top = this.BackgroundLocation.Y;

        if (this.BackgroundLocation.X < 0)
        {
            foregroundRectangle.Width += this.BackgroundLocation.X;
            foregroundRectangle.X -= this.BackgroundLocation.X;
            left = 0;
        }

        if (this.BackgroundLocation.Y < 0)
        {
            foregroundRectangle.Height += this.BackgroundLocation.Y;
            foregroundRectangle.Y -= this.BackgroundLocation.Y;
            top = 0;
        }

        // Clamp the height/width to the available space left to prevent overflowing
        foregroundRectangle.Width = Math.Min(source.Width - left, foregroundRectangle.Width);
        foregroundRectangle.Height = Math.Min(source.Height - top, foregroundRectangle.Height);
        foregroundRectangle = Rectangle.Intersect(foregroundRectangle, this.ForegroundImage.Bounds);

        int width = foregroundRectangle.Width;
        int height = foregroundRectangle.Height;
        if (width <= 0 || height <= 0)
        {
            // Nothing to do, return.
            return;
        }

        // Sanitize the dimensions so that we don't try and sample outside the image.
        Rectangle backgroundRectangle = Rectangle.Intersect(new Rectangle(left, top, width, height), this.SourceRectangle);
        Configuration configuration = this.Configuration;
        int currentFrameIndex = this.currentFrameLoop % this.ForegroundImage.Frames.Count;

        DrawImageProcessor<TPixelBg, TPixelFg>.RowOperation operation =
            new(
                configuration,
                source.PixelBuffer,
                this.ForegroundImage.Frames[currentFrameIndex].PixelBuffer,
                backgroundRectangle,
                foregroundRectangle,
                this.Blender,
                this.Opacity);

        ParallelRowIterator.IterateRows(
            configuration,
            new Rectangle(0, 0, foregroundRectangle.Width, foregroundRectangle.Height),
            in operation);

        if (this.RepeatCount is 0 || this.currentFrameLoop / this.ForegroundImage.Frames.Count <= this.RepeatCount)
        {
            this.currentFrameLoop++;
        }
    }

    /// <summary>
    /// A <see langword="struct"/> implementing the draw logic for <see cref="DrawImageProcessor{TPixelBg,TPixelFg}"/>.
    /// </summary>
    private readonly struct RowOperation : IRowOperation
    {
        private readonly Buffer2D<TPixelBg> background;
        private readonly Buffer2D<TPixelFg> foreground;
        private readonly PixelBlender<TPixelBg> blender;
        private readonly Configuration configuration;
        private readonly Rectangle foregroundRectangle;
        private readonly Rectangle backgroundRectangle;
        private readonly float opacity;

        [MethodImpl(InliningOptions.ShortMethod)]
        public RowOperation(
            Configuration configuration,
            Buffer2D<TPixelBg> background,
            Buffer2D<TPixelFg> foreground,
            Rectangle backgroundRectangle,
            Rectangle foregroundRectangle,
            PixelBlender<TPixelBg> blender,
            float opacity)
        {
            this.configuration = configuration;
            this.background = background;
            this.foreground = foreground;
            this.backgroundRectangle = backgroundRectangle;
            this.foregroundRectangle = foregroundRectangle;
            this.blender = blender;
            this.opacity = opacity;
        }

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void Invoke(int y)
        {
            Span<TPixelBg> background = this.background.DangerousGetRowSpan(y + this.backgroundRectangle.Top).Slice(this.backgroundRectangle.Left, this.backgroundRectangle.Width);
            Span<TPixelFg> foreground = this.foreground.DangerousGetRowSpan(y + this.foregroundRectangle.Top).Slice(this.foregroundRectangle.Left, this.foregroundRectangle.Width);
            this.blender.Blend<TPixelFg>(this.configuration, background, background, foreground, this.opacity);
        }
    }
}
