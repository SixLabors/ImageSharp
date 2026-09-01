// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Components.Alpha;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Decodes a single AV1-coded HEIF image item.
/// </summary>
/// <typeparam name="TPixel">The destination pixel type.</typeparam>
internal sealed class Av1HeifItemDecoder<TPixel> : IHeifItemDecoder<TPixel>, IHeifAlphaItemDecoder<TPixel>
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
    /// <param name="options">The general options governing the containing HEIF decode.</param>
    /// <param name="item">The HEIF item whose encoded payload is being decoded.</param>
    /// <param name="data">The encoded AV1 payload.</param>
    /// <param name="colorProfile">
    /// The container color description that supplies unspecified color information in the AV1 sequence header.
    /// </param>
    /// <param name="cancellationToken">The token used to cancel the payload decode.</param>
    /// <returns>The decoded image.</returns>
    public Image<TPixel> DecodeItemData(
        DecoderOptions options,
        HeifItem item,
        Span<byte> data,
        CicpProfile? colorProfile,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Span<byte> itemData = GetItemData(item, data);
        Av1CodecConfiguration codecConfiguration = ValidateItemData(
            options,
            item,
            itemData,
            out HeifContentLightLevel? obuContentLightLevel,
            out HeifMasteringDisplayColorVolume? obuMasteringDisplayColorVolume);

        byte operatingPointIndex = item.Av1OperatingPointSelector?.Index ?? 0;

        using Av1Decoder decoder = new(options.Configuration, operatingPointIndex);
        Image<TPixel> image = decoder.Decode<TPixel>(
            itemData,
            colorProfile,
            codecConfiguration,
            item.Av1LayeredImageIndex,
            item.Extent);

        HeifMetadata metadata = image.Metadata.GetHeifMetadata();
        metadata.CompressionMethod = this.CompressionMethod;
        metadata.BitDepth = codecConfiguration.BitDepth;
        metadata.IsMonochrome = codecConfiguration.IsMonochrome;
        metadata.ContentLightLevel = item.ContentLightLevel ?? obuContentLightLevel;
        metadata.MasteringDisplayColorVolume = item.MasteringDisplayColorVolume ?? obuMasteringDisplayColorVolume;
        return image;
    }

    /// <inheritdoc/>
    public void DecodeAlphaItemData(
        DecoderOptions options,
        HeifItem item,
        Span<byte> data,
        ImageFrame<TPixel> destination,
        Size outputSize,
        Rectangle destinationRectangle,
        bool premultiplied,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Span<byte> itemData = GetItemData(item, data);
        Av1CodecConfiguration codecConfiguration = ValidateItemData(options, item, itemData, out _, out _);
        if (!codecConfiguration.IsMonochrome)
        {
            throw new InvalidImageContentException($"AV1 alpha image item {item.Id} is not monochrome.");
        }

        byte operatingPointIndex = item.Av1OperatingPointSelector?.Index ?? 0;

        using Av1Decoder decoder = new(options.Configuration, operatingPointIndex);
        decoder.DecodeAlpha(
            itemData,
            item.CicpProfile,
            codecConfiguration,
            default,
            destination,
            outputSize,
            destinationRectangle,
            premultiplied,
            item.Av1LayeredImageIndex);
    }

    /// <summary>
    /// Gets the cumulative item bytes required by an explicit AV1 spatial-layer selection.
    /// </summary>
    /// <param name="item">The AV1 image item containing optional layered-image properties.</param>
    /// <param name="data">The complete logical image-item payload.</param>
    /// <returns>The complete payload for final-layer decoding, or the cumulative prefix through the selected layer.</returns>
    private static Span<byte> GetItemData(HeifItem item, Span<byte> data)
    {
        Av1LayeredImageIndex? layeredImageIndex = item.Av1LayeredImageIndex;
        if (layeredImageIndex is null)
        {
            return data;
        }

        int payloadLength = layeredImageIndex.Value.GetPayloadLength(data.Length, item.Av1LayerSelector);
        return data[..payloadLength];
    }

    /// <summary>
    /// Validates an AV1 item description against its encoded payload and returns the required codec configuration.
    /// </summary>
    /// <param name="options">The general options governing the containing HEIF decode.</param>
    /// <param name="item">The AV1 image item being validated.</param>
    /// <param name="data">The encoded AV1 payload.</param>
    /// <param name="obuContentLightLevel">Receives content-light metadata found in the AV1 payload.</param>
    /// <param name="obuMasteringDisplayColorVolume">Receives mastering-display metadata found in the AV1 payload.</param>
    /// <returns>The validated item-associated AV1 codec configuration.</returns>
    private static Av1CodecConfiguration ValidateItemData(
        DecoderOptions options,
        HeifItem item,
        ReadOnlySpan<byte> data,
        out HeifContentLightLevel? obuContentLightLevel,
        out HeifMasteringDisplayColorVolume? obuMasteringDisplayColorVolume)
    {
        Av1CodecConfiguration codecConfiguration = item.Av1CodecConfiguration
            ?? throw new InvalidImageContentException($"AV1 image item {item.Id} has no codec configuration property.");

        if (item.ChannelBitDepths is not null)
        {
            foreach (byte channelBitDepth in item.ChannelBitDepths)
            {
                if (channelBitDepth != (byte)codecConfiguration.BitDepth)
                {
                    throw new InvalidImageContentException(
                        $"AV1 image item {item.Id} has mismatched pixel-information and codec-configuration bit depths.");
                }
            }
        }

        codecConfiguration.ValidateItemData(
            data,
            item.ContentLightLevel,
            item.MasteringDisplayColorVolume,
            options,
            out obuContentLightLevel,
            out obuMasteringDisplayColorVolume);

        return codecConfiguration;
    }
}
