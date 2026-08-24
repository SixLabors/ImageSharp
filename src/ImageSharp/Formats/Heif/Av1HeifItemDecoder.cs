// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Decodes a single AV1-coded HEIF image item.
/// </summary>
/// <typeparam name="TPixel">The destination pixel type.</typeparam>
internal class Av1HeifItemDecoder<TPixel> : IHeifItemDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    /// <summary>
    /// Gets the AV1-coded image item type.
    /// </summary>
    public Heif4CharCode Type => Heif4CharCode.Av01;

    /// <summary>
    /// Gets the AV1 compression method.
    /// </summary>
    public HeifCompressionMethod CompressionMethod => HeifCompressionMethod.Av1;

    /// <summary>
    /// Decodes the encoded AV1 payload of an image item.
    /// </summary>
    /// <param name="configuration">The configuration that supplies memory allocation and codec services.</param>
    /// <param name="item">The HEIF item whose encoded payload is being decoded.</param>
    /// <param name="data">The encoded AV1 payload.</param>
    /// <param name="colorProfile">
    /// The container color description that overrides matching color information in the AV1 sequence header.
    /// </param>
    /// <returns>The decoded image.</returns>
    public Image<TPixel> DecodeItemData(
        Configuration configuration,
        HeifItem item,
        Span<byte> data,
        CicpProfile? colorProfile)
    {
        Av1CodecConfiguration codecConfiguration = item.Av1CodecConfiguration
            ?? throw new InvalidImageContentException($"AV1 image item {item.Id} has no codec configuration property.");

        if (item.ChannelBitDepths is not null)
        {
            foreach (byte channelBitDepth in item.ChannelBitDepths)
            {
                if (channelBitDepth != codecConfiguration.BitDepth)
                {
                    throw new InvalidImageContentException($"AV1 image item {item.Id} has mismatched pixel-information and codec-configuration bit depths.");
                }
            }
        }

        Av1Decoder decoder = new(configuration);
        Image<TPixel> image = decoder.Decode<TPixel>(data, colorProfile, codecConfiguration);
        HeifMetadata metadata = image.Metadata.GetHeifMetadata();
        metadata.CompressionMethod = this.CompressionMethod;
        metadata.BitDepth = codecConfiguration.BitDepth;
        metadata.IsMonochrome = codecConfiguration.IsMonochrome;
        return image;
    }
}
