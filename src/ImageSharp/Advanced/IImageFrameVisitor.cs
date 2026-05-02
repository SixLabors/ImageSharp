// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Advanced;

/// <summary>
/// A visitor to implement a double-dispatch pattern in order to apply pixel-specific operations
/// on non-generic <see cref="ImageFrame"/> instances.
/// </summary>
public interface IImageFrameVisitor
{
    /// <summary>
    /// Provides a pixel-specific implementation for a given operation.
    /// </summary>
    /// <param name="frame">The image frame.</param>
    /// <typeparam name="TPixel">The pixel type.</typeparam>
    public void Visit<TPixel>(ImageFrame<TPixel> frame)
        where TPixel : unmanaged, IPixel<TPixel>;
}
