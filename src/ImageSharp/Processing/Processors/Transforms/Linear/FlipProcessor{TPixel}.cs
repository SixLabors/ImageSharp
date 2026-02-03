// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms;

/// <summary>
/// Provides methods that allow the flipping of an image around its center point.
/// </summary>
/// <typeparam name="TPixel">The pixel format.</typeparam>
internal class FlipProcessor<TPixel> : ImageProcessor<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly FlipProcessor definition;
    private readonly Matrix4x4 transformMatrix;

    /// <summary>
    /// Initializes a new instance of the <see cref="FlipProcessor{TPixel}"/> class.
    /// </summary>
    /// <param name="configuration">The configuration which allows altering default behavior or extending the library.</param>
    /// <param name="definition">The <see cref="FlipProcessor"/>.</param>
    /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
    /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
    public FlipProcessor(Configuration configuration, FlipProcessor definition, Image<TPixel> source, Rectangle sourceRectangle)
        : base(configuration, source, sourceRectangle)
    {
        this.definition = definition;

        // Calculate the transform matrix from the flip operation to allow us
        // to update any metadata that represents pixel coordinates in the source image.
        ProjectiveTransformBuilder builder = new();
        switch (this.definition.FlipMode)
        {
            // No default needed as we have already set the pixels.
            case FlipMode.Vertical:

                // Flip vertically by scaling the Y axis by -1 and translating the Y coordinate.
                builder.AppendScale(new Vector2(1, -1))
                       .AppendTranslation(new PointF(0, this.SourceRectangle.Height - 1));
                break;
            case FlipMode.Horizontal:

                // Flip horizontally by scaling the X axis by -1 and translating the X coordinate.
                builder.AppendScale(new Vector2(-1, 1))
                       .AppendTranslation(new PointF(this.SourceRectangle.Width - 1, 0));
                break;
            default:
                this.transformMatrix = Matrix4x4.Identity;
                return;
        }

        this.transformMatrix = builder.BuildMatrix(sourceRectangle);
    }

    /// <inheritdoc/>
    protected override void OnFrameApply(ImageFrame<TPixel> source)
    {
        switch (this.definition.FlipMode)
        {
            // No default needed as we have already set the pixels.
            case FlipMode.Vertical:
                FlipX(source.PixelBuffer, this.Configuration);
                break;
            case FlipMode.Horizontal:
                FlipY(source, this.Configuration);
                break;
        }
    }

    /// <inheritdoc/>
    protected override void AfterFrameApply(ImageFrame<TPixel> source)
        => source.Metadata.AfterFrameApply(source, source, this.transformMatrix);

    /// <inheritdoc/>
    protected override void AfterImageApply()
        => this.Source.Metadata.AfterImageApply(this.Source, this.transformMatrix);

    /// <summary>
    /// Swaps the image at the X-axis, which goes horizontally through the middle at half the height of the image.
    /// </summary>
    /// <param name="source">The source image to apply the process to.</param>
    /// <param name="configuration">The configuration.</param>
    private static void FlipX(Buffer2D<TPixel> source, Configuration configuration)
    {
        int height = source.Height;
        using IMemoryOwner<TPixel> tempBuffer = configuration.MemoryAllocator.Allocate<TPixel>(source.Width);
        Span<TPixel> temp = tempBuffer.Memory.Span;

        for (int yTop = 0; yTop < (int)((uint)height / 2); yTop++)
        {
            int yBottom = height - yTop - 1;
            Span<TPixel> topRow = source.DangerousGetRowSpan(yBottom);
            Span<TPixel> bottomRow = source.DangerousGetRowSpan(yTop);
            topRow.CopyTo(temp);
            bottomRow.CopyTo(topRow);
            temp.CopyTo(bottomRow);
        }
    }

    /// <summary>
    /// Swaps the image at the Y-axis, which goes vertically through the middle at half of the width of the image.
    /// </summary>
    /// <param name="source">The source image to apply the process to.</param>
    /// <param name="configuration">The configuration.</param>
    private static void FlipY(ImageFrame<TPixel> source, Configuration configuration)
    {
        RowOperation operation = new(source.PixelBuffer);
        ParallelRowIterator.IterateRows(
            configuration,
            source.Bounds,
            in operation);
    }

    private readonly struct RowOperation : IRowOperation
    {
        private readonly Buffer2D<TPixel> source;

        [MethodImpl(InliningOptions.ShortMethod)]
        public RowOperation(Buffer2D<TPixel> source) => this.source = source;

        [MethodImpl(InliningOptions.ShortMethod)]
        public void Invoke(int y) => this.source.DangerousGetRowSpan(y).Reverse();
    }
}
