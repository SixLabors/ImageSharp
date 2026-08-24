// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Decodes a single JPEG-coded HEIF image item.
/// </summary>
/// <typeparam name="TPixel">The destination pixel type.</typeparam>
internal class JpegHeifItemDecoder<TPixel> : IHeifItemDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    /// <summary>
    /// Gets the JPEG-coded image item type.
    /// </summary>
    public Heif4CharCode Type => Heif4CharCode.Jpeg;

    /// <summary>
    /// Gets the legacy JPEG compression method.
    /// </summary>
    public HeifCompressionMethod CompressionMethod => HeifCompressionMethod.LegacyJpeg;

    /// <summary>
    /// Decodes the encoded JPEG payload of an image item.
    /// </summary>
    /// <param name="configuration">The configuration associated with the containing HEIF decode.</param>
    /// <param name="item">The HEIF item whose encoded payload is being decoded.</param>
    /// <param name="data">The encoded JPEG payload.</param>
    /// <param name="colorProfile">The container color description associated with the image item.</param>
    /// <returns>The decoded image.</returns>
    public Image<TPixel> DecodeItemData(
        Configuration configuration,
        HeifItem item,
        Span<byte> data,
        CicpProfile? colorProfile)
    {
        // The JPEG decoder owns the payload's JPEG color coding. The containing decoder attaches HEIF CICP as
        // presentation metadata after payload decode, so it must not be mistaken for JPEG component-transform syntax.
        Image<TPixel> image = Image.Load<TPixel>(data);
        return image;
    }
}
