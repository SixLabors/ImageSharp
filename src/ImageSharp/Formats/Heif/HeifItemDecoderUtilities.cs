// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Provides shared presentation operations for decoded HEIF image items.
/// </summary>
internal static class HeifItemDecoderUtilities
{
    /// <summary>
    /// Scales a decoded image to the spatial extent associated with its image item.
    /// </summary>
    /// <typeparam name="TPixel">The decoded pixel format.</typeparam>
    /// <param name="image">The decoded image.</param>
    /// <param name="item">The image item that defines the presented spatial extent.</param>
    public static void ScaleToItemExtent<TPixel>(Image<TPixel> image, HeifItem item)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Size extent = item.Extent;
        if (extent == default || (image.Width == extent.Width && image.Height == extent.Height))
        {
            return;
        }

        // libavif applies box filtering when coded dimensions differ from an item's ispe dimensions. Reusing the
        // same ImageSharp resampler keeps direct images and grid tiles on one presentation path.
        image.Mutate(context => context.Resize(extent.Width, extent.Height, KnownResamplers.Box));
    }
}
