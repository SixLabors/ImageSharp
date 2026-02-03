// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms;

internal class SwizzleProcessor<TSwizzler, TPixel> : TransformProcessor<TPixel>
    where TSwizzler : struct, ISwizzler
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly TSwizzler swizzler;
    private readonly Size destinationSize;
    private readonly Matrix4x4 transformMatrix;

    public SwizzleProcessor(Configuration configuration, TSwizzler swizzler, Image<TPixel> source, Rectangle sourceRectangle)
        : base(configuration, source, sourceRectangle)
    {
        this.swizzler = swizzler;
        this.destinationSize = swizzler.DestinationSize;

        // Calculate the transform matrix from the swizzle operation to allow us
        // to update any metadata that represents pixel coordinates in the source image.
        this.transformMatrix = new ProjectiveTransformBuilder()
            .AppendMatrix(TransformUtilities.GetSwizzlerMatrix(swizzler, sourceRectangle))
            .BuildMatrix(sourceRectangle);
    }

    /// <inheritdoc />
    protected override Size GetDestinationSize() => this.destinationSize;

    /// <inheritdoc />
    protected override Matrix4x4 GetTransformMatrix() => this.transformMatrix;

    /// <inheritdoc />
    protected override void OnFrameApply(ImageFrame<TPixel> source, ImageFrame<TPixel> destination)
    {
        Point p = default;
        Point newPoint;
        Buffer2D<TPixel> sourceBuffer = source.PixelBuffer;
        for (p.Y = 0; p.Y < source.Height; p.Y++)
        {
            Span<TPixel> rowSpan = sourceBuffer.DangerousGetRowSpan(p.Y);
            for (p.X = 0; p.X < source.Width; p.X++)
            {
                newPoint = this.swizzler.Transform(p);
                destination[newPoint.X, newPoint.Y] = rowSpan[p.X];
            }
        }
    }
}
