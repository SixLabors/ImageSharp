// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization;

/// <summary>
/// A pixel sampling strategy that enumerates all pixels.
/// </summary>
public class ExtensivePixelSamplingStrategy : IPixelSamplingStrategy
{
    /// <inheritdoc />
    public IEnumerable<Buffer2DRegion<TPixel>> EnumeratePixelRegions<TPixel>(Image<TPixel> image)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        foreach (ImageFrame<TPixel> frame in image.Frames)
        {
            yield return frame.PixelBuffer.GetRegion();
        }
    }

    /// <inheritdoc/>
    public IEnumerable<Buffer2DRegion<TPixel>> EnumeratePixelRegions<TPixel>(ImageFrame<TPixel> frame)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        yield return frame.PixelBuffer.GetRegion();
    }
}
