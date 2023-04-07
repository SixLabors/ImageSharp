// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization;

/// <summary>
/// Provides an abstraction to enumerate pixel regions for sampling within <see cref="Image{TPixel}"/>.
/// </summary>
public interface IPixelSamplingStrategy
{
    /// <summary>
    /// Enumerates pixel regions for all frames within the image as <see cref="Buffer2DRegion{T}"/>.
    /// </summary>
    /// <param name="image">The image.</param>
    /// <typeparam name="TPixel">The pixel type.</typeparam>
    /// <returns>An enumeration of pixel regions.</returns>
    IEnumerable<Buffer2DRegion<TPixel>> EnumeratePixelRegions<TPixel>(Image<TPixel> image)
        where TPixel : unmanaged, IPixel<TPixel>;

    /// <summary>
    /// Enumerates pixel regions within a single image frame as <see cref="Buffer2DRegion{T}"/>.
    /// </summary>
    /// <param name="frame">The image frame.</param>
    /// <typeparam name="TPixel">The pixel type.</typeparam>
    /// <returns>An enumeration of pixel regions.</returns>
    IEnumerable<Buffer2DRegion<TPixel>> EnumeratePixelRegions<TPixel>(ImageFrame<TPixel> frame)
        where TPixel : unmanaged, IPixel<TPixel>;
}
