// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Components.Alpha;
using SixLabors.ImageSharp.Formats.Heif.Hevc;
using SixLabors.ImageSharp.Formats.Heif.Hevc.Color;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Decodes a single HEVC-coded HEIF image item.
/// </summary>
/// <typeparam name="TPixel">The destination pixel type.</typeparam>
internal sealed class HevcHeifItemDecoder<TPixel> : IHeifItemDecoder<TPixel>, IHeifAlphaItemDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private HevcSupplementalEnhancementInformation? supplementalEnhancementInformation;

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
        this.supplementalEnhancementInformation = null;
        using HevcPictureDecoder decoder = DecodePicture(
            options,
            item,
            data,
            colorProfile,
            cancellationToken,
            out HevcCodecConfiguration codecConfiguration,
            out HevcSequenceParameterSet sequenceParameterSet,
            out CicpProfile effectiveColorProfile,
            out HevcChromaSampleLocation chromaSampleLocation,
            out HevcSupplementalEnhancementInformation supplementalEnhancementInformation);

        if (supplementalEnhancementInformation.NoDisplay)
        {
            throw new InvalidImageContentException($"HEVC image item {item.Id} is marked as unavailable for display.");
        }

        ValidateSupplementalMetadata(item, supplementalEnhancementInformation);
        this.supplementalEnhancementInformation = supplementalEnhancementInformation;

        ImageFrame<TPixel>? frame = null;
        Image<TPixel>? image = null;
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
            heifMetadata.ContentLightLevel = supplementalEnhancementInformation.ContentLightLevel;
            heifMetadata.MasteringDisplayColorVolume = supplementalEnhancementInformation.MasteringDisplayColorVolume;
            heifMetadata.ContentColorVolume = supplementalEnhancementInformation.ContentColorVolume;
            heifMetadata.AmbientViewingEnvironment = supplementalEnhancementInformation.AmbientViewingEnvironment;

            image = new Image<TPixel>(options.Configuration, metadata, [frame]);
            frame = null;
            return image;
        }
        catch
        {
            // Before the image constructor succeeds the frame remains locally owned. Afterwards the image owns it and
            // every processor-created replacement buffer, so unwind exactly one of those two ownership states.
            image?.Dispose();
            frame?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Applies the active HEVC display-orientation message to the complete presented image.
    /// </summary>
    /// <param name="image">The decoded image after item scaling and auxiliary-alpha composition.</param>
    public void ApplySupplementalPresentation(Image<TPixel> image)
    {
        HevcSupplementalEnhancementInformation supplementalEnhancementInformation
            = this.supplementalEnhancementInformation!;

        if (!supplementalEnhancementInformation.HasDisplayOrientation)
        {
            return;
        }

        image.Mutate(context =>
        {
            // H.265 applies both flips to the cropped decoded picture before its anticlockwise rotation.
            // ImageSharp's positive rotation is clockwise, so quarter turns use the exact optimized modes and
            // all other coded angles use the equivalent positive clockwise angle.
            if (supplementalEnhancementInformation.HorizontalFlip)
            {
                context.Flip(FlipMode.Horizontal);
            }

            if (supplementalEnhancementInformation.VerticalFlip)
            {
                context.Flip(FlipMode.Vertical);
            }

            ushort rotation = supplementalEnhancementInformation.AnticlockwiseRotation;
            switch (rotation)
            {
                case 0:
                    break;
                case 16384:
                    context.Rotate(RotateMode.Rotate270);
                    break;
                case 32768:
                    context.Rotate(RotateMode.Rotate180);
                    break;
                case 49152:
                    context.Rotate(RotateMode.Rotate90);
                    break;
                default:
                    context.Rotate(360F - ((360F * rotation) / 65536F));
                    break;
            }
        });
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
            out HevcChromaSampleLocation chromaSampleLocation,
            out _);

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
    /// <param name="supplementalEnhancementInformation">Receives the bounded presentation and metadata SEI state.</param>
    /// <returns>The decoder owning the reconstructed native picture. Ownership transfers to the caller.</returns>
    private static HevcPictureDecoder DecodePicture(
        DecoderOptions options,
        HeifItem item,
        ReadOnlySpan<byte> data,
        CicpProfile? colorProfile,
        CancellationToken cancellationToken,
        out HevcCodecConfiguration codecConfiguration,
        out HevcSequenceParameterSet sequenceParameterSet,
        out CicpProfile effectiveColorProfile,
        out HevcChromaSampleLocation chromaSampleLocation,
        out HevcSupplementalEnhancementInformation supplementalEnhancementInformation)
    {
        cancellationToken.ThrowIfCancellationRequested();
        codecConfiguration = item.HevcCodecConfiguration
            ?? throw new InvalidImageContentException($"HEVC image item {item.Id} has no codec configuration property.");

        if (item.ChannelBitDepths is not null)
        {
            codecConfiguration.ValidateChannelBitDepths(item.ChannelBitDepths);
        }

        HevcImageItemBitstream bitstream = new(data, codecConfiguration);
        supplementalEnhancementInformation = bitstream.SupplementalEnhancementInformation;
        HevcPictureParameterSet pictureParameterSet = bitstream.SliceSegments[0].PictureParameterSet;
        sequenceParameterSet = pictureParameterSet.SequenceParameterSet;
        HevcVideoUsabilityInformation? vui = sequenceParameterSet.VideoUsabilityInformation;
        byte transferCharacteristics = vui?.ColorDescriptionPresent == true
            ? vui.TransferCharacteristics
            : (byte)CicpTransferCharacteristics.Unspecified;

        byte? preferredTransferCharacteristics = supplementalEnhancementInformation.PreferredTransferCharacteristics;
        if (colorProfile is null && preferredTransferCharacteristics is not null)
        {
            transferCharacteristics = preferredTransferCharacteristics.Value;
        }

        // ISO BMFF color information takes precedence when both the container and HEVC VUI describe the image.
        // Otherwise, retain the VUI values and the SEI-preferred transfer function used by conversion so bitstream-only
        // color information reaches metadata.
        effectiveColorProfile = colorProfile is not null
            ? new CicpProfile(
                (byte)colorProfile.ColorPrimaries,
                (byte)colorProfile.TransferCharacteristics,
                (byte)colorProfile.MatrixCoefficients,
                colorProfile.FullRange)
            : new CicpProfile(
                vui?.ColorDescriptionPresent == true ? vui.ColorPrimaries : (byte)CicpColorPrimaries.Unspecified,
                transferCharacteristics,
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

    /// <summary>
    /// Validates equivalent codec and item-property HDR metadata before either representation is exposed.
    /// </summary>
    private static void ValidateSupplementalMetadata(
        HeifItem item,
        HevcSupplementalEnhancementInformation supplementalEnhancementInformation)
    {
        HeifContentLightLevel? supplementalContentLightLevel = supplementalEnhancementInformation.ContentLightLevel;
        HeifContentLightLevel? itemContentLightLevel = item.ContentLightLevel;
        if (supplementalContentLightLevel is not null
            && itemContentLightLevel is not null
            && (supplementalContentLightLevel.Value.MaximumContentLightLevel != itemContentLightLevel.Value.MaximumContentLightLevel
                || supplementalContentLightLevel.Value.MaximumPictureAverageLightLevel
                    != itemContentLightLevel.Value.MaximumPictureAverageLightLevel))
        {
            throw new InvalidImageContentException($"HEVC image item {item.Id} has conflicting content light-level metadata.");
        }

        HeifMasteringDisplayColorVolume? supplementalMasteringDisplayColorVolume
            = supplementalEnhancementInformation.MasteringDisplayColorVolume;

        if (supplementalMasteringDisplayColorVolume is not null
            && item.MasteringDisplayColorVolume is not null
            && supplementalMasteringDisplayColorVolume.Value != item.MasteringDisplayColorVolume.Value)
        {
            throw new InvalidImageContentException($"HEVC image item {item.Id} has conflicting mastering-display metadata.");
        }

        HeifContentColorVolume? supplementalContentColorVolume = supplementalEnhancementInformation.ContentColorVolume;
        if (supplementalContentColorVolume is not null
            && item.ContentColorVolume is not null
            && supplementalContentColorVolume.Value != item.ContentColorVolume.Value)
        {
            throw new InvalidImageContentException($"HEVC image item {item.Id} has conflicting content color-volume metadata.");
        }

        HeifAmbientViewingEnvironment? supplementalAmbientViewingEnvironment
            = supplementalEnhancementInformation.AmbientViewingEnvironment;

        if (supplementalAmbientViewingEnvironment is not null
            && item.AmbientViewingEnvironment is not null
            && supplementalAmbientViewingEnvironment.Value != item.AmbientViewingEnvironment.Value)
        {
            throw new InvalidImageContentException($"HEVC image item {item.Id} has conflicting ambient-viewing metadata.");
        }
    }
}
