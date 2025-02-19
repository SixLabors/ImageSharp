// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Decoder for a single <see cref="HeifItem"/>.
/// </summary>
/// <typeparam name="TPixel">The pixel type to use.</typeparam>
internal interface IHeifItemDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    /// <summary>
    /// Gets the type of item this decoder can decode.
    /// </summary>
    public Heif4CharCode Type { get; }

    /// <summary>
    /// Gets the <see cref="HeifCompressionMethod"/> tis decoder uses.
    /// </summary>
    public HeifCompressionMethod CompressionMethod { get; }

    /// <summary>
    /// Decode the specified item, given encoded data.
    /// </summary>
    /// <param name="configuration">The configuration to used.</param>
    /// <param name="item">The item to decode.</param>
    /// <param name="data">The encoded data.</param>
    /// <returns>The decoded image.</returns>
    public Image<TPixel> DecodeItemData(Configuration configuration, HeifItem item, Span<byte> data);
}
