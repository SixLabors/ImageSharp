// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Color;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.PixelFormats.Utils;

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

/// <summary>
/// Decodes one AV1 still-image elementary stream into an ImageSharp image.
/// </summary>
internal sealed class Av1Decoder : IAv1TileReader, IDisposable
{
    /// <summary>
    /// The open-bitstream-unit parser for the current image item.
    /// </summary>
    private readonly ObuReader obuReader;

    /// <summary>
    /// The configuration used for decoded image and scratch-memory allocation.
    /// </summary>
    private readonly Configuration configuration;

    /// <summary>
    /// The tile parser shared by all tile groups in the current frame.
    /// </summary>
    private Av1TileReader? tileReader;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1Decoder"/> class.
    /// </summary>
    /// <param name="configuration">The configuration used for image and scratch-memory allocation.</param>
    public Av1Decoder(Configuration configuration)
        : this(configuration, 0)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1Decoder"/> class for one selected AV1 operating point.
    /// </summary>
    /// <param name="configuration">The configuration used for image and scratch-memory allocation.</param>
    /// <param name="operatingPointIndex">The zero-based sequence-header operating-point index to decode.</param>
    public Av1Decoder(Configuration configuration, byte operatingPointIndex)
    {
        this.configuration = configuration;
        this.obuReader = new(operatingPointIndex);
    }

    /// <summary>
    /// Gets the decoded frame header, or <see langword="null"/> before the stream provides one.
    /// </summary>
    public ObuFrameHeader? FrameHeader { get; private set; }

    /// <summary>
    /// Gets the decoded sequence header, or <see langword="null"/> before the stream provides one.
    /// </summary>
    public ObuSequenceHeader? SequenceHeader { get; private set; }

    /// <summary>
    /// Gets the tile and superblock state for the decoded frame, or <see langword="null"/> before tile parsing completes.
    /// </summary>
    public Av1FrameInfo? FrameInfo { get; private set; }

    /// <summary>
    /// Decodes an AV1 still-image elementary stream.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel type.</typeparam>
    /// <param name="buffer">The complete AV1 elementary-stream payload.</param>
    /// <param name="containerColorProfile">
    /// The container color description that supplies unspecified sequence-header color information.
    /// </param>
    /// <param name="codecConfiguration">
    /// The item-associated AV1 codec configuration validated against the coded sequence header.
    /// </param>
    /// <returns>The decoded image.</returns>
    public Image<TPixel> Decode<TPixel>(
        Span<byte> buffer,
        CicpProfile? containerColorProfile = null,
        Av1CodecConfiguration? codecConfiguration = null)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ImageFrame<TPixel> frame = this.DecodeFrame<TPixel>(buffer, containerColorProfile, codecConfiguration, out CicpProfile effectiveColorProfile);
        ImageMetadata metadata = new()
        {
            CicpProfile = effectiveColorProfile
        };

