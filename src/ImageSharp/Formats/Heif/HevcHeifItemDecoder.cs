// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Components.Alpha;
using SixLabors.ImageSharp.Formats.Heif.Hevc;
using SixLabors.ImageSharp.Formats.Heif.Hevc.Color;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Decodes a single HEVC-coded HEIF image item.
/// </summary>
/// <typeparam name="TPixel">The destination pixel type.</typeparam>
internal sealed class HevcHeifItemDecoder<TPixel> : IHeifItemDecoder<TPixel>, IHeifAlphaItemDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    /// <summary>
    /// Gets the HEVC-coded image item type.
    /// </summary>
    public Heif4CharCode Type => Heif4CharCode.Hvc1;

    /// <summary>
    /// Gets the HEVC compression method.
    /// </summary>
    public HeifCompressionMethod CompressionMethod => HeifCompressionMethod.Hevc;

    /// <summary>
    /// Decodes the encoded HEVC payload of an image item.
    /// </summary>
    /// <param name="options">The general options governing the containing HEIF decode.</param>
    /// <param name="item">The HEIF item whose encoded payload is being decoded.</param>
    /// <param name="data">The encoded HEVC payload.</param>
    /// <param name="colorProfile">The container color description that takes precedence over bitstream color information.</param>
    /// <param name="cancellationToken">The token used to cancel the payload decode.</param>
    /// <returns>The decoded image.</returns>
    public Image<TPixel> DecodeItemData(
        DecoderOptions options,
        HeifItem item,
        Span<byte> data,
        CicpProfile? colorProfile,
        CancellationToken cancellationToken)
    {
        using HevcPictureDecoder decoder = DecodePicture(
            options,
            item,
            data,
            colorProfile,
            cancellationToken,
            out HevcCodecConfiguration codecConfiguration,
            out HevcSequenceParameterSet sequenceParameterSet,
            out CicpProfile effectiveColorProfile,
            out HevcChromaSampleLocation chromaSampleLocation);

        ImageFrame<TPixel>? frame = null;
        try
        {
            frame = new ImageFrame<TPixel>(options.Configuration, sequenceParameterSet.DisplayWidth, sequenceParameterSet.DisplayHeight);
            HevcYuvConverter.ConvertToRgb(
                options.Configuration,
                decoder.Picture,
                frame,
                effectiveColorProfile,
                chromaSampleLocation,
                sequenceParameterSet.ConformanceWindowLeftOffset,
                sequenceParameterSet.ConformanceWindowTopOffset);

            ImageMetadata metadata = new()
            {
                CicpProfile = effectiveColorProfile.DeepClone()
            };

            HeifMetadata heifMetadata = metadata.GetHeifMetadata();
            heifMetadata.CompressionMethod = this.CompressionMethod;
            heifMetadata.BitDepth = codecConfiguration.BitDepth;
            heifMetadata.IsMonochrome = codecConfiguration.IsMonochrome;
            return new Image<TPixel>(options.Configuration, metadata, [frame]);
        }
        catch
        {
            // Ownership transfers only after the image constructor accepts the completely converted frame.
            frame?.Dispose();
            throw;
        }
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
        using HevcPictureDecoder decoder = DecodePicture(
            options,
            item,
            data,
            item.CicpProfile,
            cancellationToken,
            out _,
            out HevcSequenceParameterSet sequenceParameterSet,
            out CicpProfile effectiveColorProfile,
            out HevcChromaSampleLocation chromaSampleLocation);

        Rectangle sourceRectangle = new(
            sequenceParameterSet.ConformanceWindowLeftOffset,
            sequenceParameterSet.ConformanceWindowTopOffset,
            sequenceParameterSet.DisplayWidth,
            sequenceParameterSet.DisplayHeight);

        if (decoder.Picture.ChromaFormat != 0)
        {
            throw new InvalidImageContentException($"HEVC alpha image item {item.Id} is not monochrome.");
        }

        HevcYuvConverter.ComposeAlpha(
            options.Configuration,
            decoder.Picture,
            destination,
            effectiveColorProfile,
            chromaSampleLocation,
            sourceRectangle,
            outputSize,
            destinationRectangle,
            premultiplied);
    }

    /// <summary>
    /// Validates and reconstructs one HEVC image item while retaining the native picture for its caller.
    /// </summary>
    /// <param name="options">The general options governing the containing HEIF decode.</param>
    /// <param name="item">The HEVC image item being decoded.</param>
    /// <param name="data">The encoded HEVC payload.</param>
    /// <param name="colorProfile">The container color description that takes precedence over bitstream color information.</param>
    /// <param name="cancellationToken">The token used to cancel the payload decode.</param>
    /// <param name="codecConfiguration">Receives the validated HEVC codec configuration.</param>
    /// <param name="sequenceParameterSet">Receives the sequence parameters describing the visible picture.</param>
    /// <param name="effectiveColorProfile">Receives the effective CICP description used for presentation.</param>
    /// <param name="chromaSampleLocation">Receives the progressive-frame chroma sample location.</param>
    /// <returns>The decoder owning the reconstructed native picture. Ownership transfers to the caller.</returns>
    private static HevcPictureDecoder DecodePicture(
        DecoderOptions options,
        HeifItem item,
        Span<byte> data,
        CicpProfile? colorProfile,
        CancellationToken cancellationToken,
        out HevcCodecConfiguration codecConfiguration,
        out HevcSequenceParameterSet sequenceParameterSet,
        out CicpProfile effectiveColorProfile,
        out HevcChromaSampleLocation chromaSampleLocation)
    {
        cancellationToken.ThrowIfCancellationRequested();
        codecConfiguration = item.HevcCodecConfiguration
            ?? throw new InvalidImageContentException($"HEVC image item {item.Id} has no codec configuration property.");

        if (item.ChannelBitDepths is not null)
        {
            codecConfiguration.ValidateChannelBitDepths(item.ChannelBitDepths);
        }

        HevcImageItemBitstream bitstream = new(data, codecConfiguration);
        HevcPictureParameterSet pictureParameterSet = bitstream.SliceSegments[0].PictureParameterSet;
        sequenceParameterSet = pictureParameterSet.SequenceParameterSet;
        HevcVideoUsabilityInformation? vui = sequenceParameterSet.VideoUsabilityInformation;

        // ISO BMFF color information takes precedence when both the container and HEVC VUI describe the image.
        // Otherwise, retain the VUI values used by conversion so bitstream-only color information reaches metadata.
        effectiveColorProfile = colorProfile is not null
            ? new CicpProfile(
                (byte)colorProfile.ColorPrimaries,
                (byte)colorProfile.TransferCharacteristics,
                (byte)colorProfile.MatrixCoefficients,
                colorProfile.FullRange)
            : new CicpProfile(
                vui?.ColorDescriptionPresent == true ? vui.ColorPrimaries : (byte)CicpColorPrimaries.Unspecified,
                vui?.ColorDescriptionPresent == true ? vui.TransferCharacteristics : (byte)CicpTransferCharacteristics.Unspecified,
                vui?.ColorDescriptionPresent == true ? vui.MatrixCoefficients : (byte)CicpMatrixCoefficients.Unspecified,
                vui?.VideoSignalTypePresent == true && vui.FullRange);

        chromaSampleLocation = vui?.ChromaLocationInfoPresent == true
            ? vui.ChromaSampleLocationTopField
            : HevcChromaSampleLocation.Left;

        HevcPictureDecoder decoder = new(options.Configuration, pictureParameterSet);
        try
        {
            decoder.Decode(bitstream);
            cancellationToken.ThrowIfCancellationRequested();
            return decoder;
        }
        catch
        {
            decoder.Dispose();
            throw;
        }
    }
}
