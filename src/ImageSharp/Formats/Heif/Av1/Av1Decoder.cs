// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
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
    /// The configuration used for decoded image and working buffer allocation.
    /// </summary>
    private readonly Configuration configuration;

    /// <summary>
    /// The segment integrity mode. It controls if damaged or truncated tile data throws.
    /// </summary>
    private readonly SegmentIntegrityHandling integrityHandling;

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
    private readonly Av1PlaneRegion<byte> lumaPaletteColorIndexMap;

    /// <summary>
    /// Reusable chroma palette indices for the coding blocks in one superblock.
    /// </summary>
    private readonly Av1PlaneRegion<byte> chromaPaletteColorIndexMap;

    /// <summary>
    /// The shared backing owner for both reusable palette maps.
    /// </summary>
    private readonly IMemoryOwner<byte> paletteColorIndexMapOwner;

    /// <summary>
    /// The reconstructed references and selected presentation output owned by the current bounded decode session.
    /// </summary>
    private readonly Av1ReferenceFrameStore referenceFrames = new();

    /// <summary>
    /// The frame-base, tile-working, and published entropy contexts. The decoder creates them for the first coded frame and reuses them after that.
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
    /// Retains the reconstruction work buffer across frames. The active frame borrows its memory until it completes.
    /// </summary>
    private IMemoryOwner<short>? reconstructionWorkspace;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1Decoder"/> class.
    /// </summary>
    /// <param name="configuration">The configuration used for image and working buffer allocation.</param>
    public Av1Decoder(Configuration configuration)
        : this(configuration, 0, SegmentIntegrityHandling.Strict, false)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1Decoder"/> class for one selected AV1 operating point.
    /// </summary>
    /// <param name="configuration">The configuration used for image and working buffer allocation.</param>
    /// <param name="operatingPointIndex">The zero-based sequence-header operating-point index to decode.</param>
    /// <param name="integrityHandling">
    /// The segment integrity mode. In the <see cref="SegmentIntegrityHandling.IgnoreImageData"/> mode, a damaged or truncated payload keeps every superblock
    /// that decodes before the damage, and the frame completes. The other modes throw.
    /// </param>
    /// <param name="imageExists">
    /// A value indicating whether the caller keeps an image without the payloads of this decoder. Examples are the canvas of a grid, an image sequence, and the
    /// color image of an alpha plane. If <see langword="true"/>, a payload with damage before its decoded image data gives no frame in the
    /// <see cref="SegmentIntegrityHandling.IgnoreImageData"/> mode. If <see langword="false"/>, that damage throws in each mode.
    /// </param>
    public Av1Decoder(Configuration configuration, byte operatingPointIndex, SegmentIntegrityHandling integrityHandling, bool imageExists)
    {
        this.configuration = configuration;
        this.integrityHandling = integrityHandling;
        this.obuReader = new(operatingPointIndex, this.referenceFrames) { IntegrityHandling = integrityHandling, ImageExists = imageExists };
        this.cdefDecoder = new(configuration.MemoryAllocator);
        this.restorationBoundary = new(configuration.MemoryAllocator);
        this.restorationDecoder = new(configuration.MemoryAllocator);

        // Sequential tile decoding needs only the palette indices of the current superblock. One fixed owner holds both maps at the maximum superblock size.
        // Thus the maps stay reusable for the session without fragmented group rents.
        int paletteMapLength = 1 << Av1Constants.MaxSuperBlockSizeLog2;
        int paletteMapArea = paletteMapLength * paletteMapLength;
        this.paletteColorIndexMapOwner = configuration.MemoryAllocator.Allocate<byte>(2 * paletteMapArea);
        Memory<byte> paletteMaps = this.paletteColorIndexMapOwner.Memory;
        Rectangle paletteMapBounds = new(0, 0, paletteMapLength, paletteMapLength);
        this.lumaPaletteColorIndexMap = new(paletteMaps[..paletteMapArea], paletteMapLength, paletteMapBounds);
        this.chromaPaletteColorIndexMap = new(paletteMaps.Slice(paletteMapArea, paletteMapArea), paletteMapLength, paletteMapBounds);
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
    /// Gets tile and superblock state for the most recently reconstructed frame. The value is <see langword="null"/> when no frame was reconstructed, or when
    /// the output selected an existing reference without new tile syntax.
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
    /// Returns the native planes of the frame a reference slot holds, or <see langword="null"/> for an empty slot.
    /// </summary>
    /// <param name="slot">The reference slot, 0 to 7.</param>
    /// <returns>The frame planes.</returns>
    internal Av1FrameBuffer<byte>? GetReferenceFrameBuffer(int slot) => this.referenceFrames.Resolve(slot)?.FrameBuffer;

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
    /// <param name="effectiveColorProfile">Receives the effective CICP description of the decoded sample.</param>
    /// <returns>
    /// <see langword="true"/> if the sample decodes into the destination. <see langword="false"/> if the decoder reported damage, the mode ignores it, and the
    /// sample gives no frame. The destination then keeps its pixels.
    /// </returns>
    public bool DecodeSequenceFrame<TPixel>(
        Span<byte> buffer,
        CicpProfile? containerColorProfile,
        Av1CodecConfiguration? codecConfiguration,
        Size expectedCodedSize,
        Rectangle sourceRectangle,
        Buffer2DRegion<TPixel> destination,
        HeifPixelTransform transform,
        IccProfile? profile,
        Av1FrameBuffer<byte>? alphaFrame,
        bool premultiplied,
        [NotNullWhen(true)] out CicpProfile? effectiveColorProfile)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (!this.DecodePayload(buffer, containerColorProfile, codecConfiguration, null, true, out effectiveColorProfile))
        {
            return false;
        }

        Av1ReferenceFrame outputFrame = this.referenceFrames.ResolveOutput();
        Size codedSize = new(
            outputFrame.FrameHeader.FrameSize.SuperResolutionUpscaledWidth,
            outputFrame.FrameHeader.FrameSize.FrameHeight);

        if (codedSize != expectedCodedSize)
        {
            this.integrityHandling.ThrowOrIgnoreImageDataError(
                this.obuReader.ImageExists,
                "The decoded image-sequence sample dimensions do not match its visual sample entry.");

            return false;
        }

        // The conversion uses the presentation range of the container when it has one. It does not change the reconstructed reference state.
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

        return true;
    }

    /// <summary>
    /// Decodes the next visible monochrome AV1 sequence sample into native samples.
    /// </summary>
    /// <param name="buffer">The complete AV1 sample payload.</param>
    /// <param name="containerColorProfile">The container color description.</param>
    /// <param name="codecConfiguration">The AV1 sample-entry configuration.</param>
    /// <param name="expectedCodedSize">The required coded dimensions.</param>
    /// <returns>
    /// The auxiliary frame. The decoder keeps this frame until it decodes the next sample, or until the caller disposes the decoder. The value is
    /// <see langword="null"/> if the decoder reported damage, the mode ignores it, and the sample gives no frame.
    /// </returns>
    public Av1FrameBuffer<byte>? DecodeSequenceAlpha(
        Span<byte> buffer,
        CicpProfile? containerColorProfile,
        Av1CodecConfiguration? codecConfiguration,
        Size expectedCodedSize)
    {
        if (!this.DecodePayload(buffer, containerColorProfile, codecConfiguration, null, true, out _))
        {
            return null;
        }

        Av1ReferenceFrame outputFrame = this.referenceFrames.ResolveOutput();
        Av1FrameBuffer<byte> frame = outputFrame.FrameBuffer;
        if (frame.Width != expectedCodedSize.Width || frame.Height != expectedCodedSize.Height)
        {
            this.integrityHandling.ThrowOrIgnoreImageDataError(
                this.obuReader.ImageExists,
                "The decoded alpha sample dimensions do not match its visual sample entry.");

            return null;
        }

        if (frame.ColorFormat != Av1ColorFormat.Yuv400)
        {
            this.integrityHandling.ThrowOrIgnoreImageDataError(this.obuReader.ImageExists, "An AV1 alpha sample must be monochrome.");
            return null;
        }

        return frame;
    }

    /// <summary>
    /// Parses every coded frame in an AV1 payload and returns the final shown frame's native component planes.
    /// </summary>
    /// <param name="buffer">The complete AV1 elementary-stream payload.</param>
    /// <param name="containerColorProfile">The container color description that supplies unspecified sequence-header color information.</param>
    /// <param name="codecConfiguration">The AV1 codec configuration validated against the coded sequence header.</param>
    /// <param name="effectiveColorProfile">
    /// Receives the effective CICP description of the native planes, or <see langword="null"/> when the payload gives no frame.
    /// </param>
    /// <param name="layeredImageIndex">The byte boundaries of a layered AV1 image item, or <see langword="null"/>.</param>
    /// <returns>
    /// The reconstructed native frame buffer. Ownership transfers to the caller. The value is <see langword="null"/> if the decoder reported damage, the mode
    /// ignores it, and the payload gives no frame.
    /// </returns>
    public Av1FrameBuffer<byte>? DecodeFrameBuffer(
        Span<byte> buffer,
        CicpProfile? containerColorProfile,
        Av1CodecConfiguration? codecConfiguration,
        out CicpProfile? effectiveColorProfile,
        Av1LayeredImageIndex? layeredImageIndex)
    {
        if (!this.DecodePayload(buffer, containerColorProfile, codecConfiguration, layeredImageIndex, true, out effectiveColorProfile))
        {
            return null;
        }

        using Av1ReferenceFrame outputFrame = this.referenceFrames.TakeOutput();
        return outputFrame.TakeFrameBuffer();
    }

    /// <summary>
    /// Parses one bounded payload into the retained decoder session.
    /// </summary>
    /// <param name="buffer">The complete AV1 payload.</param>
    /// <param name="containerColorProfile">The container color description.</param>
    /// <param name="codecConfiguration">The AV1 codec configuration.</param>
    /// <param name="layeredImageIndex">The optional layer byte boundaries.</param>
    /// <param name="requireShownFrame">
    /// Whether the payload must produce a shown frame. The value is <see langword="false"/> for a non-presented image-sequence sample. The decoder then keeps
    /// only its reference state.
    /// </param>
    /// <param name="effectiveColorProfile">Receives the effective CICP description.</param>
    /// <returns>
    /// <see langword="true"/> if the payload decodes. <see langword="false"/> if the decoder reported damage, the mode ignores it, and the payload gives no
    /// frame.
    /// </returns>
    public bool DecodePayload(
        Span<byte> buffer,
        CicpProfile? containerColorProfile,
        Av1CodecConfiguration? codecConfiguration,
        Av1LayeredImageIndex? layeredImageIndex,
        bool requireShownFrame,
        [NotNullWhen(true)] out CicpProfile? effectiveColorProfile)
    {
        effectiveColorProfile = null;
        this.codecConfiguration = codecConfiguration;
        this.containerColorProfile = containerColorProfile;
        this.validatedSequenceHeader = null;
        this.SequenceHeader = null;
        this.FrameHeader = null;
        this.DecodedInterPredictionFeatures = Av1InterPredictionFeatures.None;

        // Full tile syntax describes only the frames that this payload reconstructs. Reference slots already own the compact state that later frames need. Thus
        // the decoder releases the reconstruction graph of the previous payload before it parses this payload.
        this.FrameInfo?.ReleaseOwner();
        this.FrameInfo = null;

        // Each payload presents its own shown frame. A payload can end before its shown frame, for example when
        // truncated data holds only a hidden frame. Such a payload must not present the output of the earlier payload.
        this.referenceFrames.ReleaseOutput();

        try
        {
            bool decoded;
            if (layeredImageIndex is null)
            {
                Av1BitStreamReader reader = new(buffer);
                decoded = this.obuReader.ReadAll(ref reader, buffer.Length, this);
            }
            else
            {
                decoded = true;
                int layerOffset = 0;
                for (int layer = 0; decoded && layer < Av1Constants.MaxSpatialLayerCount - 1 && layerOffset < buffer.Length; layer++)
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

                    // A truncated item holds fewer bytes than its layer sizes declare. The item data reader already
                    // reported the truncation, so the layer that crosses the end keeps the bytes that remain.
                    int layerSize = Math.Min((int)declaredLayerSize, buffer.Length - layerOffset);
                    Av1BitStreamReader layerReader = new(buffer.Slice(layerOffset, layerSize));
                    decoded = this.obuReader.ReadAll(ref layerReader, layerSize, this);
                    layerOffset += layerSize;
                }

                if (decoded && layerOffset < buffer.Length)
                {
                    Span<byte> finalLayer = buffer[layerOffset..];
                    Av1BitStreamReader finalLayerReader = new(finalLayer);
                    decoded = this.obuReader.ReadAll(ref finalLayerReader, finalLayer.Length, this);
                }
            }

            if (!decoded)
            {
                // The OBU reader reported the damage and reset its session. The mode ignores the damage, and the payload gives no frame. The decoder cannot use
                // the state of the stopped payload again. This is the same as after a failed payload.
                this.ResetSession();
                return false;
            }

            // The checks that follow find a payload that gives no presentable frame. The mode can ignore the error only when the caller keeps an image without
            // the payload.
            ObuSequenceHeader? sequenceHeader = this.obuReader.SequenceHeader;
            if (sequenceHeader is null)
            {
                this.integrityHandling.ThrowOrIgnoreImageDataError(this.obuReader.ImageExists, "The AV1 payload contains no sequence header.");
                return false;
            }

            if (requireShownFrame && this.referenceFrames.OutputFrame is null)
            {
                // The payload decoded its frames, so the reference state stays valid for later samples.
                this.integrityHandling.ThrowOrIgnoreImageDataError(this.obuReader.ImageExists, "The AV1 payload did not produce a shown frame.");
                return false;
            }

            // The returned profile is the effective CICP description that conversion uses. It includes container values that legally replaced unspecified
            // bitstream fields. It also gives bitstream-only color metadata to callers.
            ObuColorConfig effectiveColorConfig = sequenceHeader.ColorConfig;
            effectiveColorProfile = new CicpProfile(
                (byte)effectiveColorConfig.ColorPrimaries,
                (byte)effectiveColorConfig.TransferCharacteristics,
                (byte)effectiveColorConfig.MatrixCoefficients,
                effectiveColorConfig.ColorRange);

            return true;
        }
        catch
        {
            this.ResetSession();
            throw;
        }
        finally
        {
            // Validation inputs belong to this bounded decode call. Completed native buffers keep no reference to either description. Thus the release here
            // prevents a reused decoder from seeing stale item state.
            this.codecConfiguration = null;
            this.containerColorProfile = null;
            this.validatedSequenceHeader = null;
        }
    }

    /// <summary>
    /// Releases the frame state and resets the decoder session after a payload stops at damage.
    /// </summary>
    /// <remarks>
    /// A failed frame can own pooled neighbor contexts. Earlier layers own reconstructed references and published CDF snapshots. After a failed
    /// non-transactional frame transition, the decoder cannot use any of these items again.
    /// </remarks>
    private void ResetSession()
    {
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
    }

    /// <summary>
    /// Validates the active sequence against its container declarations before reconstruction begins.
    /// </summary>
    /// <remarks>
    /// The method returns the damage and does not report it. The OBU reader reports the damage one time, through the damage route of its payload.
    /// </remarks>
    /// <param name="sequenceHeader">The active sequence header.</param>
    /// <returns>The message of the mismatch, or <see langword="null"/> when the sequence matches its container.</returns>
    private string? ValidateSequence(ObuSequenceHeader sequenceHeader)
    {
        if (ReferenceEquals(this.validatedSequenceHeader, sequenceHeader))
        {
            return null;
        }

        string? damage = this.codecConfiguration?.Validate(sequenceHeader);
        if (damage is not null)
        {
            return damage;
        }

        CicpProfile? colorProfile = this.containerColorProfile;

        if (colorProfile is not null)
        {
            ObuColorConfig colorConfig = sequenceHeader.ColorConfig;
            ObuColorPrimaries containerColorPrimaries = (ObuColorPrimaries)colorProfile.ColorPrimaries;
            ObuTransferCharacteristics containerTransferCharacteristics = (ObuTransferCharacteristics)colorProfile.TransferCharacteristics;
            ObuMatrixCoefficients containerMatrixCoefficients = (ObuMatrixCoefficients)colorProfile.MatrixCoefficients;

            // AV1-ISOBMFF permits nclx to supply only bitstream fields explicitly coded as unspecified. A different specified value is a conformance error, not
            // a container-level color override.
            if (colorConfig.ColorPrimaries == ObuColorPrimaries.Unspecified)
            {
                colorConfig.ColorPrimaries = containerColorPrimaries;
            }
            else if (colorConfig.ColorPrimaries != containerColorPrimaries)
            {
                return "The HEIF CICP color primaries do not match the AV1 sequence header.";
            }

            if (colorConfig.TransferCharacteristics == ObuTransferCharacteristics.Unspecified)
            {
                colorConfig.TransferCharacteristics = containerTransferCharacteristics;
            }
            else if (colorConfig.TransferCharacteristics != containerTransferCharacteristics)
            {
                return "The HEIF CICP transfer characteristics do not match the AV1 sequence header.";
            }

            if (colorConfig.MatrixCoefficients == ObuMatrixCoefficients.Unspecified)
            {
                colorConfig.MatrixCoefficients = containerMatrixCoefficients;
            }
            else if (colorConfig.MatrixCoefficients != containerMatrixCoefficients)
            {
                return "The HEIF CICP matrix coefficients do not match the AV1 sequence header.";
            }
        }

        // The same sequence header governs subsequent layered frames until another header OBU replaces it.
        this.validatedSequenceHeader = sequenceHeader;
        return null;
    }

    /// <summary>
    /// Parses one entropy-coded tile payload into the current frame state.
    /// </summary>
    /// <param name="tileData">The entropy-coded tile payload.</param>
    /// <param name="tileNum">The raster-order tile index.</param>
    /// <returns>
    /// The message of a sequence that does not match its container, or <see langword="null"/>. The decoder checks the sequence before the first tile of a
    /// frame. As a result, the frame holds no decoded tile when the method returns a message.
    /// </returns>
    public string? ReadTile(Span<byte> tileData, int tileNum)
    {
        FrameDecodeState frameDecodeState;
        if (this.frameDecodeState is null)
        {
            ObuSequenceHeader sequenceHeader = this.obuReader.CurrentSequenceHeader;
            ObuFrameHeader frameHeader = this.obuReader.CurrentFrameHeader;
            string? damage = this.ValidateSequence(sequenceHeader);
            if (damage is not null)
            {
                return damage;
            }

            // The sequence dimensions are an upper limit, not an allocation request. The OBU reader compared the active upscaled frame with the contiguous
            // frame limit when it checked the frame header. As a result, the frame state that this method rents fits. A small frame can legally be part of a
            // much larger sequence envelope.
            if (!ReferenceEquals(this.entropySequenceHeader, sequenceHeader))
            {
                if (this.entropySequenceHeader is not null)
                {
                    // A coded-sequence boundary invalidates both sample references and their retained CDF snapshots. Returned snapshot graphs stay
                    // decoder-local and can be overwritten for the new sequence.
                    this.referenceFrames.Reset();
                    this.entropyContexts?.Reset();
                }

                this.entropySequenceHeader = sequenceHeader;
            }

            Av1FrameEntropyContext? primaryReferenceContext = null;
            byte? primaryReferenceSlot = frameHeader.PrimaryReferenceSlot;
            if (primaryReferenceSlot is not null)
            {
                // The uncompressed-header parser checks the slot occupancy. A slot holds only a frame that CompleteFrame committed with a refresh role. Each
                // such frame keeps a completed CDF snapshot. Presentation-only frames never go into a slot. As a result, file data cannot select a primary
                // reference without an entropy context.
                Av1ReferenceFrame primaryReference = this.referenceFrames.ResolveRequired(primaryReferenceSlot.Value);
                primaryReferenceContext = primaryReference.EntropyContext;
                DebugGuard.NotNull(primaryReferenceContext);
            }

            // Every tile group in a frame contributes to the same mode-info and coefficient state.
            Av1FrameEntropyContexts entropyContexts =
                this.entropyContexts ??= new(frameHeader.QuantizationParameters.BaseQIndex);

            Av1TileReader? tileReader = null;
            Av1FrameBuffer<byte>? frameBuffer = null;

            // Presentation-only samples contain no new tile syntax, so they keep the most recently reconstructed frame state. The decoder releases that state
            // only when a new reconstruction begins. Thus two reconstruction graphs never overlap.
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

                // No preceding frame is active here. The code releases an undersized owner before it rents the replacement. Thus a failed rent leaves the
                // session empty and retryable. Completed frames reuse this storage.
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
                tileReader.IntegrityHandling = this.integrityHandling;
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
        return null;
    }

    /// <summary>
    /// Completes the current frame after the parser reads all of its tile payloads. The method presents a shown existing frame, or reconstructs a new frame
    /// from its tiles.
    /// </summary>
    /// <returns>
    /// The message of a sequence that does not match its container, or <see langword="null"/>. The method then releases the frame without a presentation.
    /// </returns>
    public string? CompleteFrame()
    {
        ObuSequenceHeader sequenceHeader = this.obuReader.CurrentSequenceHeader;
        ObuFrameHeader frameHeader = this.obuReader.CurrentFrameHeader;

        Av1FrameBuffer<byte>? frameBuffer = null;
        Av1FrameDecoder? frameDecoder = null;
        Av1TileReader? tileReader = null;
        Av1FrameBuffer<byte>? presentationBuffer = null;

        try
        {
            string? damage = this.ValidateSequence(sequenceHeader);
            if (damage is not null)
            {
                return damage;
            }

            if (frameHeader.ShowExistingFrame)
            {
                Av1ReferenceFrame existingFrame = this.referenceFrames.ShowExisting((int)frameHeader.FrameToShowMapIdx);
                ObuFrameHeader existingFrameHeader = existingFrame.FrameHeader;

                if (existingFrameHeader.FrameType == ObuFrameType.KeyFrame)
                {
                    // Showing an existing key frame resets both the working context of the decoder and the context that the key frame keeps. Thus a later
                    // primary-reference selection sees the default entropy contexts.
                    existingFrame.ResetEntropyContext();
                    this.entropyContexts?.Reset();
                }

                if (existingFrameHeader.FilmGrainParameters.ApplyGrain)
                {
                    presentationBuffer = Av1FrameBuffer<byte>.CreatePresentation(this.configuration, sequenceHeader, existingFrame.FrameBuffer);

                    // Retained reference samples remain ungrained. Existing-frame presentation receives its own allocator-owned copy only when the inherited
                    // film-grain parameters change the output.
                    existingFrame.FrameBuffer.CopyVisibleTo(presentationBuffer);
                    Av1FilmGrainDecoder filmGrainDecoder = new(sequenceHeader, existingFrameHeader, presentationBuffer);
                    filmGrainDecoder.DecodeFrame();

                    Av1ReferenceFrame presentationFrame = new(presentationBuffer, existingFrameHeader);
                    presentationBuffer = null;
                    this.referenceFrames.CommitOutput(presentationFrame);
                }

                this.SequenceHeader = sequenceHeader;
                this.FrameHeader = existingFrameHeader;
                return null;
            }

            // The OBU reader completes a frame without an existing-frame display only after it gave each tile of the frame to ReadTile. A frame has at least
            // one tile. The first ReadTile call creates the frame state.
            DebugGuard.IsTrue(this.frameDecodeState.HasValue, nameof(this.frameDecodeState), "A completed frame has tile syntax.");
            FrameDecodeState activeFrame = this.frameDecodeState.GetValueOrDefault();
            this.frameDecodeState = null;
            frameBuffer = activeFrame.FrameBuffer;
            frameDecoder = activeFrame.FrameDecoder;
            tileReader = activeFrame.TileReader;

            Av1FrameInfo frameInfo = tileReader.FrameInfo;
            Av1FrameBuffer<byte> reconstructedFrameBuffer = frameBuffer;
            frameDecoder.CompleteFrame(this.cdefDecoder, this.restorationBoundary, this.restorationDecoder);

            bool retainsReference = (frameHeader.RefreshFrameFlags & byte.MaxValue) != 0;
            if (retainsReference)
            {
                // Motion compensation can address any clamped position inside the decoder border. One extension after all in-loop filters lets every later
                // block use the full padded span, without edge copies for each prediction.
                Av1ReferenceFrameBorder.Extend(reconstructedFrameBuffer);

                // The code detaches only the state that a reference frame keeps, before any later ownership transfer can fail. The full reconstruction graph
                // stays local to the current result and expires separately.
                frameInfo.PrepareReferenceState();
            }

            bool needsSeparatePresentation = frameHeader.ShowFrame && frameHeader.FilmGrainParameters.ApplyGrain && retainsReference;
            if (needsSeparatePresentation)
            {
                presentationBuffer = Av1FrameBuffer<byte>.CreatePresentation(this.configuration, sequenceHeader, reconstructedFrameBuffer);

                // Film grain must never change a decoded reference. Thus a shown frame that is also refreshed gets one allocator-owned presentation copy.
                // Frames with no reference role get the grain in place.
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
                    // The snapshot rent comes before the reference owner exists. If the construction fails, the code returns the snapshot.
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

            // Hidden frames can contain the inter syntax that is necessary to validate a sequence. Only the latest full reconstruction state stays until the
            // next bounded decode. Reference-map entries keep their compact state.
            frameInfo.AddOwner();
            this.FrameInfo?.ReleaseOwner();
            this.FrameInfo = frameInfo;
            return null;
        }
        finally
        {
            // A non-shown frame or a failed reconstruction never leaves this callback. The tile reader releases the reconstruction lease. After the neighbor
            // contexts and the remaining frame-sized syntax are returned, a retained frame keeps only its compact reference state.
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
        this.paletteColorIndexMapOwner.Dispose();
    }

    /// <summary>
    /// Carries the active frame resources as one valid state so no partially initialized combination can be observed.
    /// </summary>
    private readonly struct FrameDecodeState : IDisposable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FrameDecodeState"/> struct.
        /// </summary>
        /// <param name="tileReader">The tile parser shared by all tile groups in the frame.</param>
        /// <param name="frameBuffer">The destination sample buffer reconstructed by the frame pipeline.</param>
        /// <param name="frameDecoder">The reconstruction pipeline for the frame.</param>
        public FrameDecodeState(
            Av1TileReader tileReader,
            Av1FrameBuffer<byte> frameBuffer,
            Av1FrameDecoder frameDecoder)
        {
            this.TileReader = tileReader;
            this.FrameBuffer = frameBuffer;
            this.FrameDecoder = frameDecoder;
        }

        /// <summary>
        /// Gets the tile parser shared by all tile groups in the frame.
        /// </summary>
        public Av1TileReader TileReader { get; }

        /// <summary>
        /// Gets the destination sample buffer reconstructed by the frame pipeline.
        /// </summary>
        public Av1FrameBuffer<byte> FrameBuffer { get; }

        /// <summary>
        /// Gets the reconstruction pipeline for the frame.
        /// </summary>
        public Av1FrameDecoder FrameDecoder { get; }

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
