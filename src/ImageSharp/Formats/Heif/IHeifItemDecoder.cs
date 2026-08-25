// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Decodes the compressed payload of a single HEIF image item.
/// </summary>
/// <typeparam name="TPixel">The destination pixel type.</typeparam>
internal interface IHeifItemDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    /// <summary>
    /// Gets the image item type decoded by this implementation.
    /// </summary>
    public Heif4CharCode Type { get; }

    /// <summary>
    /// Gets the compression method used by the image item.
    /// </summary>
    public HeifCompressionMethod CompressionMethod { get; }

    /// <summary>
    /// Decodes the compressed payload of an image item.
    /// </summary>
    /// <param name="options">The general options governing the containing HEIF decode.</param>
    /// <param name="item">The HEIF item whose encoded payload is being decoded.</param>
    /// <param name="data">The encoded image payload.</param>
    /// <param name="colorProfile">
    /// The container color description that overrides matching color information in the encoded image payload.
    /// </param>
    /// <param name="cancellationToken">The token used to cancel the payload decode.</param>
    /// <returns>The decoded image.</returns>
    public Image<TPixel> DecodeItemData(
        DecoderOptions options,
        HeifItem item,
        Span<byte> data,
        CicpProfile? colorProfile,
        CancellationToken cancellationToken);
}
