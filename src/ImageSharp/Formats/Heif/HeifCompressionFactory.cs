// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Hevc;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Selects the still-image decoder for a compressed HEIF image item.
/// </summary>
internal static class HeifCompressionFactory
{
    /// <summary>
    /// Gets a decoder for the specified compressed image item type.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel type.</typeparam>
    /// <param name="type">The image item type.</param>
    /// <returns>A matching item decoder, or <see langword="null"/> when the item type is not supported.</returns>
    public static IHeifItemDecoder<TPixel>? GetDecoder<TPixel>(Heif4CharCode type)
        where TPixel : unmanaged, IPixel<TPixel> => type switch
        {
            Heif4CharCode.Jpeg => new JpegHeifItemDecoder<TPixel>(),
            Heif4CharCode.Av01 => new Av1HeifItemDecoder<TPixel>(),
            Heif4CharCode.Hvc1 => new HevcHeifItemDecoder<TPixel>(),
            _ => null
        };
}
