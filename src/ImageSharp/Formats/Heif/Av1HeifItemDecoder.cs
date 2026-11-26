// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Color;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Components.Alpha;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Decodes a single AV1-coded HEIF image item.
/// </summary>
/// <typeparam name="TPixel">The destination pixel type.</typeparam>
internal sealed class Av1HeifItemDecoder<TPixel> : IHeifItemDecoder<TPixel>, IHeifAlphaItemDecoder
    where TPixel : unmanaged, IPixel<TPixel>
{
    /// <summary>
    /// A value that is <see langword="true"/> if the caller keeps an image without the decoded items. In that case, the integrity mode controls if a
    /// damaged item payload throws.
    /// </summary>
    private readonly bool imageExists;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1HeifItemDecoder{TPixel}"/> class.
    /// </summary>
    /// <param name="imageExists">
    /// <see langword="true"/> if the caller keeps an image without the decoded items. This image is the canvas of a grid, or the color image of an alpha
    /// item. If the value is <see langword="true"/> and the mode is <see cref="SegmentIntegrityHandling.IgnoreImageData"/>, a damaged item payload that
    /// holds no frame gives no pixels. In all other cases, that damage throws.
    /// </param>
    public Av1HeifItemDecoder(bool imageExists) => this.imageExists = imageExists;

    /// <summary>
    /// Gets the AV1-coded image item type.
    /// </summary>
    public Heif4CharCode Type => Heif4CharCode.Av01;

    /// <summary>
    /// Decodes the encoded AV1 payload of an image item.
    /// </summary>
    /// <param name="options">The decoder options for the HEIF decode.</param>
    /// <param name="chromaUpsampling">The chroma reconstruction mode.</param>
    /// <param name="item">The HEIF item that holds the encoded payload.</param>
    /// <param name="data">The encoded AV1 payload.</param>
    /// <param name="colorProfile">
    /// The container color description. It gives the color information that the AV1 sequence header does not specify.
    /// </param>
    /// <param name="profile">The source ICC profile for the color conversion, or <see langword="null"/> to keep the source colors.</param>
    /// <param name="alphaFrame">The native auxiliary plane, or <see langword="null"/> for an opaque image.</param>
    /// <param name="alphaOutputSize">The full color extent that the auxiliary plane covers.</param>
    /// <param name="alphaRectangle">The matching region in the auxiliary presentation.</param>
    /// <param name="premultiplied"><see langword="true"/> if alpha premultiplies the source RGB values.</param>
    /// <param name="sourceRectangle">The source area of interest in luma-sample coordinates.</param>
    /// <param name="transform">The rotation and mirroring that the method applies in the destination region.</param>
    /// <param name="destination">
    /// Gets the destination pixel region. The call can allocate the output image. Thus, the method calls it only after it validates the spatial extent of
    /// the item against the decoded sequence header. If the mode ignores damage and the payload gives no frame, the method does not call it.
    /// </param>
    /// <param name="metadata">The metadata that receives the decoded image properties.</param>
    /// <param name="cancellationToken">The token that cancels the payload decode.</param>
    /// <exception cref="InvalidImageContentException">
    /// The spatial extent of the item is larger than the maximum frame size of its sequence header, and the decoder cannot ignore the error. The decoder
    /// ignores the error only if an image exists and the mode ignores image data errors.
    /// </exception>
    public void DecodeItemData(
        DecoderOptions options,
        HeifChromaUpsampling chromaUpsampling,
        HeifItem item,
        Span<byte> data,
        CicpProfile? colorProfile,
        IccProfile? profile,
        Av1FrameBuffer<byte>? alphaFrame,
        Size alphaOutputSize,
        Rectangle alphaRectangle,
        bool premultiplied,
        Rectangle sourceRectangle,
        HeifPixelTransform transform,
        Func<Buffer2DRegion<TPixel>> destination,
        ImageMetadata metadata,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // The item graph validator validated the item properties before the decode. The codec configuration exists, and the pixel information agrees with
        // it. The AV1 code reports damage one time, at the check site that finds it. A false or null result means that the mode ignored the damage and that
        // the payload holds no frame. Then the item writes no pixels, and its region keeps the fill value.
        Av1CodecConfiguration codecConfiguration = item.Av1CodecConfiguration!;
        if (!this.TryGetItemData(options, item, data, out Span<byte> itemData)
            || !codecConfiguration.ValidateData(
                itemData,
                true,
                "AV1 image item",
                item.ContentLightLevel,
                item.MasteringDisplayColorVolume,
                options,
                this.imageExists,
                out HeifContentLightLevel? obuContentLightLevel,
                out HeifMasteringDisplayColorVolume? obuMasteringDisplayColorVolume))
        {
            return;
        }

        byte operatingPointIndex = item.Av1OperatingPointSelector?.Index ?? 0;

        using Av1Decoder decoder = new(options.Configuration, operatingPointIndex, options.SegmentIntegrityHandling, this.imageExists);
        using Av1FrameBuffer<byte>? frameBuffer = decoder.DecodeFrameBuffer(
            itemData,
            colorProfile,
            codecConfiguration,
            out CicpProfile? effectiveColorProfile,
            item.Av1LayeredImageIndex);

        if (frameBuffer is null)
        {
            return;
        }

        // The image spatial extents property gives the size of the reconstructed image, before the clean aperture,
        // rotation, and mirror properties apply. For an AV1 item it shall equal the upscaled width and the height of
        // the frame that the selected operating point or layer outputs. Layered files exist that declare the size of
        // the top layer while a layer selector selects a smaller layer, and readers scale the decoded frame to the
        // declared size. This decoder does the same, so the extents and the frame size can differ. Every frame of
        // the stream is at most the maximum frame size of its sequence header, so an extent larger than that size
        // describes no frame of the payload. Reject it before the caller allocates the output image, so that a small
        // payload cannot make the decoder allocate and scale an image of an arbitrary declared size.
        ObuSequenceHeader sequenceHeader = decoder.SequenceHeader!;
        if (item.Extent.Width > sequenceHeader.MaxFrameWidth || item.Extent.Height > sequenceHeader.MaxFrameHeight)
        {
            string message = $"AV1 image item {item.Id} declares spatial extents {item.Extent.Width}x{item.Extent.Height}, " +
                $"which exceed the maximum frame size {sequenceHeader.MaxFrameWidth}x{sequenceHeader.MaxFrameHeight} of its sequence header.";

            options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError(this.imageExists, message);
            return;
        }

        // The container range controls the presentation. The bitstream range stays with the decoded planes.
        Av1YuvConverter.ConvertToRgb(
            options.Configuration,
            frameBuffer,
            sourceRectangle,
            destination(),
            item.Extent,
            transform,
            profile,
            alphaFrame,
            alphaOutputSize,
            alphaRectangle,
            premultiplied,
            chromaUpsampling,
            colorProfile?.FullRange ?? frameBuffer.ColorConfig.ColorRange);

        metadata.CicpProfile = effectiveColorProfile;
        HeifMetadata heifMetadata = metadata.GetHeifMetadata();
        heifMetadata.BitDepth = codecConfiguration.BitDepth;
        heifMetadata.IsMonochrome = codecConfiguration.IsMonochrome;
        heifMetadata.ChromaSubsampling = codecConfiguration.GetChromaSubsampling();
        heifMetadata.ContentLightLevel = item.ContentLightLevel ?? obuContentLightLevel;
        heifMetadata.MasteringDisplayColorVolume = item.MasteringDisplayColorVolume ?? obuMasteringDisplayColorVolume;
    }

    /// <inheritdoc/>
    public Av1FrameBuffer<byte>? DecodeAlphaItemData(DecoderOptions options, HeifItem item, Span<byte> data, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // The item graph validator validated that the codec configuration of the alpha item exists and is monochrome. The AV1 code reports a damaged
        // payload one time. A false or null result means that the mode ignored the damage and that the payload holds no frame.
        Av1CodecConfiguration codecConfiguration = item.Av1CodecConfiguration!;
        if (!this.TryGetItemData(options, item, data, out Span<byte> itemData)
            || !codecConfiguration.ValidateData(
                itemData,
                true,
                "AV1 image item",
                item.ContentLightLevel,
                item.MasteringDisplayColorVolume,
                options,
                this.imageExists,
                out _,
                out _))
        {
            return null;
        }

        byte operatingPointIndex = item.Av1OperatingPointSelector?.Index ?? 0;

        using Av1Decoder decoder = new(options.Configuration, operatingPointIndex, options.SegmentIntegrityHandling, this.imageExists);
        return decoder.DecodeFrameBuffer(itemData, item.CicpProfile, codecConfiguration, out _, item.Av1LayeredImageIndex);
    }

    /// <summary>
    /// Gets the cumulative item bytes that an explicit AV1 spatial-layer selection requires.
    /// </summary>
    /// <param name="options">The decoder options for the HEIF decode.</param>
    /// <param name="item">The AV1 image item that holds the optional layered-image properties.</param>
    /// <param name="data">
    /// The logical image-item payload. If the file is truncated and the item data reader ignores image data errors, the payload is shorter than the
    /// declared extents.
    /// </param>
    /// <param name="itemData">
    /// Receives the payload for final-layer decoding, or the cumulative prefix through the selected layer. Each payload has at most the bytes that
    /// <paramref name="data"/> holds.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the layers fit the item. <see langword="false"/> if the layered-image index reported an incorrect layer layout and the
    /// mode ignores it.
    /// </returns>
    private bool TryGetItemData(DecoderOptions options, HeifItem item, Span<byte> data, out Span<byte> itemData)
    {
        itemData = data;
        Av1LayeredImageIndex? layeredImageIndex = item.Av1LayeredImageIndex;
        if (layeredImageIndex is null)
        {
            return true;
        }

        // The layer sizes describe the declared item data, so the declared length validates them. The item data
        // reader limits the declared length to int.MaxValue. When the file is truncated, the reader already reported
        // the error, and the selected layers end at the bytes that the item holds.
        long declaredLength = 0;
        foreach (HeifLocation location in item.DataLocations)
        {
            declaredLength += location.Length;
        }

        SegmentIntegrityHandling handling = options.SegmentIntegrityHandling;
        if (!layeredImageIndex.Value.TryGetPayloadLength((int)declaredLength, item.Av1LayerSelector, handling, this.imageExists, out int payloadLength))
        {
            return false;
        }

        itemData = data[..Math.Min(payloadLength, data.Length)];
        return true;
    }
}
