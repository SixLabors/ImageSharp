// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Color;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.FilmGrain;
using SixLabors.ImageSharp.Formats.Heif.Av1.ReferenceFrames;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.PixelFormats.Utils;

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

/// <summary>
/// Decodes one bounded AV1 image payload into an ImageSharp image.
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
        this.obuReader = new(operatingPointIndex, this.referenceFrames);
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
    /// Gets the tile and superblock state for the final retained shown frame, or <see langword="null"/> before one completes.
    /// </summary>
    public Av1FrameInfo? FrameInfo { get; private set; }

    /// <summary>
    /// Decodes a bounded AV1 image payload and presents its final shown frame.
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
    /// Parses every coded frame in an AV1 payload and returns the final shown frame's native component planes.
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
        this.codecConfiguration = codecConfiguration;
        this.containerColorProfile = containerColorProfile;
        this.validatedSequenceHeader = null;
        this.SequenceHeader = null;
        this.FrameHeader = null;
        this.FrameInfo = null;
        Av1BitStreamReader reader = new(buffer);

        try
        {
            this.obuReader.ReadAll(ref reader, buffer.Length, () => this, false);

            Guard.NotNull(this.referenceFrames.OutputFrame, nameof(this.referenceFrames.OutputFrame));
            Guard.NotNull(this.SequenceHeader, nameof(this.SequenceHeader));
            Guard.NotNull(this.FrameHeader, nameof(this.FrameHeader));

            // Preserve the effective CICP description used for conversion, including container values that legally
            // supplied unspecified bitstream fields. This also exposes bitstream-only color metadata to callers.
            ObuColorConfig effectiveColorConfig = this.SequenceHeader.ColorConfig;
            effectiveColorProfile = new CicpProfile(
                (byte)effectiveColorConfig.ColorPrimaries,
                (byte)effectiveColorConfig.TransferCharacteristics,
                (byte)effectiveColorConfig.MatrixCoefficients,
                effectiveColorConfig.ColorRange);

            using Av1ReferenceFrame outputFrame = this.referenceFrames.TakeOutput();
            return outputFrame.TakeFrameBuffer();
        }
        catch
        {
            // A failed frame may own pooled neighbor contexts while earlier layers own reconstructed references and
            // published CDF snapshots. None can be reused after a non-transactional frame transition has failed.
            this.tileReader?.Dispose();
            this.tileReader = null;
            this.obuReader.Reset();
            this.entropyContexts?.Reset();
            this.entropySequenceHeader = null;
            this.SequenceHeader = null;
            this.FrameHeader = null;
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
    /// Parses one entropy-coded tile payload into the current frame state.
    /// </summary>
    /// <param name="tileData">The entropy-coded tile payload.</param>
    /// <param name="tileNum">The raster-order tile index.</param>
    public void ReadTile(Span<byte> tileData, int tileNum)
    {
        if (this.tileReader is null)
        {
            ObuSequenceHeader? sequenceHeader = this.obuReader.SequenceHeader;
            ObuFrameHeader? frameHeader = this.obuReader.FrameHeader;
            Guard.NotNull(sequenceHeader, nameof(sequenceHeader));
            Guard.NotNull(frameHeader, nameof(frameHeader));

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

            this.tileReader = new Av1TileReader(
                this.configuration,
                sequenceHeader,
                frameHeader,
                entropyContexts,
                primaryReferenceContext,
                this.referenceFrames);
        }

        this.tileReader.ReadTile(tileData, tileNum);
    }

    /// <summary>
    /// Reconstructs a frame after all of its tile payloads have been parsed.
    /// </summary>
    public void CompleteFrame()
    {
        Av1TileReader tileReader = this.tileReader!;
        ObuSequenceHeader sequenceHeader = this.obuReader.SequenceHeader!;
        ObuFrameHeader frameHeader = this.obuReader.FrameHeader!;
        Av1FrameBuffer<byte>? frameBuffer = null;
        Av1FrameBuffer<byte>? presentationBuffer = null;

        try
        {
            if (!ReferenceEquals(this.validatedSequenceHeader, sequenceHeader))
            {
                this.codecConfiguration?.Validate(sequenceHeader);
                CicpProfile? colorProfile = this.containerColorProfile;

                if (colorProfile is not null)
                {
                    ObuColorConfig colorConfig = sequenceHeader.ColorConfig;
                    ObuColorPrimaries containerColorPrimaries = (ObuColorPrimaries)colorProfile.ColorPrimaries;
                    ObuTransferCharacteristics containerTransferCharacteristics =
                        (ObuTransferCharacteristics)colorProfile.TransferCharacteristics;

                    ObuMatrixCoefficients containerMatrixCoefficients =
                        (ObuMatrixCoefficients)colorProfile.MatrixCoefficients;

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

                    if (colorConfig.ColorRange != colorProfile.FullRange)
                    {
                        throw new InvalidImageContentException("The HEIF CICP color range does not match the AV1 sequence header.");
                    }
                }

                // The same sequence header governs subsequent layered frames until another header OBU replaces it.
                // Retaining the validated reference prevents repeated item/color checks in the common layered case.
                this.validatedSequenceHeader = sequenceHeader;
            }

            Av1FrameInfo frameInfo = tileReader.FrameInfo;
            frameBuffer = new Av1FrameBuffer<byte>(
                this.configuration,
                sequenceHeader,
                sequenceHeader.ColorConfig.GetColorFormat(),
                false);

            using Av1FrameDecoder frameDecoder = new(sequenceHeader, frameHeader, frameInfo, frameBuffer);
            frameDecoder.DecodeFrame();

            bool retainsReference = (frameHeader.RefreshFrameFlags & byte.MaxValue) != 0;
            if (retainsReference)
            {
                // Motion compensation may address any clamped position inside the decoder border. Extending once after
                // all in-loop filters lets every later block use the full padded span without per-prediction edge copies.
                Av1ReferenceFrameBorder.Extend(frameBuffer);
            }

            bool needsSeparatePresentation = frameHeader.ShowFrame && frameHeader.FilmGrainParameters.ApplyGrain && retainsReference;
            if (needsSeparatePresentation)
            {
                presentationBuffer = new Av1FrameBuffer<byte>(
                    this.configuration,
                    sequenceHeader,
                    sequenceHeader.ColorConfig.GetColorFormat(),
                    false);

                // Film grain must never contaminate a decoded reference. A shown frame that is also refreshed therefore
                // receives one allocator-owned presentation copy; frames with no reference role are grained in place.
                frameBuffer.CopyTo(presentationBuffer);
            }

            Av1FrameBuffer<byte> grainTarget = presentationBuffer ?? frameBuffer;
            if (frameHeader.ShowFrame && frameHeader.FilmGrainParameters.ApplyGrain)
            {
                Av1FilmGrainDecoder filmGrainDecoder = new(sequenceHeader, frameHeader, grainTarget);
                filmGrainDecoder.DecodeFrame();
            }

            Av1ReferenceFrame referenceFrame;
            if (retainsReference)
            {
                Av1FrameEntropyContexts entropyContexts = this.entropyContexts!;
                Av1FrameEntropyContext entropySnapshot = entropyContexts.RentPublishedSnapshot();
                referenceFrame = new(frameBuffer, frameHeader, frameInfo, entropySnapshot, entropyContexts);
            }
            else
            {
                // Presentation-only frames can never become primary references, so they own no unused CDF graph.
                referenceFrame = new(frameBuffer, frameHeader, frameInfo);
            }

            frameBuffer = null;

            if (!this.referenceFrames.Commit(frameHeader.RefreshFrameFlags, referenceFrame, frameHeader.ShowFrame && !needsSeparatePresentation))
            {
                referenceFrame.Dispose();
            }

            if (presentationBuffer is not null)
            {
                Av1ReferenceFrame presentationFrame = new(presentationBuffer, frameHeader, frameInfo);
                presentationBuffer = null;
                this.referenceFrames.CommitOutput(presentationFrame);
            }

            if (frameHeader.ShowFrame)
            {
                this.SequenceHeader = sequenceHeader;
                this.FrameHeader = frameHeader;
                this.FrameInfo = frameInfo;
            }
        }
        finally
        {
            // A non-shown frame or failed reconstruction never escapes this callback. FrameInfo uses managed storage,
            // so it remains inspectable for a retained frame after the pooled entropy-neighbor contexts are returned.
            presentationBuffer?.Dispose();
            frameBuffer?.Dispose();
            tileReader.Dispose();
            this.tileReader = null;
        }
    }

    /// <summary>
    /// Releases the current tile parser, reference map, and retained presentation output.
    /// </summary>
    public void Dispose()
    {
        this.tileReader?.Dispose();
        this.tileReader = null;
        this.referenceFrames.Dispose();
    }
}
