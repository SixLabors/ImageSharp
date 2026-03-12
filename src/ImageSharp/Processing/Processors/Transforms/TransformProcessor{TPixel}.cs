// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms;

/// <summary>
/// The base class for all transform processors. Any processor that changes the dimensions of the image should inherit from this.
/// </summary>
/// <typeparam name="TPixel">The pixel format.</typeparam>
internal abstract class TransformProcessor<TPixel> : CloningImageProcessor<TPixel>
     where TPixel : unmanaged, IPixel<TPixel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TransformProcessor{TPixel}"/> class.
    /// </summary>
    /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
    /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
    /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
    protected TransformProcessor(Configuration configuration, Image<TPixel> source, Rectangle sourceRectangle)
        : base(configuration, source, sourceRectangle)
    {
    }

    /// <summary>
    /// Gets the transform matrix that will be applied to the image.
    /// </summary>
    /// <returns>
    /// The <see cref="Matrix4x4"/> that represents the transformation to be applied to the image.
    /// </returns>
    protected abstract Matrix4x4 GetTransformMatrix();

    /// <inheritdoc/>
    protected override void AfterFrameApply(ImageFrame<TPixel> source, ImageFrame<TPixel> destination)
        => destination.Metadata.AfterFrameApply(source, destination, this.GetTransformMatrix());

    /// <inheritdoc/>
    protected override void AfterImageApply(Image<TPixel> destination)
        => destination.Metadata.AfterImageApply(destination, this.GetTransformMatrix());
}