        try
        {
            return new Image<TPixel>(this.configuration, metadata, [frame]);
        }
        catch
        {
            // Ownership transfers only after the image constructor accepts the decoded frame.
            frame.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Decodes an AV1 elementary-stream payload into one independently owned ImageSharp frame.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel type.</typeparam>
    /// <param name="buffer">The complete AV1 elementary-stream payload.</param>
    /// <param name="containerColorProfile">
    /// The container color description that supplies unspecified sequence-header color information.
    /// </param>
    /// <param name="codecConfiguration">
    /// The AV1 codec configuration validated against the coded sequence header.
    /// </param>
    /// <param name="effectiveColorProfile">Receives the effective CICP description used for conversion.</param>
    /// <returns>The decoded frame. Ownership transfers to the caller.</returns>
    public ImageFrame<TPixel> DecodeFrame<TPixel>(
        Span<byte> buffer,
        CicpProfile? containerColorProfile,
        Av1CodecConfiguration? codecConfiguration,
        out CicpProfile effectiveColorProfile)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Av1FrameBuffer<byte> frameBuffer = this.DecodeFrameBuffer(
            buffer,
            containerColorProfile,
            codecConfiguration,
            out effectiveColorProfile);

        ImageFrame<TPixel>? resultFrame = null;
        try
        {
            resultFrame = new ImageFrame<TPixel>(
                this.configuration,
                this.FrameHeader!.FrameSize.SuperResolutionUpscaledWidth,
                this.FrameHeader.FrameSize.FrameHeight);

            Av1YuvConverter.ConvertToRgb(this.configuration, frameBuffer, resultFrame);
            resultFrame.Metadata.CicpProfile = effectiveColorProfile.DeepClone();
            return resultFrame;
        }
        catch
        {
            resultFrame?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Decodes an AV1 elementary-stream payload and composes its luma plane directly into a packed color frame.
    /// </summary>
    /// <typeparam name="TPixel">The destination color pixel type.</typeparam>
    /// <param name="buffer">The complete AV1 elementary-stream payload.</param>
    /// <param name="containerColorProfile">
    /// The container color description that supplies unspecified sequence-header color information.
    /// </param>
    /// <param name="codecConfiguration">The AV1 codec configuration validated against the coded sequence header.</param>
    /// <param name="expectedCodedSize">The required coded dimensions, or an empty size when the item extent may differ.</param>
    /// <param name="destination">The packed color frame receiving alpha values.</param>
    /// <param name="outputSize">The complete presented size of the auxiliary image or grid tile.</param>
    /// <param name="destinationRectangle">The destination region receiving the top-left portion of the presented alpha image.</param>
    /// <param name="premultiplied">Whether stored color samples must be converted to unassociated alpha.</param>
    public void DecodeAlpha<TPixel>(
        Span<byte> buffer,
        CicpProfile? containerColorProfile,
        Av1CodecConfiguration? codecConfiguration,
        Size expectedCodedSize,
        ImageFrame<TPixel> destination,
        Size outputSize,
        Rectangle destinationRectangle,
        bool premultiplied)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Av1FrameBuffer<byte> frameBuffer = this.DecodeFrameBuffer(buffer, containerColorProfile, codecConfiguration, out _);
        if (expectedCodedSize != default && (frameBuffer.Width != expectedCodedSize.Width || frameBuffer.Height != expectedCodedSize.Height))
        {
            throw new InvalidImageContentException("The decoded alpha sample dimensions do not match its visual sample entry.");
        }

        if (frameBuffer.ColorFormat != Av1ColorFormat.Yuv400)
        {
            // AVIF auxiliary alpha is the luma plane of an AV1 monochrome image. Accepting chroma-bearing payloads
            // would silently reinterpret a color image and contradict the Sequence Header mono_chrome requirement.
            throw new InvalidImageContentException("An AV1 auxiliary alpha image must be encoded as monochrome.");
        }

        Av1YuvConverter.ComposeAlpha(
            this.configuration,
            frameBuffer,
            destination,
            outputSize,
            destinationRectangle,
            premultiplied);
    }

    /// <summary>
    /// Parses and reconstructs one AV1 frame while retaining its native component planes for the caller.
    /// </summary>
    /// <param name="buffer">The complete AV1 elementary-stream payload.</param>
    /// <param name="containerColorProfile">
    /// The container color description that supplies unspecified sequence-header color information.
    /// </param>
    /// <param name="codecConfiguration">The AV1 codec configuration validated against the coded sequence header.</param>
    /// <param name="effectiveColorProfile">Receives the effective CICP description associated with the native planes.</param>
    /// <returns>The reconstructed native frame buffer. Ownership transfers to the caller.</returns>
    public Av1FrameBuffer<byte> DecodeFrameBuffer(
        Span<byte> buffer,
        CicpProfile? containerColorProfile,
        Av1CodecConfiguration? codecConfiguration,
        out CicpProfile effectiveColorProfile)
    {
        Av1BitStreamReader reader = new(buffer);
        this.obuReader.ReadAll(ref reader, buffer.Length, () => this, false);
        Guard.NotNull(this.tileReader, nameof(this.tileReader));
        Guard.NotNull(this.SequenceHeader, nameof(this.SequenceHeader));
        Guard.NotNull(this.FrameHeader, nameof(this.FrameHeader));
        codecConfiguration?.Validate(this.SequenceHeader);

        if (containerColorProfile is not null)
        {
            ObuColorConfig colorConfig = this.SequenceHeader.ColorConfig;
            ObuColorPrimaries containerColorPrimaries = (ObuColorPrimaries)containerColorProfile.ColorPrimaries;
            ObuTransferCharacteristics containerTransferCharacteristics =
                (ObuTransferCharacteristics)containerColorProfile.TransferCharacteristics;

            ObuMatrixCoefficients containerMatrixCoefficients =
                (ObuMatrixCoefficients)containerColorProfile.MatrixCoefficients;

            // AV1-ISOBMFF permits nclx to supply only bitstream fields explicitly coded as unspecified. A
            // different specified value is a conformance error rather than a container-level color override.
            if (colorConfig.ColorPrimaries == ObuColorPrimaries.Unspecified)
            {
                colorConfig.ColorPrimaries = containerColorPrimaries;
            }
            else if (colorConfig.ColorPrimaries != containerColorPrimaries)
            {
                throw new InvalidImageContentException("The HEIF CICP color primaries do not match the AV1 sequence header.");
            }

            if (colorConfig.TransferCharacteristics == ObuTransferCharacteristics.Unspecified)
            {
                colorConfig.TransferCharacteristics = containerTransferCharacteristics;
            }
            else if (colorConfig.TransferCharacteristics != containerTransferCharacteristics)
            {
                throw new InvalidImageContentException("The HEIF CICP transfer characteristics do not match the AV1 sequence header.");
            }

            if (colorConfig.MatrixCoefficients == ObuMatrixCoefficients.Unspecified)
            {
                colorConfig.MatrixCoefficients = containerMatrixCoefficients;
            }
            else if (colorConfig.MatrixCoefficients != containerMatrixCoefficients)
            {
                throw new InvalidImageContentException("The HEIF CICP matrix coefficients do not match the AV1 sequence header.");
            }

            if (colorConfig.ColorRange != containerColorProfile.FullRange)
            {
                throw new InvalidImageContentException("The HEIF CICP color range does not match the AV1 sequence header.");
            }
        }

        this.FrameInfo = this.tileReader.FrameInfo;
        Av1FrameBuffer<byte> frameBuffer = new(
            this.configuration,
            this.SequenceHeader,
            this.SequenceHeader.ColorConfig.GetColorFormat(),
            false);

        try
        {
            using Av1FrameDecoder frameDecoder = new(this.SequenceHeader, this.FrameHeader, this.FrameInfo, frameBuffer);
            frameDecoder.DecodeFrame();

            // Preserve the effective CICP description used for conversion, including container values that legally
            // supplied unspecified bitstream fields. This also exposes bitstream-only color metadata to callers.
            ObuColorConfig effectiveColorConfig = this.SequenceHeader.ColorConfig;
            effectiveColorProfile = new CicpProfile(
                (byte)effectiveColorConfig.ColorPrimaries,
                (byte)effectiveColorConfig.TransferCharacteristics,
                (byte)effectiveColorConfig.MatrixCoefficients,
                effectiveColorConfig.ColorRange);

            return frameBuffer;
        }
        catch
        {
            frameBuffer.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Parses one entropy-coded tile payload into the current frame state.
    /// </summary>
    /// <param name="tileData">The entropy-coded tile payload.</param>
    /// <param name="tileNum">The raster-order tile index.</param>
    public void ReadTile(Span<byte> tileData, int tileNum)
    {
        if (this.tileReader is null)
        {
            this.SequenceHeader = this.obuReader.SequenceHeader;
            this.FrameHeader = this.obuReader.FrameHeader;
            Guard.NotNull(this.SequenceHeader, nameof(this.SequenceHeader));
            Guard.NotNull(this.FrameHeader, nameof(this.FrameHeader));

            // Every tile group in a frame contributes to the same mode-info and coefficient state.
            this.tileReader = new Av1TileReader(this.configuration, this.SequenceHeader, this.FrameHeader);
        }

        this.tileReader.ReadTile(tileData, tileNum);
    }

    /// <summary>
    /// Releases the tile reader and its frame-scoped parsing storage.
    /// </summary>
    public void Dispose()
    {
        this.tileReader?.Dispose();
        this.tileReader = null;
    }
}
