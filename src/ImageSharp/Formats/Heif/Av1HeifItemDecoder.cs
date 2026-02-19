// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Decoder for a single <see cref="HeifItem"/> into a AVIF image.
/// </summary>
internal class Av1HeifItemDecoder<TPixel> : IHeifItemDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    /// <summary>
    /// Gets the item type this decoder decodes, which is <see cref="Heif4CharCode.Av01"/>.
    /// </summary>
    public Heif4CharCode Type => Heif4CharCode.Av01;

    /// <summary>
    /// Gets the compression method this doceder uses, which is <see cref="HeifCompressionMethod.Av1"/>.
    /// </summary>
    public HeifCompressionMethod CompressionMethod => HeifCompressionMethod.Av1;

    /// <summary>
    /// Decode the specified item as AVIF.
    /// </summary>
    public Image<TPixel> DecodeItemData(Configuration configuration, HeifItem item, Span<byte> data)
    {
        Av1Decoder decoder = new(configuration);
        return decoder.Decode<TPixel>(data);
    }
}
