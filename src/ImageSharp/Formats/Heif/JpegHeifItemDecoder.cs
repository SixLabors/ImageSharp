// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Decoder for a single <see cref="HeifItem"/> into a JPEG image.
/// </summary>
internal class JpegHeifItemDecoder<TPixel> : IHeifItemDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    /// <summary>
    /// Gets the item type this decoder decodes, which is <see cref="Heif4CharCode.Jpeg"/>.
    /// </summary>
    public Heif4CharCode Type => Heif4CharCode.Jpeg;

    /// <summary>
    /// Gets the compression method this doceder uses, which is <see cref="HeifCompressionMethod.LegacyJpeg"/>.
    /// </summary>
    public HeifCompressionMethod CompressionMethod => HeifCompressionMethod.LegacyJpeg;

    /// <summary>
    /// Decode the specified item as JPEG.
    /// </summary>
    public Image<TPixel> DecodeItemData(Configuration configuration, HeifItem item, Span<byte> data)
    {
        Image<TPixel> image = Image.Load<TPixel>(data);
        return image;
    }
}
