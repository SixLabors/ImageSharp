// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Color;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Cdef;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.FilmGrain;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopRestoration;
using SixLabors.ImageSharp.Formats.Heif.Av1.ReferenceFrames;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.PixelFormats.Utils;

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

/// <summary>
/// Decodes bounded AV1 image payloads and image-sequence samples into ImageSharp frames.
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
    /// The CDEF filtering stage and working storage shared by successive frames.
    /// </summary>
    private readonly Av1CdefDecoder cdefDecoder;

    /// <summary>
    /// The restoration boundary rows shared by successive frames.
    /// </summary>
    private readonly Av1LoopRestorationBoundary restorationBoundary;

    /// <summary>
    /// The restoration stage and working storage shared by successive frames.
    /// </summary>
    private readonly Av1LoopRestorationDecoder restorationDecoder;

    /// <summary>
    /// Reusable luma palette indices for the coding blocks in one superblock.
    /// </summary>
    private readonly Buffer2D<byte> lumaPaletteColorIndexMap;

    /// <summary>
    /// Reusable chroma palette indices for the coding blocks in one superblock.
    /// </summary>
    private readonly Buffer2D<byte> chromaPaletteColorIndexMap;

    /// <summary>
    /// The shared backing owner for both reusable palette maps.
    /// </summary>
    private readonly IMemoryOwner<byte> paletteColorIndexMapOwner;

    /// <summary>
    /// The reconstructed references and selected presentation output owned by the current bounded decode session.
    /// </summary>
    private readonly Av1ReferenceFrameStore referenceFrames = new();

    /// <summary>
    /// The frame-base, tile-working, and published entropy contexts created for the first coded frame and then reused
    /// for this bounded decoder session.
    /// </summary>
    private Av1FrameEntropyContexts? entropyContexts;

    /// <summary>
    /// The coded sequence governing the active reference map and reusable entropy session.
    /// </summary>
    private ObuSequenceHeader? entropySequenceHeader;

    /// <summary>
    /// The item codec configuration validated before reconstructing a completed frame.
    /// </summary>
    private Av1CodecConfiguration? codecConfiguration;

    /// <summary>
    /// The container color description applied before reconstructing a completed frame.
    /// </summary>
    private CicpProfile? containerColorProfile;

    /// <summary>
    /// The sequence header already validated for the current bounded payload.
    /// </summary>
    private ObuSequenceHeader? validatedSequenceHeader;

    /// <summary>
    /// The complete parser, sample buffer, and reconstruction state for the frame currently being decoded.
    /// </summary>
    private FrameDecodeState? frameDecodeState;

    /// <summary>
    /// Retains reconstruction scratch across frames; the active frame borrows its memory until completion.
    /// </summary>
    private IMemoryOwner<short>? reconstructionWorkspace;

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
        this.obuReader = new(operatingPointIndex, this.referenceFrames);
        this.cdefDecoder = new(configuration.MemoryAllocator);
        this.restorationBoundary = new(configuration.MemoryAllocator);
        this.restorationDecoder = new(configuration.MemoryAllocator);

        // Sequential tile decoding needs only the palette indices belonging to the current superblock. One fixed
        // owner keeps both maximum-superblock maps reusable across the bounded session without fragmented group rents.
        int paletteMapLength = 1 << Av1Constants.MaxSuperBlockSizeLog2;
        int paletteMapArea = paletteMapLength * paletteMapLength;
        this.paletteColorIndexMapOwner = configuration.MemoryAllocator.Allocate<byte>(2 * paletteMapArea);
        Memory<byte> paletteMaps = this.paletteColorIndexMapOwner.Memory;
        this.lumaPaletteColorIndexMap = Buffer2D<byte>.WrapMemory(
            paletteMaps[..paletteMapArea],
            paletteMapLength,
            paletteMapLength);

        this.chromaPaletteColorIndexMap = Buffer2D<byte>.WrapMemory(
            paletteMaps[paletteMapArea..],
            paletteMapLength,
            paletteMapLength);
    }

    /// <summary>
    /// Gets the final retained shown-frame header, or <see langword="null"/> before a shown frame completes.
    /// </summary>
    public ObuFrameHeader? FrameHeader { get; private set; }

    /// <summary>
    /// Gets the sequence header governing the final retained shown frame, or <see langword="null"/> before one completes.
    /// </summary>
    public ObuSequenceHeader? SequenceHeader { get; private set; }

    /// <summary>
    /// Gets tile and superblock state for the most recently reconstructed frame, or <see langword="null"/> when no
    /// frame was reconstructed or the output selected an existing reference without new tile syntax.
    /// </summary>
    public Av1FrameInfo? FrameInfo { get; private set; }

    /// <summary>
    /// Gets the inter-prediction features selected by every coded frame completed in the most recently decoded payload.
    /// </summary>
    public Av1InterPredictionFeatures DecodedInterPredictionFeatures { get; private set; }

    /// <summary>
    /// Gets or sets the chroma reconstruction mode for presented sequence frames.
    /// </summary>
    public HeifChromaUpsampling ChromaUpsampling { get; set; }

    /// <summary>
    /// Gets the native planes of the current retained shown frame, or <see langword="null"/> before one completes.
    /// </summary>
    public Av1FrameBuffer<byte>? FrameBuffer => this.referenceFrames.OutputFrame?.FrameBuffer;

    /// <summary>
    /// Decodes the next visible sample in a bounded AV1 image sequence directly into a caller-owned frame.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel type.</typeparam>
    /// <param name="buffer">The complete AV1 sample payload.</param>
    /// <param name="containerColorProfile">The container color description.</param>
    /// <param name="codecConfiguration">The AV1 sample-entry configuration.</param>
    /// <param name="expectedCodedSize">The coded dimensions declared by the visual sample entry.</param>
    /// <param name="sourceRectangle">The clean-aperture region mapped to the complete destination frame.</param>
    /// <param name="destination">The caller-owned packed-pixel frame receiving the presented sample.</param>
    /// <param name="transform">The rotation and mirroring applied within the destination region.</param>
    /// <param name="profile">The source profile selected for conversion, or null to preserve source colors.</param>
    /// <param name="alphaFrame">The decoder-owned auxiliary frame, or null for opaque pixels.</param>
    /// <param name="premultiplied">Whether source RGB is associated with alpha.</param>
    public CicpProfile DecodeSequenceFrame<TPixel>(
        Span<byte> buffer,
        CicpProfile? containerColorProfile,
        Av1CodecConfiguration? codecConfiguration,
        Size expectedCodedSize,
        Rectangle sourceRectangle,
        Buffer2DRegion<TPixel> destination,
        HeifPixelTransform transform,
        IccProfile? profile,
        Av1FrameBuffer<byte>? alphaFrame,
        bool premultiplied)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        CicpProfile effectiveColorProfile = this.DecodePayload(
            buffer,
            containerColorProfile,
            codecConfiguration,
            null,
            requireShownFrame: true);

        Av1ReferenceFrame outputFrame = this.referenceFrames.ResolveOutput();
        Size codedSize = new(
            outputFrame.FrameHeader.FrameSize.SuperResolutionUpscaledWidth,
            outputFrame.FrameHeader.FrameSize.FrameHeight);

        if (codedSize != expectedCodedSize)
        {
            throw new InvalidImageContentException(
                "The decoded image-sequence sample dimensions do not match its visual sample entry.");
        }

        // Keep reconstructed reference state unchanged while honoring the container's presentation range.
        Av1YuvConverter.ConvertToRgb(
            this.configuration,
            outputFrame.FrameBuffer,
            sourceRectangle,
            destination,
            codedSize,
            transform,
            profile,
            alphaFrame,
            codedSize,
            sourceRectangle,
            premultiplied,
            this.ChromaUpsampling,
            containerColorProfile?.FullRange ?? outputFrame.FrameBuffer.ColorConfig.ColorRange);

        return effectiveColorProfile;
    }

    /// <summary>
    /// Decodes one non-presented AV1 image-sequence sample while retaining its reference state.
    /// </summary>
    /// <param name="buffer">The complete AV1 sample payload.</param>
    /// <param name="containerColorProfile">The container color description.</param>
    /// <param name="codecConfiguration">The AV1 sample-entry configuration.</param>
    public void DecodeSequenceReference(
        Span<byte> buffer,
        CicpProfile? containerColorProfile,
        Av1CodecConfiguration? codecConfiguration)
        => _ = this.DecodePayload(
            buffer,
            containerColorProfile,
            codecConfiguration,
            null,
            requireShownFrame: false);

    /// <summary>
    /// Decodes the next visible monochrome AV1 sequence sample into native samples.
    /// </summary>
    /// <param name="buffer">The complete AV1 sample payload.</param>
    /// <param name="containerColorProfile">The container color description.</param>
    /// <param name="codecConfiguration">The AV1 sample-entry configuration.</param>
    /// <param name="expectedCodedSize">The required coded dimensions.</param>
    /// <returns>The auxiliary frame retained by this decoder until the next sample is decoded or the decoder is disposed.</returns>
    public Av1FrameBuffer<byte> DecodeSequenceAlpha(
        Span<byte> buffer,
        CicpProfile? containerColorProfile,
        Av1CodecConfiguration? codecConfiguration,
        Size expectedCodedSize)
    {
        _ = this.DecodePayload(
            buffer,
            containerColorProfile,
            codecConfiguration,
            null,
            requireShownFrame: true);

        Av1ReferenceFrame outputFrame = this.referenceFrames.ResolveOutput();
        Av1FrameBuffer<byte> frame = outputFrame.FrameBuffer;
        if (frame.Width != expectedCodedSize.Width || frame.Height != expectedCodedSize.Height)
        {
            throw new InvalidImageContentException("The decoded alpha sample dimensions do not match its visual sample entry.");
        }

        if (frame.ColorFormat != Av1ColorFormat.Yuv400)
        {
            throw new InvalidImageContentException("An AV1 alpha sample must be monochrome.");
        }

        return frame;
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
    /// <param name="transform">The rotation and mirroring applied within the destination region.</param>
    /// <param name="layeredImageIndex">The optional byte boundaries of a layered AV1 image item.</param>
    public void DecodeAlpha<TPixel>(
        Span<byte> buffer,
        CicpProfile? containerColorProfile,
        Av1CodecConfiguration? codecConfiguration,
        Size expectedCodedSize,
        Buffer2DRegion<TPixel> destination,
        Size outputSize,
        Rectangle destinationRectangle,
        bool premultiplied,
        HeifPixelTransform transform,
        Av1LayeredImageIndex? layeredImageIndex = null)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Av1FrameBuffer<byte> frameBuffer = this.DecodeFrameBuffer(
            buffer,
            containerColorProfile,
            codecConfiguration,
            out _,
            layeredImageIndex);

        this.ComposeAlpha(
            frameBuffer,
            expectedCodedSize,
            destination,
            outputSize,
            destinationRectangle,
            premultiplied,
            transform);
    }

    /// <summary>
    /// Composes one decoded monochrome plane into a packed color frame.
    /// </summary>
    /// <typeparam name="TPixel">The destination color pixel type.</typeparam>
    /// <param name="frameBuffer">The decoded monochrome planes.</param>
    /// <param name="expectedCodedSize">The required coded dimensions.</param>
    /// <param name="destination">The packed color frame receiving alpha values.</param>
    /// <param name="outputSize">The complete presented size of the auxiliary image.</param>
    /// <param name="destinationRectangle">The destination region receiving the alpha image.</param>
    /// <param name="premultiplied">Whether stored color samples must be converted to unassociated alpha.</param>
    /// <param name="transform">The rotation and mirroring applied within the destination region.</param>
    private void ComposeAlpha<TPixel>(
        Av1FrameBuffer<byte> frameBuffer,
        Size expectedCodedSize,
        Buffer2DRegion<TPixel> destination,
        Size outputSize,
        Rectangle destinationRectangle,
        bool premultiplied,
        HeifPixelTransform transform)
        where TPixel : unmanaged, IPixel<TPixel>
        => this.ComposeAlpha(
            frameBuffer,
            expectedCodedSize,
            new Rectangle(0, 0, frameBuffer.Width, frameBuffer.Height),
            destination,
            outputSize,
            destinationRectangle,
            premultiplied,
            transform);

    /// <summary>
    /// Composes one decoded monochrome region into a packed color frame.
    /// </summary>
    /// <typeparam name="TPixel">The destination color pixel type.</typeparam>
    /// <param name="frameBuffer">The decoded monochrome planes.</param>
    /// <param name="expectedCodedSize">The required coded dimensions.</param>
    /// <param name="sourceRectangle">The luma region mapped to the destination rectangle.</param>
    /// <param name="destination">The packed color frame receiving alpha values.</param>
    /// <param name="outputSize">The complete presented size of the auxiliary image.</param>
    /// <param name="destinationRectangle">The destination region receiving the alpha image.</param>
    /// <param name="premultiplied">Whether stored color samples must be converted to unassociated alpha.</param>
    /// <param name="transform">The rotation and mirroring applied within the destination region.</param>
    private void ComposeAlpha<TPixel>(
        Av1FrameBuffer<byte> frameBuffer,
        Size expectedCodedSize,
        Rectangle sourceRectangle,
        Buffer2DRegion<TPixel> destination,
        Size outputSize,
        Rectangle destinationRectangle,
        bool premultiplied,
        HeifPixelTransform transform)
        where TPixel : unmanaged, IPixel<TPixel>
    {
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
            sourceRectangle,
            destination,
            outputSize,
            destinationRectangle,
            premultiplied,
            transform);
    }

    /// <summary>
    /// Parses every coded frame in an AV1 payload and returns the final shown frame's native component planes.
    /// </summary>
    /// <param name="buffer">The complete AV1 elementary-stream payload.</param>
    /// <param name="containerColorProfile">
    /// The container color description that supplies unspecified sequence-header color information.
    /// </param>
    /// <param name="codecConfiguration">The AV1 codec configuration validated against the coded sequence header.</param>
    /// <param name="effectiveColorProfile">Receives the effective CICP description associated with the native planes.</param>
    /// <param name="layeredImageIndex">The optional byte boundaries of a layered AV1 image item.</param>
    /// <returns>The reconstructed native frame buffer. Ownership transfers to the caller.</returns>
    public Av1FrameBuffer<byte> DecodeFrameBuffer(
        Span<byte> buffer,
        CicpProfile? containerColorProfile,
        Av1CodecConfiguration? codecConfiguration,
        out CicpProfile effectiveColorProfile,
        Av1LayeredImageIndex? layeredImageIndex = null)
        => this.DecodeFrameBuffer(
            buffer,
            containerColorProfile,
            codecConfiguration,
            out effectiveColorProfile,
            out _,
            layeredImageIndex);

    /// <summary>
    /// Parses every coded frame in an AV1 payload and returns the final shown frame's native planes and header.
    /// </summary>
    private Av1FrameBuffer<byte> DecodeFrameBuffer(
        Span<byte> buffer,
        CicpProfile? containerColorProfile,
        Av1CodecConfiguration? codecConfiguration,
        out CicpProfile effectiveColorProfile,
        out ObuFrameHeader frameHeader,
        Av1LayeredImageIndex? layeredImageIndex)
    {
        effectiveColorProfile = this.DecodePayload(
            buffer,
            containerColorProfile,
            codecConfiguration,
            layeredImageIndex,
            requireShownFrame: true);

        using Av1ReferenceFrame outputFrame = this.referenceFrames.TakeOutput();
        frameHeader = outputFrame.FrameHeader;
        return outputFrame.TakeFrameBuffer();
    }

    /// <summary>
    /// Parses one bounded payload into the retained decoder session.
    /// </summary>
    /// <param name="buffer">The complete AV1 payload.</param>
    /// <param name="containerColorProfile">The container color description.</param>
    /// <param name="codecConfiguration">The AV1 codec configuration.</param>
    /// <param name="layeredImageIndex">The optional layer byte boundaries.</param>
    /// <param name="requireShownFrame">Whether the payload must produce a shown frame.</param>
    /// <returns>The effective CICP description.</returns>
    private CicpProfile DecodePayload(
        Span<byte> buffer,
        CicpProfile? containerColorProfile,
        Av1CodecConfiguration? codecConfiguration,
        Av1LayeredImageIndex? layeredImageIndex,
        bool requireShownFrame)
    {
        this.codecConfiguration = codecConfiguration;
        this.containerColorProfile = containerColorProfile;
        this.validatedSequenceHeader = null;
        this.obuReader.ResetMetadata();
        this.SequenceHeader = null;
        this.FrameHeader = null;
        this.DecodedInterPredictionFeatures = Av1InterPredictionFeatures.None;

        // Full tile syntax describes only frames reconstructed by this payload. Reference slots already own the compact
        // state needed by later frames, so release the previous payload's reconstruction graph before parsing the next.
        this.FrameInfo?.ReleaseOwner();
        this.FrameInfo = null;

        try
        {
            if (layeredImageIndex is null)
            {
                Av1BitStreamReader reader = new(buffer);
                this.obuReader.ReadAll(ref reader, buffer.Length, this, false);
            }
            else
            {
                int layerOffset = 0;
                for (int layer = 0; layer < Av1Constants.MaxSpatialLayerCount - 1 && layerOffset < buffer.Length; layer++)
                {
                    uint declaredLayerSize = layer switch
                    {
                        0 => layeredImageIndex.Value.FirstLayerSize,
                        1 => layeredImageIndex.Value.SecondLayerSize,
                        _ => layeredImageIndex.Value.ThirdLayerSize
                    };

                    if (declaredLayerSize == 0)
                    {
                        break;
                    }

                    int layerSize = (int)declaredLayerSize;
                    Av1BitStreamReader layerReader = new(buffer.Slice(layerOffset, layerSize));
                    this.obuReader.ReadAll(ref layerReader, layerSize, this, false);
                    layerOffset += layerSize;
                }

                if (layerOffset < buffer.Length)
                {
                    Span<byte> finalLayer = buffer[layerOffset..];
                    Av1BitStreamReader finalLayerReader = new(finalLayer);
                    this.obuReader.ReadAll(ref finalLayerReader, finalLayer.Length, this, false);
                }
            }

            ObuSequenceHeader sequenceHeader = this.obuReader.SequenceHeader
                ?? throw new InvalidImageContentException("The AV1 payload contains no sequence header.");

            if (requireShownFrame)
            {
                _ = this.referenceFrames.ResolveOutput();
            }

            // Preserve the effective CICP description used for conversion, including container values that legally
            // supplied unspecified bitstream fields. This also exposes bitstream-only color metadata to callers.
            ObuColorConfig effectiveColorConfig = sequenceHeader.ColorConfig;
            return new CicpProfile(
                (byte)effectiveColorConfig.ColorPrimaries,
                (byte)effectiveColorConfig.TransferCharacteristics,
                (byte)effectiveColorConfig.MatrixCoefficients,
                effectiveColorConfig.ColorRange);
        }
        catch
        {
            // A failed frame may own pooled neighbor contexts while earlier layers own reconstructed references and
            // published CDF snapshots. None can be reused after a non-transactional frame transition has failed.
            this.frameDecodeState?.Dispose();
            this.frameDecodeState = null;
            this.obuReader.Reset();
            this.entropyContexts?.Reset();
            this.entropySequenceHeader = null;
            this.SequenceHeader = null;
            this.FrameHeader = null;
            this.DecodedInterPredictionFeatures = Av1InterPredictionFeatures.None;
            this.FrameInfo?.ReleaseOwner();
            this.FrameInfo = null;
            throw;
        }
        finally
        {
            // Validation inputs belong to this bounded decode call. Completed native buffers retain no references to
            // either description, so releasing them here prevents a reused decoder from observing stale item state.
            this.codecConfiguration = null;
            this.containerColorProfile = null;
            this.validatedSequenceHeader = null;
        }
    }

    /// <summary>
    /// Validates the active sequence against its container declarations before reconstruction begins.
    /// </summary>
    /// <param name="sequenceHeader">The active sequence header.</param>
    private void ValidateSequence(ObuSequenceHeader sequenceHeader)
    {
        if (ReferenceEquals(this.validatedSequenceHeader, sequenceHeader))
        {
            return;
        }

        this.codecConfiguration?.Validate(sequenceHeader);
        CicpProfile? colorProfile = this.containerColorProfile;

        if (colorProfile is not null)
        {
            ObuColorConfig colorConfig = sequenceHeader.ColorConfig;
            ObuColorPrimaries containerColorPrimaries = (ObuColorPrimaries)colorProfile.ColorPrimaries;
            ObuTransferCharacteristics containerTransferCharacteristics = (ObuTransferCharacteristics)colorProfile.TransferCharacteristics;
            ObuMatrixCoefficients containerMatrixCoefficients = (ObuMatrixCoefficients)colorProfile.MatrixCoefficients;

            // AV1-ISOBMFF permits nclx to supply only bitstream fields explicitly coded as unspecified. A different
            // specified value is a conformance error rather than a container-level color override.
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
        }

        // The same sequence header governs subsequent layered frames until another header OBU replaces it.
        this.validatedSequenceHeader = sequenceHeader;
    }

    /// <summary>
    /// Parses one entropy-coded tile payload into the current frame state.
    /// </summary>
    /// <param name="tileData">The entropy-coded tile payload.</param>
    /// <param name="tileNum">The raster-order tile index.</param>
    public void ReadTile(Span<byte> tileData, int tileNum)
    {
        FrameDecodeState frameDecodeState;
        if (this.frameDecodeState is null)
        {
            ObuSequenceHeader sequenceHeader = this.obuReader.CurrentSequenceHeader;
            ObuFrameHeader frameHeader = this.obuReader.CurrentFrameHeader;
            this.ValidateSequence(sequenceHeader);

            // Sequence dimensions are an upper bound, not an allocation request. Check the active upscaled frame
            // before renting its syntax state; a small frame can legally belong to a much larger sequence envelope.
            Av1FrameBuffer<byte>.ValidateDimensions(
                sequenceHeader,
                sequenceHeader.ColorConfig.GetColorFormat(),
                false,
                frameHeader.FrameSize.SuperResolutionUpscaledWidth,
                frameHeader.FrameSize.FrameHeight);

            if (!ReferenceEquals(this.entropySequenceHeader, sequenceHeader))
            {
                if (this.entropySequenceHeader is not null)
                {
                    // A coded-sequence boundary invalidates both sample references and their retained CDF snapshots.
                    // Returned snapshot graphs stay decoder-local and can be overwritten for the new sequence.
                    this.referenceFrames.Reset();
                    this.entropyContexts?.Reset();
                }

                this.entropySequenceHeader = sequenceHeader;
            }

            Av1FrameEntropyContext? primaryReferenceContext = null;
            byte? primaryReferenceSlot = frameHeader.PrimaryReferenceSlot;
            if (primaryReferenceSlot is not null)
            {
                // The uncompressed-header parser validates slot occupancy. Entropy ownership is checked here because
                // only the reconstructed frame owner knows whether that slot retained a completed CDF snapshot.
                Av1ReferenceFrame? primaryReference = this.referenceFrames.Resolve(primaryReferenceSlot.Value);
                if (primaryReference is null || primaryReference.EntropyContext is null)
                {
                    throw new InvalidImageContentException("The AV1 primary reference has no retained entropy context.");
                }

                primaryReferenceContext = primaryReference.EntropyContext;
            }

            // Every tile group in a frame contributes to the same mode-info and coefficient state.
            Av1FrameEntropyContexts entropyContexts =
                this.entropyContexts ??= new(frameHeader.QuantizationParameters.BaseQIndex);

            Av1TileReader? tileReader = null;
            Av1FrameBuffer<byte>? frameBuffer = null;

            // Presentation-only samples contain no new tile syntax, so they keep the most recently reconstructed
            // frame state. Release that state only when a new reconstruction begins to avoid overlapping two graphs.
            this.FrameInfo?.ReleaseOwner();
            this.FrameInfo = null;

            try
            {
                tileReader = new Av1TileReader(
                    this.configuration,
                    sequenceHeader,
                    frameHeader,
                    entropyContexts,
                    primaryReferenceContext,
                    this.referenceFrames,
                    this.lumaPaletteColorIndexMap,
                    this.chromaPaletteColorIndexMap);

                frameBuffer = new Av1FrameBuffer<byte>(
                    this.configuration,
                    sequenceHeader,
                    sequenceHeader.ColorConfig.GetColorFormat(),
                    false,
                    frameHeader.FrameSize.SuperResolutionUpscaledWidth,
                    frameHeader.FrameSize.FrameHeight)
                {
                    Width = frameHeader.FrameSize.FrameWidth,
                    Height = frameHeader.FrameSize.FrameHeight
                };

                // No preceding frame is active here. Release an undersized owner before renting its replacement;
                // a failed rent leaves the session empty and retryable, while completed frames reuse this storage.
                int workspaceLength = Av1BlockDecoder.GetWorkspaceLength(sequenceHeader);
                if (this.reconstructionWorkspace is null || this.reconstructionWorkspace.Memory.Length < workspaceLength)
                {
                    this.reconstructionWorkspace?.Dispose();
                    this.reconstructionWorkspace = null;
                    this.reconstructionWorkspace = this.configuration.MemoryAllocator.Allocate<short>(workspaceLength);
                }

                Av1FrameDecoder frameDecoder = new(
                    sequenceHeader,
                    frameHeader,
                    tileReader.FrameInfo,
                    frameBuffer,
                    this.referenceFrames,
                    this.reconstructionWorkspace.Memory[..workspaceLength],
                    new Av1TileReader.PaletteColorIndexMaps(
                        this.lumaPaletteColorIndexMap,
                        this.chromaPaletteColorIndexMap));

                tileReader.FrameDecoder = frameDecoder;
                frameDecodeState = new(tileReader, frameBuffer, frameDecoder);
                this.frameDecodeState = frameDecodeState;
            }
            catch
            {
                frameBuffer?.Dispose();
                tileReader?.Dispose();
                throw;
            }
        }
        else
        {
            frameDecodeState = this.frameDecodeState.Value;
        }

        frameDecodeState.TileReader.ReadTile(tileData, tileNum);
    }

    /// <summary>
    /// Reconstructs a frame after all of its tile payloads have been parsed.
    /// </summary>
    public void CompleteFrame()
    {
        ObuSequenceHeader sequenceHeader = this.obuReader.SequenceHeader
            ?? throw new InvalidImageContentException("An AV1 frame cannot complete before its sequence header.");

        ObuFrameHeader frameHeader = this.obuReader.FrameHeader
            ?? throw new InvalidImageContentException("An AV1 frame cannot complete before its frame header.");

        Av1FrameBuffer<byte>? frameBuffer = null;
        Av1FrameDecoder? frameDecoder = null;
        Av1TileReader? tileReader = null;
        Av1FrameBuffer<byte>? presentationBuffer = null;

        try
        {
            this.ValidateSequence(sequenceHeader);

            if (frameHeader.ShowExistingFrame)
            {
                Av1ReferenceFrame existingFrame = this.referenceFrames.ShowExisting((int)frameHeader.FrameToShowMapIdx);
                ObuFrameHeader existingFrameHeader = existingFrame.FrameHeader;

                if (existingFrameHeader.FrameType == ObuFrameType.KeyFrame)
                {
                    // Both the decoder working context and the context retained by the newly aliased key frame reset
                    // frame. Later primary-reference selection must therefore observe normative defaults.
                    existingFrame.ResetEntropyContext();
                    this.entropyContexts?.Reset();
                }

                if (existingFrameHeader.FilmGrainParameters.ApplyGrain)
                {
                    presentationBuffer = Av1FrameBuffer<byte>.CreatePresentation(this.configuration, sequenceHeader, existingFrame.FrameBuffer);

                    // Retained reference samples remain ungrained. Existing-frame presentation receives its own
                    // allocator-owned copy only when the inherited film-grain parameters actually modify the output.
                    existingFrame.FrameBuffer.CopyVisibleTo(presentationBuffer);
                    Av1FilmGrainDecoder filmGrainDecoder = new(sequenceHeader, existingFrameHeader, presentationBuffer);
                    filmGrainDecoder.DecodeFrame();

                    Av1ReferenceFrame presentationFrame = new(presentationBuffer, existingFrameHeader);
                    presentationBuffer = null;
                    this.referenceFrames.CommitOutput(presentationFrame);
                }

                this.SequenceHeader = sequenceHeader;
                this.FrameHeader = existingFrameHeader;
                return;
            }

            FrameDecodeState? activeFrameDecodeState = this.frameDecodeState;
            if (activeFrameDecodeState is null)
            {
                throw new InvalidImageContentException("The AV1 frame completed without tile syntax.");
            }

            this.frameDecodeState = null;
            FrameDecodeState activeFrame = activeFrameDecodeState.Value;
            frameBuffer = activeFrame.FrameBuffer;
            frameDecoder = activeFrame.FrameDecoder;
            tileReader = activeFrame.TileReader;

            Av1FrameInfo frameInfo = tileReader.FrameInfo;
            Av1FrameBuffer<byte> reconstructedFrameBuffer = frameBuffer;
            frameDecoder.CompleteFrame(this.cdefDecoder, this.restorationBoundary, this.restorationDecoder);

            bool retainsReference = (frameHeader.RefreshFrameFlags & byte.MaxValue) != 0;
            if (retainsReference)
            {
                // Motion compensation may address any clamped position inside the decoder border. Extending once after
                // all in-loop filters lets every later block use the full padded span without per-prediction edge copies.
                Av1ReferenceFrameBorder.Extend(reconstructedFrameBuffer);

                // Detach only the state libaom retains on RefCntBuffer before any later ownership transfer can fail.
                // The full reconstruction graph remains local to the current result and expires independently.
                frameInfo.PrepareReferenceState();
            }

            bool needsSeparatePresentation = frameHeader.ShowFrame && frameHeader.FilmGrainParameters.ApplyGrain && retainsReference;
            if (needsSeparatePresentation)
            {
                presentationBuffer = Av1FrameBuffer<byte>.CreatePresentation(this.configuration, sequenceHeader, reconstructedFrameBuffer);

                // Film grain must never contaminate a decoded reference. A shown frame that is also refreshed therefore
                // receives one allocator-owned presentation copy; frames with no reference role are grained in place.
                reconstructedFrameBuffer.CopyVisibleTo(presentationBuffer);
            }

            Av1FrameBuffer<byte> grainTarget = presentationBuffer ?? reconstructedFrameBuffer;
            if (frameHeader.ShowFrame && frameHeader.FilmGrainParameters.ApplyGrain)
            {
                Av1FilmGrainDecoder filmGrainDecoder = new(sequenceHeader, frameHeader, grainTarget);
                filmGrainDecoder.DecodeFrame();
            }

            Av1ReferenceFrame referenceFrame;
            if (retainsReference)
            {
                Av1FrameEntropyContexts entropyContexts = tileReader.EntropyContexts;

                Av1FrameEntropyContext entropySnapshot = entropyContexts.RentPublishedSnapshot();
                try
                {
                    referenceFrame = new(reconstructedFrameBuffer, frameHeader, frameInfo, entropySnapshot, entropyContexts);
                }
                catch
                {
                    // The snapshot rent precedes the reference owner. Return it if object construction cannot accept it.
                    entropyContexts.ReturnSnapshot(entropySnapshot);
                    throw;
                }
            }
            else
            {
                // Presentation-only frames can never become primary references, so they own no unused CDF graph.
                referenceFrame = new(reconstructedFrameBuffer, frameHeader);
            }

            frameBuffer = null;

            if (!this.referenceFrames.Commit(frameHeader.RefreshFrameFlags, referenceFrame, frameHeader.ShowFrame && !needsSeparatePresentation))
            {
                referenceFrame.Dispose();
            }

            if (presentationBuffer is not null)
            {
                Av1ReferenceFrame presentationFrame = new(presentationBuffer, frameHeader);
                presentationBuffer = null;
                this.referenceFrames.CommitOutput(presentationFrame);
            }

            this.SequenceHeader = sequenceHeader;
            this.FrameHeader = frameHeader;
            this.DecodedInterPredictionFeatures |= frameInfo.InterPredictionFeatures;

            // Hidden frames can contain the inter syntax needed to validate a sequence. Retain only the latest full
            // reconstruction state until the next bounded decode; reference-map entries keep their compact state.
            frameInfo.AddOwner();
            this.FrameInfo?.ReleaseOwner();
            this.FrameInfo = frameInfo;
        }
        finally
        {
            // A non-shown frame or failed reconstruction never escapes this callback. The tile reader releases the
            // reconstruction lease; a retained frame keeps only its compact reference state after neighbor contexts
            // and the remaining frame-sized syntax are returned.
            presentationBuffer?.Dispose();
            frameBuffer?.Dispose();
            tileReader?.Dispose();
            this.frameDecodeState?.Dispose();
            this.frameDecodeState = null;
        }
    }

    /// <summary>
    /// Releases the current tile parser, reference map, and retained presentation output.
    /// </summary>
    public void Dispose()
    {
        this.frameDecodeState?.Dispose();
        this.frameDecodeState = null;
        this.referenceFrames.Dispose();
        this.reconstructionWorkspace?.Dispose();
        this.reconstructionWorkspace = null;
        this.cdefDecoder.Dispose();
        this.restorationBoundary.Dispose();
        this.restorationDecoder.Dispose();
        this.FrameInfo?.ReleaseOwner();
        this.FrameInfo = null;
        this.lumaPaletteColorIndexMap.Dispose();
        this.chromaPaletteColorIndexMap.Dispose();
        this.paletteColorIndexMapOwner.Dispose();
    }

    /// <summary>
    /// Carries the active frame resources as one valid state so no partially initialized combination can be observed.
    /// </summary>
    private readonly struct FrameDecodeState(
        Av1TileReader tileReader,
        Av1FrameBuffer<byte> frameBuffer,
        Av1FrameDecoder frameDecoder) : IDisposable
    {
        /// <summary>
        /// Gets the tile parser shared by all tile groups in the frame.
        /// </summary>
        public Av1TileReader TileReader { get; } = tileReader;

        /// <summary>
        /// Gets the destination sample buffer reconstructed by the frame pipeline.
        /// </summary>
        public Av1FrameBuffer<byte> FrameBuffer { get; } = frameBuffer;

        /// <summary>
        /// Gets the reconstruction pipeline for the frame.
        /// </summary>
        public Av1FrameDecoder FrameDecoder { get; } = frameDecoder;

        /// <summary>
        /// Releases every resource when ownership has not transferred to a completed frame.
        /// </summary>
        public void Dispose()
        {
            this.FrameBuffer.Dispose();
            this.TileReader.Dispose();
        }
    }
}
