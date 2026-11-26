// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Components.Alpha;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Xmp;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Performs the HEIF decoding operation.
/// </summary>
internal sealed class HeifDecoderCore : ImageDecoderCore
{
    /// <summary>
    /// Marks an item property whose box type is not understood by this decoder.
    /// </summary>
    private static readonly object UnknownProperty = new();

    /// <summary>
    /// Marks an understood item property whose value was skipped or discarded by decoder policy.
    /// </summary>
    private static readonly object IgnoredProperty = new();

    /// <summary>
    /// The general configuration.
    /// </summary>
    private readonly Configuration configuration;

    /// <summary>
    /// The chroma reconstruction mode selected for this decode.
    /// </summary>
    private readonly HeifChromaUpsampling chromaUpsampling;

    /// <summary>
    /// The general options passed to nested coded-image decoders without presentation-level target scaling.
    /// </summary>
    private readonly DecoderOptions payloadOptions;

    /// <summary>
    /// The <see cref="ImageMetadata"/> decoded by this decoder instance.
    /// </summary>
    private readonly ImageMetadata metadata;

    /// <summary>
    /// The shared bounded box reader used by the item and image-sequence container paths.
    /// </summary>
    private readonly HeifBoxReader boxReader;

    /// <summary>
    /// The bounded image-sequence parser sharing the configured frame and metadata policy.
    /// </summary>
    private readonly HeifSequenceParser sequenceParser;

    /// <summary>
    /// The fixed buffer reused for all item-container box headers in this decode operation.
    /// </summary>
    private readonly byte[] boxHeaderBuffer;

    /// <summary>
    /// The item identifier selected by the primary-item box.
    /// </summary>
    private uint primaryItem;

    /// <summary>
    /// The item declarations parsed from the item-information box, indexed by item identifier.
    /// </summary>
    /// <remarks>
    /// The item-information, item-reference, item-property-association, and item-location boxes each resolve one item
    /// per entry. A file can declare tens of thousands of items, so a keyed lookup keeps parsing linear in the entry count.
    /// </remarks>
    private readonly Dictionary<uint, HeifItem> items;

    /// <summary>
    /// The typed relationships parsed from the item-reference box.
    /// </summary>
    private readonly List<HeifItemLink> itemLinks;

    /// <summary>
    /// The absolute stream position at which the current HEIF file begins.
    /// </summary>
    private long fileStartOffset;

    /// <summary>
    /// The stream range of the item-data box payload. The range is not present when no item-data box exists.
    /// </summary>
    private HeifBoxRange itemData;

    /// <summary>
    /// Initializes a new instance of the <see cref="HeifDecoderCore" /> class.
    /// </summary>
    /// <param name="decoderOptions">The decoder options.</param>
    public HeifDecoderCore(HeifDecoderOptions decoderOptions)
        : base(decoderOptions.GeneralOptions)
    {
        DecoderOptions options = decoderOptions.GeneralOptions;
        this.chromaUpsampling = decoderOptions.ChromaUpsampling;
        this.configuration = options.Configuration;

        // Resize only the final presentation. Retain ICC until item/grid profile selection and native conversion
        // finish; ImageDecoder then applies the caller's profile-retention policy.
        this.payloadOptions = options.TargetSize is null && options.ColorProfileHandling == ColorProfileHandling.Preserve
            ? options
            : new DecoderOptions(options.Configuration)
            {
                Sampler = options.Sampler,
                SkipMetadata = options.SkipMetadata,
                MaxFrames = options.MaxFrames,
                SegmentIntegrityHandling = options.SegmentIntegrityHandling,
                ColorProfileHandling = ColorProfileHandling.Preserve
            };

        this.metadata = new ImageMetadata();
        this.boxReader = new HeifBoxReader(this.configuration.MemoryAllocator);
        this.sequenceParser = new HeifSequenceParser(options);
        this.boxHeaderBuffer = new byte[8];
        this.items = [];
        this.itemLinks = [];
    }

    /// <summary>
    /// Describes how the item property association box can associate one property type.
    /// </summary>
    [Flags]
    private enum AssociationRule
    {
        /// <summary>
        /// The property type has no association rule.
        /// </summary>
        None = 0,

        /// <summary>
        /// The association must have the essential flag.
        /// </summary>
        Essential = 1,

        /// <summary>
        /// The association must not have the essential flag.
        /// </summary>
        NotEssential = 2,

        /// <summary>
        /// Only an AV1 image item can have the property.
        /// </summary>
        Av1Only = 4,

        /// <summary>
        /// The property describes only metadata. If SkipMetadata is <see langword="true"/>, the decoder skips the property. The decoder reports damage in the
        /// property as an ancillary error.
        /// </summary>
        Metadata = 8
    }

    /// <summary>
    /// Gets the dependency order in which recognized metadata children are interpreted.
    /// </summary>
    private static ReadOnlySpan<Heif4CharCode> MetadataParseOrder =>
    [
        Heif4CharCode.Hdlr,
        Heif4CharCode.Iinf,
        Heif4CharCode.Pitm,
        Heif4CharCode.Iref,
        Heif4CharCode.Iloc,
        Heif4CharCode.Iprp,
        Heif4CharCode.Idat
    ];

    /// <inheritdoc/>
    protected override Image<TPixel> Decode<TPixel>(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        this.fileStartOffset = stream.Position;
        HeifFileType fileType = this.ReadFileTypeBox(stream);
        if (fileType == HeifFileType.Unsupported)
        {
            throw new ImageFormatException("Not an HEIF image.");
        }

        if (fileType == HeifFileType.ImageSequence)
        {
            HeifSequence sequence = this.ParseImageSequence(stream);
            return this.DecodeImageSequence<TPixel>(stream, sequence, cancellationToken);
        }

        this.items.Clear();
        this.itemLinks.Clear();
        this.itemData = default;

        // Item locations are absolute file offsets or idat-relative offsets, so payload bytes need not be adjacent to
        // the metadata box. Complete the top-level scan before resolving and decoding the primary item.
        while (stream.Position < stream.Length)
        {
            long boxLength = this.ReadTopLevelBoxHeader(stream, out Heif4CharCode boxType);
            switch (boxType)
            {
                case Heif4CharCode.Meta:
                    this.ParseMetadata(stream, boxLength);
                    break;
                case Heif4CharCode.Mdat:
                case Heif4CharCode.Free:
                    HeifBoxReader.Skip(stream, boxLength);
                    break;
                case 0U:
                    // Some files end with zero bytes. Skip to the end of the stream.
                    HeifBoxReader.Skip(stream, stream.Length - stream.Position);
                    break;
                default:
                    HeifBoxReader.Skip(stream, boxLength);
                    break;
            }
        }

        return this.DecodePrimaryItem<TPixel>(stream, cancellationToken);
    }

    /// <inheritdoc/>
    protected override ImageInfo Identify(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        this.fileStartOffset = stream.Position;
        HeifFileType fileType = this.ReadFileTypeBox(stream);
        if (fileType == HeifFileType.Unsupported)
        {
            throw new ImageFormatException("Not an HEIF image.");
        }

        if (fileType == HeifFileType.ImageSequence)
        {
            return this.IdentifyImageSequence(this.ParseImageSequence(stream));
        }

        this.items.Clear();
        this.itemLinks.Clear();
        this.itemData = default;

        // Identification reads only the container model. Payload boxes remain skipped because dimensions and format
        // metadata come from item declarations and associated properties rather than reconstructed pixels.
        while (stream.Position < stream.Length)
        {
            long boxLength = this.ReadTopLevelBoxHeader(stream, out Heif4CharCode boxType);
            switch (boxType)
            {
                case Heif4CharCode.Meta:
                    this.ParseMetadata(stream, boxLength);
                    break;
                default:
                    // Silently skip all other box types.
                    HeifBoxReader.Skip(stream, boxLength);
                    break;
            }
        }

        HeifDecodePlan plan = HeifItemGraphValidator.Validate(this.items, this.itemLinks, this.primaryItem, this.Options.SegmentIntegrityHandling);
        this.UpdateMetadata(this.metadata, plan);

        Size presentationExtent = GetPresentationExtent(plan);
        return new ImageInfo(new(presentationExtent.Width, presentationExtent.Height), this.metadata);
    }

    /// <summary>
    /// Reads the header of a top-level box after the file-type box.
    /// </summary>
    /// <remarks>
    /// A media data box holds only item and sample payloads. When a file ends inside it, the payloads are damaged image
    /// data. Strict and IgnoreAncillary throw. IgnoreImageData keeps the bytes that the file holds, so each item and sample
    /// keeps the part of its payload that is present. A truncated box of another type throws in every mode.
    /// </remarks>
    /// <param name="stream">The container stream positioned at the box size field.</param>
    /// <param name="boxType">Receives the box four-character code.</param>
    /// <returns>The number of payload bytes that the file holds after the complete box header.</returns>
    private long ReadTopLevelBoxHeader(BufferedReadStream stream, out Heif4CharCode boxType)
    {
        long boxLength = HeifBoxReader.ReadHeader(stream, stream.Length, this.boxHeaderBuffer, out boxType, true, out bool truncated);
        if (truncated)
        {
            if (boxType != Heif4CharCode.Mdat)
            {
                throw new InvalidImageContentException("Box size extends beyond its parent boundary.");
            }

            this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError("The HEIF media data box is truncated.");
        }

        return boxLength;
    }

    /// <summary>
    /// Reads and validates the leading file-type box against the image presentations supported by this decoder.
    /// </summary>
    /// <param name="stream">The container stream positioned at its first top-level box.</param>
    /// <returns>The declared supported image presentation, or <see cref="HeifFileType.Unsupported"/>.</returns>
    private HeifFileType ReadFileTypeBox(BufferedReadStream stream)
    {
        long boxLength = HeifBoxReader.ReadHeader(stream, stream.Length, this.boxHeaderBuffer, out Heif4CharCode boxType, true);
        if (boxType != Heif4CharCode.Ftyp)
        {
            return HeifFileType.Unsupported;
        }

        if (boxLength < 8 || boxLength > int.MaxValue || (boxLength & 3) != 0)
        {
            return HeifFileType.Unsupported;
        }

        using IMemoryOwner<byte> boxMemory = this.boxReader.ReadPayload(stream, boxLength);
        Span<byte> boxBuffer = boxMemory.GetSpan();
        return HeifConstants.TryGetFileType(boxBuffer, out HeifFileType fileType) ? fileType : HeifFileType.Unsupported;
    }

    /// <summary>
    /// Locates and parses the single movie box of a supported AVIF image sequence.
    /// </summary>
    /// <param name="stream">The complete container stream positioned after its file-type box.</param>
    /// <returns>The bounded selected image-sequence model.</returns>
    private HeifSequence ParseImageSequence(BufferedReadStream stream)
    {
        this.items.Clear();
        this.itemLinks.Clear();
        this.itemData = default;

        HeifSequence? sequence = null;
        while (stream.Position < stream.Length)
        {
            long boxLength = this.ReadTopLevelBoxHeader(stream, out Heif4CharCode boxType);
            switch (boxType)
            {
                case Heif4CharCode.Meta:
                    this.ParseMetadata(stream, boxLength);
                    break;
                case Heif4CharCode.Moov:
                    if (sequence is not null)
                    {
                        throw new InvalidImageContentException("The HEIF image sequence contains more than one movie box.");
                    }

                    sequence = this.sequenceParser.Parse(stream, boxLength, this.fileStartOffset);
                    break;
                default:
                    // Sequence samples and image items use file-relative offsets, so payload boxes never need buffering.
                    HeifBoxReader.Skip(stream, boxLength);
                    break;
            }
        }

        return sequence ?? throw new InvalidImageContentException("The HEIF image sequence contains no movie box.");
    }

    /// <summary>
    /// Creates image and frame metadata from a parsed AVIF image sequence without decoding its samples.
    /// </summary>
    /// <param name="sequence">The parsed selected image sequence.</param>
    /// <returns>The identified dimensions and bounded visible-frame metadata.</returns>
    private ImageInfo IdentifyImageSequence(HeifSequence sequence)
    {
        HeifSequenceTrack colorTrack = sequence.ColorTrack;
        HeifItem? primaryItem = this.FindItemById(this.primaryItem);
        bool animateRootFrame = primaryItem is null || IsPrimaryItemFirstSequenceSample(primaryItem, colorTrack);
        Size sequenceExtent = GetSequencePresentationExtent(colorTrack);
        this.Dimensions = sequenceExtent;
        if (primaryItem is not null)
        {
            HeifDecodePlan plan = HeifItemGraphValidator.Validate(this.items, this.itemLinks, this.primaryItem, this.Options.SegmentIntegrityHandling);
            if (!animateRootFrame)
            {
                this.Dimensions = GetPresentationExtent(plan);
            }

            this.UpdateMetadata(this.metadata, plan);
        }

        this.UpdateSequenceMetadata(this.metadata, sequence, animateRootFrame);
        ImageFrameMetadata[] frameMetadata = CreateSequenceFrameMetadata(colorTrack, animateRootFrame);
        return new ImageInfo(this.Dimensions, this.metadata, frameMetadata);
    }

    /// <summary>
    /// Decodes the retained visible samples of an AVIF image sequence into one multi-frame image.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel format.</typeparam>
    /// <param name="stream">The complete seekable HEIF stream.</param>
    /// <param name="sequence">The parsed selected image sequence.</param>
    /// <param name="cancellationToken">The token used to cancel work between coded samples.</param>
    /// <returns>The decoded multi-frame image.</returns>
    private Image<TPixel> DecodeImageSequence<TPixel>(
        BufferedReadStream stream,
        HeifSequence sequence,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        HeifSequenceTrack colorTrack = sequence.ColorTrack;
        HeifItem? primaryItem = this.FindItemById(this.primaryItem);

        // Track samples carry their own codec configuration and presentation metadata. When no still item is
        // available, the first visible sample supplies the root. An independent still item keeps its own root.
        bool animateRootFrame = primaryItem is null || IsPrimaryItemFirstSequenceSample(primaryItem, colorTrack);
        this.UpdateSequenceMetadata(this.metadata, sequence, animateRootFrame);
        Size codedSize = new(colorTrack.CodedWidth, colorTrack.CodedHeight);
        Rectangle sourceRectangle = colorTrack.CleanAperture is not null
            ? colorTrack.CleanAperture.Value.ToRectangle(codedSize)
            : new Rectangle(Point.Empty, codedSize);

        HeifPixelTransform transform = new(colorTrack.RotationAngle ?? 0, colorTrack.MirrorAxis);
        Size presentationSize = transform.GetDestinationSize(sourceRectangle.Size);

        // The returned image owns every presented frame from the start. A separate primary item becomes its root.
        // Otherwise the first decoded timed sample fills the root that this call allocates.
        Image<TPixel> image = animateRootFrame
            ? new Image<TPixel>(
                this.configuration,
                presentationSize.Width,
                presentationSize.Height,
                this.metadata)
            : this.DecodePrimaryItem<TPixel>(stream, cancellationToken);

        try
        {
            Rectangle outputBounds = new(
                0, 0, Math.Min(presentationSize.Width, image.Width), Math.Min(presentationSize.Height, image.Height));

            Rectangle visibleSource = transform.GetSourceRectangle(outputBounds, sourceRectangle.Size);
            sourceRectangle = new Rectangle(
                sourceRectangle.X + visibleSource.X,
                sourceRectangle.Y + visibleSource.Y,
                visibleSource.Width,
                visibleSource.Height);

            // The image exists before the first sample decodes. Thus, in the IgnoreImageData mode, a sample with damage before its decoded image data gives no
            // frame. The AV1 code reports the damage one time. Then the loop skips the sample.
            using Av1Decoder colorDecoder = new(this.configuration, 0, this.Options.SegmentIntegrityHandling, true);

            HeifSequenceTrack? alphaTrack = sequence.AlphaTrack;
            (HeifSequenceTrack Track, Av1Decoder Decoder)? alphaState = alphaTrack is null
                ? null
                : (alphaTrack, new Av1Decoder(this.configuration, 0, this.Options.SegmentIntegrityHandling, true));

            using Av1Decoder? alphaDecoder = alphaState?.Decoder;

            int decodedFrameCount = 0;
            for (int sampleIndex = 0; sampleIndex < colorTrack.Samples.Length; sampleIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                HeifSequenceSample colorSample = colorTrack.Samples[sampleIndex];
                bool appendedDestination = false;
                ImageFrame<TPixel> decodedFrame;
                if (animateRootFrame && decodedFrameCount == 0)
                {
                    decodedFrame = image.Frames.RootFrame;
                }
                else
                {
                    decodedFrame = image.Frames.CreateFrame();
                    appendedDestination = true;
                }

                Av1FrameBuffer<byte>? alphaFrame = null;
                bool alphaDecoded = true;
                if (alphaState is not null)
                {
                    (HeifSequenceTrack Track, Av1Decoder Decoder) currentAlphaState = alphaState.Value;
                    HeifSequenceSample alphaSample = currentAlphaState.Track.Samples[sampleIndex];
                    alphaDecoded = this.TryDecodeSequenceAlphaFrame(stream, currentAlphaState.Track, alphaSample, currentAlphaState.Decoder, out alphaFrame);
                }

                if (!alphaDecoded)
                {
                    // The decoder still decodes the color sample, because the later color samples can refer to it.
                    this.DecodeSequenceReference(stream, colorTrack, colorSample, colorDecoder);
                    if (appendedDestination)
                    {
                        image.Frames.RemoveFrame(image.Frames.Count - 1);
                    }

                    continue;
                }

                bool colorDecoded = this.TryDecodeSequenceFrame(
                    stream,
                    colorTrack,
                    colorSample,
                    colorDecoder,
                    sourceRectangle,
                    decodedFrame.PixelBuffer.GetRegion(outputBounds),
                    transform,
                    alphaFrame,
                    alphaFrame is not null && colorTrack.IsPremultiplied,
                    out CicpProfile? outputProfile);

                if (!colorDecoded)
                {
                    if (appendedDestination)
                    {
                        image.Frames.RemoveFrame(image.Frames.Count - 1);
                    }

                    continue;
                }

                if (!this.Options.SkipMetadata)
                {
                    decodedFrame.Metadata.CicpProfile = outputProfile;
                }

                ImageFrame<TPixel> presentedFrame = decodedFrame;
                presentedFrame.Metadata.GetHeifMetadata().FrameDelay = new Rational(
                    colorSample.Duration,
                    colorTrack.MediaTimescale);

                // Each timed frame uses the track profile, including when a separate still image owns the root.
                // Keep it under SkipMetadata so later encoding can restore the source color space.
                presentedFrame.Metadata.IccProfile = colorTrack.IccProfile;

                decodedFrameCount++;
            }

            if (decodedFrameCount == 0)
            {
                // The decoder reported each skipped sample at the check that found its damage. A sequence that presents no sample is not an image sequence.
                // Thus the decoder throws in each mode, also when a separate primary item gives the root frame.
                throw new InvalidImageContentException("The HEIF image sequence contains no decodable visible samples.");
            }

            if (!this.Options.SkipMetadata)
            {
                image.Metadata.CicpProfile = image.Frames.RootFrame.Metadata.CicpProfile;
            }

            HeifMetadata resultMetadata = image.Metadata.GetHeifMetadata();
            resultMetadata.RepeatCount = colorTrack.RepeatCount;
            resultMetadata.AnimateRootFrame = animateRootFrame;
            resultMetadata.HasAlpha |= alphaState is not null;
            this.Dimensions = image.Size;
            return image;
        }
        catch
        {
            image.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Reads and decodes one bounded coded sample without retaining its encoded byte buffer.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel format.</typeparam>
    /// <param name="stream">The complete seekable HEIF stream.</param>
    /// <param name="track">The track supplying the codec configuration and color description.</param>
    /// <param name="sample">The validated sample range.</param>
    /// <param name="decoder">The decoder retaining earlier sequence references.</param>
    /// <param name="sourceRectangle">The clean-aperture region mapped to the destination frame.</param>
    /// <param name="destination">The caller-owned frame receiving the presented sample.</param>
    /// <param name="transform">The rotation and mirroring within the destination region.</param>
    /// <param name="alphaFrame">The decoder-owned auxiliary frame, or null for opaque pixels.</param>
    /// <param name="premultiplied">Whether source RGB is associated with alpha.</param>
    /// <param name="outputProfile">Receives the output color description, or null when metadata is skipped.</param>
    /// <returns>
    /// <see langword="true"/> if the sample decodes. <see langword="false"/> if the track or sample cannot be decoded
    /// and the mode ignores image data errors.
    /// </returns>
    private bool TryDecodeSequenceFrame<TPixel>(
        BufferedReadStream stream,
        HeifSequenceTrack track,
        HeifSequenceSample sample,
        Av1Decoder decoder,
        Rectangle sourceRectangle,
        Buffer2DRegion<TPixel> destination,
        HeifPixelTransform transform,
        Av1FrameBuffer<byte>? alphaFrame,
        bool premultiplied,
        out CicpProfile? outputProfile)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        outputProfile = null;

        // The sequence parser accepts only an AV1 sample entry with a codec configuration.
        Av1CodecConfiguration codecConfiguration = track.Av1CodecConfiguration!;
        if (!this.TryReadSequenceSample(stream, track, sample, codecConfiguration, out IMemoryOwner<byte>? sampleOwner, out Span<byte> sampleData))
        {
            return false;
        }

        using IMemoryOwner<byte> ownedSample = sampleOwner;

        // Preserve and Convert both produce sRGB pixels. The base decoder controls profile retention.
        IccProfile.TryGetIccProfileForColorConversion(track.IccProfile, out IccProfile? profile);

        decoder.ChromaUpsampling = this.chromaUpsampling;
        if (!decoder.DecodeSequenceFrame(
            sampleData,
            track.CicpProfile,
            codecConfiguration,
            new Size(track.CodedWidth, track.CodedHeight),
            sourceRectangle,
            destination,
            transform,
            profile,
            alphaFrame,
            premultiplied,
            out CicpProfile? sourceProfile))
        {
            // The AV1 decoder reported the damage. The mode ignores the damage, so the sample gives no frame.
            return false;
        }

        if (this.Options.SkipMetadata)
        {
            return true;
        }

        // Sequence output has the same packed RGB description as a still image. ICC conversion changes the
        // primaries and transfer to sRGB. Without ICC conversion, the source primaries and transfer stay.
        outputProfile = profile is null || profile.IsUnusableForDecoding
            ? new CicpProfile(
                (byte)sourceProfile.ColorPrimaries,
                (byte)sourceProfile.TransferCharacteristics,
                (byte)CicpMatrixCoefficients.Identity,
                true)
            : new CicpProfile(1, 13, 0, true);

        return true;
    }

    /// <summary>
    /// Decodes one AV1 auxiliary sample and returns its native plane.
    /// </summary>
    /// <param name="stream">The complete seekable HEIF stream.</param>
    /// <param name="track">The alpha track supplying the codec configuration and color description.</param>
    /// <param name="sample">The validated alpha sample range.</param>
    /// <param name="decoder">The decoder retaining earlier alpha-sequence references.</param>
    /// <param name="alphaFrame">
    /// Receives the auxiliary plane retained by the sequence decoder until the next sample is decoded.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the sample decodes. <see langword="false"/> if the track or sample cannot be decoded
    /// and the mode ignores image data errors.
    /// </returns>
    private bool TryDecodeSequenceAlphaFrame(
        BufferedReadStream stream,
        HeifSequenceTrack track,
        HeifSequenceSample sample,
        Av1Decoder decoder,
        out Av1FrameBuffer<byte>? alphaFrame)
    {
        alphaFrame = null;

        // The sequence parser accepts only a monochrome AV1 alpha track with a codec configuration.
        Av1CodecConfiguration codecConfiguration = track.Av1CodecConfiguration!;
        if (!this.TryReadSequenceSample(stream, track, sample, codecConfiguration, out IMemoryOwner<byte>? sampleOwner, out Span<byte> sampleData))
        {
            return false;
        }

        using IMemoryOwner<byte> ownedSample = sampleOwner;

        // A null frame means that the AV1 decoder reported damage and that the mode ignores the damage.
        alphaFrame = decoder.DecodeSequenceAlpha(sampleData, track.CicpProfile, codecConfiguration, new Size(track.CodedWidth, track.CodedHeight));
        return alphaFrame is not null;
    }

    /// <summary>
    /// Decodes one non-presented sequence sample so later dependent samples can resolve its retained references.
    /// </summary>
    /// <param name="stream">The complete seekable HEIF stream.</param>
    /// <param name="track">The track supplying the codec configuration and color description.</param>
    /// <param name="sample">The validated non-presented sample.</param>
    /// <param name="decoder">The decoder retaining sequence reference state.</param>
    private void DecodeSequenceReference(
        BufferedReadStream stream,
        HeifSequenceTrack track,
        HeifSequenceSample sample,
        Av1Decoder decoder)
    {
        // The sequence parser accepts only an AV1 sample entry with a codec configuration.
        Av1CodecConfiguration codecConfiguration = track.Av1CodecConfiguration!;
        if (!this.TryReadSequenceSample(stream, track, sample, codecConfiguration, out IMemoryOwner<byte>? sampleOwner, out Span<byte> sampleData))
        {
            return;
        }

        using IMemoryOwner<byte> ownedSample = sampleOwner;
        decoder.DecodePayload(sampleData, track.CicpProfile, codecConfiguration, null, false, out _);
    }

    /// <summary>
    /// Reads one bounded AV1 sequence sample into a buffer from the memory allocator. Then the AV1 codec configuration validates the sample data.
    /// </summary>
    /// <param name="stream">The complete seekable HEIF stream.</param>
    /// <param name="track">The track that supplies the color description.</param>
    /// <param name="sample">The validated sample range.</param>
    /// <param name="codecConfiguration">The AV1 codec configuration of the track.</param>
    /// <param name="sampleOwner">Receives the buffer from the memory allocator that holds the validated coded sample. The caller disposes it.</param>
    /// <param name="sampleData">Receives the coded sample bytes in <paramref name="sampleOwner"/>. The bytes are valid until the caller disposes it.</param>
    /// <returns>
    /// <see langword="true"/> if the method reads the sample. <see langword="false"/> if the mode ignores image data errors, and the sample has no bytes or the
    /// AV1 sample data has damage.
    /// </returns>
    /// <remarks>
    /// This method is the only image data report for a sample that ends after the end of the file. The sequence parser limits the sample to the bytes that the
    /// file holds, and marks the sample as truncated. In the IgnoreImageData mode, a truncated sample decodes in part. A sample with no bytes gives no frame.
    /// The AV1 codec configuration reports damaged sample data. Because the image exists before the first sample, a damaged first sample also gives no frame in
    /// the IgnoreImageData mode.
    /// </remarks>
    private bool TryReadSequenceSample(
        BufferedReadStream stream,
        HeifSequenceTrack track,
        HeifSequenceSample sample,
        Av1CodecConfiguration codecConfiguration,
        [NotNullWhen(true)] out IMemoryOwner<byte>? sampleOwner,
        out Span<byte> sampleData)
    {
        sampleOwner = null;
        sampleData = default;
        if (sample.IsTruncated)
        {
            this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError("The HEIF image-sequence sample extends beyond the file.");

            // The parser rejects a declared size of 0. Thus a sample has no bytes only when the file ends before the sample.
            if (sample.Length == 0)
            {
                return false;
            }
        }

        IMemoryOwner<byte> owner = this.configuration.MemoryAllocator.Allocate<byte>(sample.Length);
        try
        {
            sampleData = owner.GetSpan()[..sample.Length];
            stream.Position = this.fileStartOffset + sample.Offset;
            HeifBoxReader.ReadExactly(stream, sampleData, "The HEIF image-sequence sample is truncated.");
            if (!codecConfiguration.ValidateData(
                sampleData,
                sample.IsSync,
                "AV1 track sample",
                track.ContentLightLevel,
                track.MasteringDisplayColorVolume,
                this.Options,
                true,
                out _,
                out _))
            {
                owner.Dispose();
                return false;
            }

            sampleOwner = owner;
            return true;
        }
        catch
        {
            owner.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Updates image-level metadata from the selected color and optional alpha sequence tracks.
    /// </summary>
    /// <param name="metadata">The image metadata receiving the sequence description.</param>
    /// <param name="sequence">The parsed selected image sequence.</param>
    /// <param name="animateRootFrame">Whether the primary image is also the first timed sample.</param>
    private void UpdateSequenceMetadata(ImageMetadata metadata, HeifSequence sequence, bool animateRootFrame)
    {
        HeifSequenceTrack colorTrack = sequence.ColorTrack;
        HeifMetadata heifMetadata = metadata.GetHeifMetadata();
        heifMetadata.RepeatCount = colorTrack.RepeatCount;
        heifMetadata.AnimateRootFrame = animateRootFrame;
        heifMetadata.HasAlpha |= sequence.AlphaTrack is not null;
        if (!animateRootFrame)
        {
            return;
        }

        // The sequence parser accepts only an AV1 sample entry with a codec configuration.
        Av1CodecConfiguration av1Configuration = colorTrack.Av1CodecConfiguration!;
        heifMetadata.BitDepth = av1Configuration.BitDepth;
        heifMetadata.IsMonochrome = av1Configuration.IsMonochrome;
        heifMetadata.ChromaSubsampling = av1Configuration.GetChromaSubsampling();

        // The selected track is decoder-private and no longer mutates after parsing. Reuse its profiles so the
        // returned metadata does not duplicate their storage.
        metadata.IccProfile = colorTrack.IccProfile;
        if (this.Options.SkipMetadata)
        {
            // ICC remains required for color conversion; optional track metadata can be omitted.
            return;
        }

        metadata.CicpProfile = colorTrack.CicpProfile;
        heifMetadata.ContentLightLevel = colorTrack.ContentLightLevel;
        heifMetadata.MasteringDisplayColorVolume = colorTrack.MasteringDisplayColorVolume;
        heifMetadata.ContentColorVolume = colorTrack.ContentColorVolume;
        heifMetadata.AmbientViewingEnvironment = colorTrack.AmbientViewingEnvironment;
        heifMetadata.ReferenceViewingEnvironment = colorTrack.ReferenceViewingEnvironment;
        heifMetadata.NominalDiffuseWhite = colorTrack.NominalDiffuseWhite;
        ApplyPixelAspectRatioMetadata(metadata, colorTrack.PixelAspectRatio, colorTrack.RotationAngle);

        HeifSequenceMetadata? trackMetadata = colorTrack.Metadata;
        if (trackMetadata?.ExifData is not null)
        {
            this.ApplyExifProfile(metadata, trackMetadata.ExifData);
        }

        if (trackMetadata?.XmpData is not null)
        {
            metadata.XmpProfile = XmpReader.ReadProfile(trackMetadata.XmpData);
        }
    }

    /// <summary>
    /// Creates one HEIF frame-metadata entry for each retained sequence sample.
    /// </summary>
    /// <param name="track">The selected color track supplying sample durations.</param>
    /// <param name="animateRootFrame">Whether the first sequence sample occupies the root-frame slot.</param>
    /// <returns>The exact frame metadata array in presentation order.</returns>
    private static ImageFrameMetadata[] CreateSequenceFrameMetadata(
        HeifSequenceTrack track,
        bool animateRootFrame)
    {
        int firstSequenceFrameIndex = animateRootFrame ? 0 : 1;
        ImageFrameMetadata[] result = new ImageFrameMetadata[track.Samples.Length + firstSequenceFrameIndex];
        if (!animateRootFrame)
        {
            result[0] = new ImageFrameMetadata();
        }

        int frameIndex = firstSequenceFrameIndex;
        foreach (HeifSequenceSample sample in track.Samples)
        {
            ImageFrameMetadata frameMetadata = new();
            frameMetadata.IccProfile = track.IccProfile;
            frameMetadata.GetHeifMetadata().FrameDelay = new Rational(sample.Duration, track.MediaTimescale);
            result[frameIndex++] = frameMetadata;
        }

        return result;
    }

    /// <summary>
    /// Determines whether the primary image item reuses the first presented sequence sample.
    /// </summary>
    private static bool IsPrimaryItemFirstSequenceSample(HeifItem primaryItem, HeifSequenceTrack colorTrack)
    {
        if (primaryItem.DataLocations.Count != 1 || colorTrack.Samples.Length == 0)
        {
            return false;
        }

        HeifLocation location = primaryItem.DataLocations[0];
        HeifSequenceSample sample = colorTrack.Samples[0];
        return location.Origin == HeifLocationOffsetOrigin.FileOffset
            && location.BaseOffset + location.Offset == sample.Offset
            && location.Length == sample.Length;
    }

    /// <summary>
    /// Updates identification metadata from the primary item.
    /// </summary>
    /// <param name="metadata">The destination image metadata.</param>
    /// <param name="plan">The decode plan of the primary image.</param>
    private void UpdateMetadata(ImageMetadata metadata, HeifDecodePlan plan)
    {
        // A grid is a derived image, not a compression method. The item graph validator validated that each tile uses the AV1 codec configuration of the first
        // tile.
        Av1CodecConfiguration codecConfiguration = plan.CodedItem.Av1CodecConfiguration!;
        HeifMetadata meta = metadata.GetHeifMetadata();
        meta.BitDepth = codecConfiguration.BitDepth;
        meta.IsMonochrome = codecConfiguration.IsMonochrome;
        meta.ChromaSubsampling = codecConfiguration.GetChromaSubsampling();
        meta.HasAlpha = plan.AlphaItem is not null;

        this.ApplyItemColorMetadata(metadata, plan);
        if (!this.Options.SkipMetadata)
        {
            ApplyItemHdrMetadata(metadata, plan);
        }
    }

    /// <summary>
    /// Indexes and parses the recognized children of a metadata box.
    /// </summary>
    /// <param name="stream">The stream positioned at the metadata full-box header.</param>
    /// <param name="boxLength">The bounded metadata payload length.</param>
    private void ParseMetadata(BufferedReadStream stream, long boxLength)
    {
        long endPosition = stream.Position + boxLength;
        HeifItemBoxParser.ReadMetadataHeader(stream, boxLength, this.boxHeaderBuffer, HeifSpanReader.AnyFlags, "metadata");

        // Physical child order is not a dependency order. Record bounded payload positions first, then parse item
        // declarations before the locations, references, and properties that resolve those identifiers.
        Span<HeifBoxRange> boxes = stackalloc HeifBoxRange[MetadataParseOrder.Length];
        HeifBoxReader.IndexChildren(stream, endPosition - stream.Position, this.boxHeaderBuffer, MetadataParseOrder, boxes, 0, "metadata");
        foreach (HeifBoxRange box in boxes)
        {
            if (!box.IsPresent)
            {
                continue;
            }

            stream.Position = box.Offset;
            if (box.Type == Heif4CharCode.Iprp)
            {
                this.ParseItemProperties(stream, box.Length);
                continue;
            }

            if (box.Type == Heif4CharCode.Idat)
            {
                if (box.Length == 0)
                {
                    throw new InvalidImageContentException("The item data box is empty.");
                }

                // The iloc construction method 1 addresses bytes from the start of the idat payload, not from the start of its header.
                this.itemData = box;
                continue;
            }

            using IMemoryOwner<byte> payload = this.boxReader.ReadPayload(stream, box.Length);
            Span<byte> data = payload.GetSpan();
            switch (box.Type)
            {
                case Heif4CharCode.Hdlr:
                    // This parser reads only still-image metadata. Thus the metadata box must have the picture handler.
                    if (HeifItemBoxParser.ParseHandler(data) != Heif4CharCode.Pict)
                    {
                        throw new ImageFormatException("Not a picture file.");
                    }

                    break;
                case Heif4CharCode.Iinf:
                    HeifItemBoxParser.ParseItemInfo(data, this.items);
                    break;
                case Heif4CharCode.Pitm:
                    this.primaryItem = ParsePrimaryItem(data);
                    break;
                case Heif4CharCode.Iref:
                    this.ParseItemReference(data);
                    break;
                default:
                    HeifItemBoxParser.ParseItemLocation(data, this.items);
                    break;
            }
        }

        stream.Position = endPosition;
    }

    /// <summary>
    /// Parses typed relationships between source and destination items.
    /// </summary>
    /// <remarks>
    /// The parser reads only the syntax. <see cref="HeifItemGraphValidator"/> and the metadata reader validate that the references they use name declared
    /// items.
    /// </remarks>
    /// <param name="payload">The item-reference full-box payload.</param>
    private void ParseItemReference(ReadOnlySpan<byte> payload)
    {
        HeifSpanReader reader = new(payload, "item reference");
        byte version = reader.ReadFullBoxHeader(1, HeifSpanReader.AnyFlags, out _);

        // ISO/IEC 14496-12, section 8.11.12: version 1 widens the item identifiers from 16 to 32 bits.
        bool largeIds = version == 1;
        while (reader.Remaining > 0)
        {
            ReadOnlySpan<byte> remaining = reader.RemainingData;
            int referenceHeaderLength = HeifBoxReader.ParseHeader(remaining, out long referenceLength, out Heif4CharCode linkType);
            reader.Skip(referenceHeaderLength + (int)referenceLength);

            if (linkType is not Heif4CharCode.Dimg
                and not Heif4CharCode.Auxl
                and not Heif4CharCode.Prem
                and not Heif4CharCode.Thmb
                and not Heif4CharCode.Cdsc)
            {
                // Unknown reference types do not participate in the bounded image model. Their child-box boundary
                // was validated above, so skip the payload without imposing semantics from a general ISOBMFF reader.
                continue;
            }

            if (this.Options.SkipMetadata && linkType == Heif4CharCode.Cdsc)
            {
                // If SkipMetadata is true, the descriptive metadata links have no effect.
                continue;
            }

            HeifSpanReader referenceReader = new(remaining.Slice(referenceHeaderLength, (int)referenceLength), "item reference");
            HeifItemLink link = new(linkType, referenceReader.ReadU16Or32(largeIds));
            uint count = referenceReader.ReadEntryCount(2, largeIds ? 4 : 2);
            for (uint i = 0; i < count; i++)
            {
                link.DestinationIds.Add(referenceReader.ReadU16Or32(largeIds));
            }

            referenceReader.EnsureEnd();
            this.itemLinks.Add(link);
        }

        reader.EnsureEnd();
    }

    /// <summary>
    /// Reads the identifier of the primary item.
    /// </summary>
    /// <param name="payload">The primary-item full-box payload.</param>
    /// <returns>The primary item identifier.</returns>
    private static uint ParsePrimaryItem(ReadOnlySpan<byte> payload)
    {
        HeifSpanReader reader = new(payload, "primary item");
        byte version = reader.ReadFullBoxHeader(1, HeifSpanReader.AnyFlags, out _);
        uint primaryItem = reader.ReadU16Or32(version == 1);
        reader.EnsureEnd();
        return primaryItem;
    }

    /// <summary>
    /// Parses the ordered item-property table and applies its item associations.
    /// </summary>
    /// <param name="stream">The stream positioned at the item-properties payload.</param>
    /// <param name="boxLength">The bounded item-properties payload length.</param>
    private void ParseItemProperties(BufferedReadStream stream, long boxLength)
    {
        // Property types can repeat, and ipma can come before ipco in the file. Index the bounded children first,
        // so the decoder resolves the associations after it has the ordered property table.
        List<KeyValuePair<Heif4CharCode, object>> properties = [];
        long endBoxPosition = stream.Position + boxLength;
        (long Offset, long Length)? propertyContainer = null;
        List<(long Offset, long Length)> associations = [];
        while (stream.Position < endBoxPosition)
        {
            long containerLength = HeifBoxReader.ReadHeader(stream, endBoxPosition, this.boxHeaderBuffer, out Heif4CharCode containerType, topLevel: false);
            if (containerType == Heif4CharCode.Ipco)
            {
                if (propertyContainer.HasValue)
                {
                    throw new InvalidImageContentException("The item properties box contains duplicate property containers.");
                }

                propertyContainer = (stream.Position, containerLength);
            }
            else if (containerType == Heif4CharCode.Ipma)
            {
                associations.Add((stream.Position, containerLength));
            }

            // Unknown optional children remain bounded by iprp and do not expand the still-image model.
            HeifBoxReader.Skip(stream, containerLength);
        }

        if (!propertyContainer.HasValue)
        {
            throw new InvalidImageContentException("The item properties box does not contain a property container.");
        }

        stream.Position = propertyContainer.Value.Offset;
        this.ParsePropertyContainer(stream, propertyContainer.Value.Length, properties);
        foreach ((long offset, long length) in associations)
        {
            stream.Position = offset;
            using IMemoryOwner<byte> payload = this.boxReader.ReadPayload(stream, length);
            this.ParsePropertyAssociation(payload.GetSpan(), properties);
        }

        stream.Position = endBoxPosition;
    }

    /// <summary>
    /// Parses the ordered property boxes contained by an item-property container.
    /// </summary>
    /// <param name="stream">The stream positioned at the first property box.</param>
    /// <param name="boxLength">The bounded item-property-container payload length.</param>
    /// <param name="properties">The one-based association table in physical property order.</param>
    private void ParsePropertyContainer(BufferedReadStream stream, long boxLength, List<KeyValuePair<Heif4CharCode, object>> properties)
    {
        long endPosition = stream.Position + boxLength;
        while (stream.Position < endPosition)
        {
            long propertyLength = HeifBoxReader.ReadHeader(stream, endPosition, this.boxHeaderBuffer, out Heif4CharCode propertyType, topLevel: false);
            long propertyEnd = stream.Position + propertyLength;
            if (this.Options.SkipMetadata && (GetAssociationRule(propertyType) & AssociationRule.Metadata) != 0)
            {
                // These properties affect only exposed image metadata. Preserve their physical ipco positions while
                // avoiding payload allocation and validation when the caller requested no metadata.
                HeifBoxReader.Skip(stream, propertyLength);
                properties.Add(new KeyValuePair<Heif4CharCode, object>(propertyType, IgnoredProperty));
                continue;
            }

            if (propertyType == Heif4CharCode.Colr && propertyLength >= 4)
            {
                Span<byte> profileTypeBuffer = this.boxHeaderBuffer.AsSpan(0, 4);
                HeifBoxReader.ReadExactly(stream, profileTypeBuffer, "The HEIF color-information property is truncated.");
                HeifSpanReader profileTypeReader = new(profileTypeBuffer, "color information");
                Heif4CharCode profileType = (Heif4CharCode)profileTypeReader.ReadUInt32();
                if (profileType is Heif4CharCode.RICC or Heif4CharCode.Prof)
                {
                    // The ICC profile changes the decoded colors also when SkipMetadata is true. The parser reads the profile from the stream without a
                    // separate box buffer. If the mode ignores a malformed ancillary profile, the property still occupies its ipco index. Thus the
                    // essential-association rules still treat the property as a known property.
                    IccProfile? iccProfile = HeifPropertyParser.ParseIccProfile(stream, propertyLength - 4, this.Options.SegmentIntegrityHandling);
                    properties.Add(new KeyValuePair<Heif4CharCode, object>(Heif4CharCode.Colr, (object?)iccProfile ?? IgnoredProperty));
                    stream.Position = propertyEnd;
                    continue;
                }

                stream.Position -= 4;
            }

            using IMemoryOwner<byte> payload = this.boxReader.ReadPayload(stream, propertyLength);
            properties.Add(new KeyValuePair<Heif4CharCode, object>(propertyType, this.ParseProperty(propertyType, payload.GetSpan())));
        }
    }

    /// <summary>
    /// Parses one item property.
    /// </summary>
    /// <remarks>
    /// A malformed property that changes the decoded pixels is a structural error. The decoder throws for this error before the image exists. The decoder
    /// reports a malformed metadata property as an ancillary error. If the mode ignores this error, <see cref="IgnoredProperty"/> keeps the ipco index of the
    /// property.
    /// </remarks>
    /// <param name="propertyType">The property box type.</param>
    /// <param name="data">The property payload.</param>
    /// <returns>The parsed value, <see cref="IgnoredProperty"/>, or <see cref="UnknownProperty"/>.</returns>
    private object ParseProperty(Heif4CharCode propertyType, Span<byte> data)
    {
        SegmentIntegrityHandling handling = this.Options.SegmentIntegrityHandling;
        switch (propertyType)
        {
            case Heif4CharCode.Ispe:
                // ISO/IEC 23008-12 defines only version 0 of this property. The unsigned display width and height follow the full-box header.
                HeifSpanReader extentsReader = new(data, "image spatial extents");
                extentsReader.ReadFullBoxHeader(0, HeifSpanReader.AnyFlags, out _);
                uint width = extentsReader.ReadUInt32();
                uint height = extentsReader.ReadUInt32();
                extentsReader.EnsureNotTruncated();
                if (width is 0 or > int.MaxValue || height is 0 or > int.MaxValue)
                {
                    throw new InvalidImageContentException("The image spatial extents property has invalid dimensions.");
                }

                return new Size((int)width, (int)height);
            case Heif4CharCode.Pixi:
                return (object?)HeifPropertyParser.ParsePixelInformation(data, handling) ?? IgnoredProperty;
            case Heif4CharCode.AuxC:
                // The aux_type field is a required null-terminated string. The remaining bytes are the payload of the registered auxiliary subtype. The decoder
                // does not need this payload to identify an alpha image plane.
                HeifSpanReader auxiliaryReader = new(data, "auxiliary type");
                auxiliaryReader.ReadFullBoxHeader(0, HeifSpanReader.AnyFlags, out _);
                string auxiliaryType = auxiliaryReader.ReadCString();
                auxiliaryReader.EnsureNotTruncated();
                return auxiliaryType;
            case Heif4CharCode.Colr:
                HeifSpanReader colorReader = new(data, "color information");
                Heif4CharCode profileType = (Heif4CharCode)colorReader.ReadUInt32();
                ReadOnlySpan<byte> colorData = colorReader.RemainingData;
                colorReader.EnsureNotTruncated();
                return profileType == Heif4CharCode.Nclx ? HeifPropertyParser.ParseCicpProfile(colorData) : UnknownProperty;
            case Heif4CharCode.Pasp:
                return (object?)HeifPropertyParser.ParsePixelAspectRatio(data, handling) ?? IgnoredProperty;
            case Heif4CharCode.Clli:
                return (object?)HeifPropertyParser.ParseContentLightLevel(data, handling) ?? IgnoredProperty;
            case Heif4CharCode.Mdcv:
                return (object?)HeifPropertyParser.ParseMasteringDisplayColorVolume(data, handling) ?? IgnoredProperty;
            case Heif4CharCode.Cclv:
                return (object?)HeifPropertyParser.ParseContentColorVolume(data, handling) ?? IgnoredProperty;
            case Heif4CharCode.Amve:
                return (object?)HeifPropertyParser.ParseAmbientViewingEnvironment(data, handling) ?? IgnoredProperty;
            case Heif4CharCode.Reve:
                return (object?)HeifPropertyParser.ParseReferenceViewingEnvironment(data, handling) ?? IgnoredProperty;
            case Heif4CharCode.Ndwt:
                return (object?)HeifPropertyParser.ParseNominalDiffuseWhite(data, handling) ?? IgnoredProperty;
            case Heif4CharCode.Av1C:
                return new Av1CodecConfiguration(data, this.Options);
            case Heif4CharCode.A1op:
                return HeifPropertyParser.ParseAv1OperatingPointSelector(data);
            case Heif4CharCode.Lsel:
                return HeifPropertyParser.ParseAv1LayerSelector(data);
            case Heif4CharCode.A1lx:
                return HeifPropertyParser.ParseAv1LayeredImageIndex(data);
            case Heif4CharCode.Clap:
                return HeifPropertyParser.ParseCleanAperture(data);
            case Heif4CharCode.Irot:
                return HeifPropertyParser.ParseRotation(data);
            case Heif4CharCode.Imir:
                return HeifPropertyParser.ParseMirrorAxis(data);
            case Heif4CharCode.Altt:
            case Heif4CharCode.Iscl:
            case Heif4CharCode.Rloc:
            case Heif4CharCode.Udes:
                // These properties are registered image properties, not unknown boxes. The parser keeps their ipco indices, so that the identification of the
                // container stays available. The image stage that owns each property handles its value.
                return IgnoredProperty;
            default:
                // An unknown property still occupies an ipco index. The unknown property causes an error only when its association has the essential flag.
                return UnknownProperty;
        }
    }

    /// <summary>
    /// Applies one-based property indices and essential flags to their referenced items.
    /// </summary>
    /// <param name="payload">The property-association full-box payload.</param>
    /// <param name="properties">The properties in the order of the association indices.</param>
    private void ParsePropertyAssociation(ReadOnlySpan<byte> payload, List<KeyValuePair<Heif4CharCode, object>> properties)
    {
        // Each association stores the essential flag in its highest bit and the one-based property index in the
        // other bits. The byte form has seven index bits, and the ushort form has fifteen.
        const uint smallPropertyIndexMask = 0x7FU;
        const uint smallEssentialMask = 0x80U;
        const uint largePropertyIndexMask = 0x7FFFU;
        const uint largeEssentialMask = 0x8000U;

        SegmentIntegrityHandling handling = this.Options.SegmentIntegrityHandling;
        HeifSpanReader reader = new(payload, "item property association");

        // Version 1 widens the item identifiers to 32 bits, and flag bit 0 selects the 16-bit association form.
        byte version = reader.ReadFullBoxHeader(1, HeifSpanReader.AnyFlags, out uint flags);
        bool largeItemIds = version == 1;
        bool largePropertyIndex = (flags & 1) != 0;
        uint propertyIndexMask = largePropertyIndex ? largePropertyIndexMask : smallPropertyIndexMask;
        uint essentialMask = largePropertyIndex ? largeEssentialMask : smallEssentialMask;

        // Each entry stores at least the item identifier and the association count.
        uint entryCount = reader.ReadEntryCount(4, (largeItemIds ? 4 : 2) + 1);
        for (uint entryIndex = 0; entryIndex < entryCount; entryIndex++)
        {
            // The associations of earlier entries can use the bytes that the count validation reserved for this entry. A truncated read gives the identifier 0.
            // Thus EnsureNotTruncated reports the truncation before the item lookup.
            uint itemId = reader.ReadU16Or32(largeItemIds);
            reader.EnsureNotTruncated();
            HeifItem item = this.FindItemById(itemId)
                ?? throw new InvalidImageContentException($"Item property association references unknown item ID {itemId}.");

            int associationCount = reader.ReadUInt8();
            for (int i = 0; i < associationCount; i++)
            {
                uint association = largePropertyIndex ? reader.ReadUInt16() : reader.ReadUInt8();
                uint propertyIndex = association & propertyIndexMask;
                bool essential = (association & essentialMask) != 0;
                if (propertyIndex == 0)
                {
                    if (essential)
                    {
                        throw new InvalidImageContentException($"Item {itemId} associates essential property index 0.");
                    }

                    continue;
                }

                propertyIndex--;
                if (propertyIndex >= properties.Count)
                {
                    throw new InvalidImageContentException(
                        $"Item {itemId} references property index {propertyIndex + 1}, but only {properties.Count} properties exist.");
                }

                KeyValuePair<Heif4CharCode, object> property = properties[(int)propertyIndex];
                if (essential && ReferenceEquals(property.Value, UnknownProperty))
                {
                    throw new InvalidImageContentException($"Item {itemId} associates unknown essential property '{property.Key}'.");
                }

                AssociationRule rule = GetAssociationRule(property.Key);
                if (essential ? (rule & AssociationRule.NotEssential) != 0 : (rule & AssociationRule.Essential) != 0)
                {
                    handling.ThrowOrIgnoreImageDataError($"Item {itemId} associates property '{property.Key}' with an invalid essential flag.");
                    continue;
                }

                if ((rule & AssociationRule.Av1Only) != 0 && item.Type != Heif4CharCode.Av01)
                {
                    handling.ThrowOrIgnoreImageDataError($"Item {itemId} associates AV1 property '{property.Key}' with non-AV1 item type '{item.Type}'.");
                    continue;
                }

                switch (property.Value)
                {
                    case Size extent:
                        item.SetExtent(extent);
                        break;
                    case HeifPixelAspectRatio pixelAspectRatio:
                        if (HeifPropertyParser.AssociateOnce(item.PixelAspectRatio, handling, "item", itemId, property.Key, true))
                        {
                            item.PixelAspectRatio = pixelAspectRatio;
                        }

                        break;
                    case byte[] channelBitDepths:
                        if (HeifPropertyParser.AssociateOnce(item.ChannelBitDepths, handling, "item", itemId, property.Key, false))
                        {
                            item.ChannelCount = channelBitDepths.Length;
                            item.ChannelBitDepths = channelBitDepths;
                        }

                        break;
                    case Av1CodecConfiguration av1CodecConfiguration:
                        if (HeifPropertyParser.AssociateOnce(item.Av1CodecConfiguration, handling, "item", itemId, property.Key, false))
                        {
                            item.Av1CodecConfiguration = av1CodecConfiguration;
                        }

                        break;
                    case Av1OperatingPointSelector operatingPointSelector:
                        if (HeifPropertyParser.AssociateOnce(item.Av1OperatingPointSelector, handling, "item", itemId, property.Key, false))
                        {
                            item.Av1OperatingPointSelector = operatingPointSelector;
                        }

                        break;
                    case Av1LayerSelector layerSelector:
                        if (HeifPropertyParser.AssociateOnce(item.Av1LayerSelector, handling, "item", itemId, property.Key, false))
                        {
                            item.Av1LayerSelector = layerSelector;
                        }

                        break;
                    case Av1LayeredImageIndex layeredImageIndex:
                        if (HeifPropertyParser.AssociateOnce(item.Av1LayeredImageIndex, handling, "item", itemId, property.Key, false))
                        {
                            item.Av1LayeredImageIndex = layeredImageIndex;
                        }

                        break;
                    case string auxiliaryType:
                        if (HeifPropertyParser.AssociateOnce(item.AuxiliaryType, handling, "item", itemId, property.Key, false))
                        {
                            item.AuxiliaryType = auxiliaryType;
                        }

                        break;
                    case IccProfile iccProfile:
                        if (HeifPropertyParser.AssociateOnce(item.IccProfile, handling, "item", itemId, property.Key, true))
                        {
                            item.IccProfile = iccProfile;
                        }

                        break;
                    case CicpProfile cicpProfile:
                        if (HeifPropertyParser.AssociateOnce(item.CicpProfile, handling, "item", itemId, property.Key, false))
                        {
                            item.CicpProfile = cicpProfile;
                        }

                        break;
                    case HeifContentLightLevel contentLightLevel:
                        if (HeifPropertyParser.AssociateOnce(item.ContentLightLevel, handling, "item", itemId, property.Key, true))
                        {
                            item.ContentLightLevel = contentLightLevel;
                        }

                        break;
                    case HeifMasteringDisplayColorVolume masteringDisplayColorVolume:
                        if (HeifPropertyParser.AssociateOnce(item.MasteringDisplayColorVolume, handling, "item", itemId, property.Key, true))
                        {
                            item.MasteringDisplayColorVolume = masteringDisplayColorVolume;
                        }

                        break;
                    case HeifContentColorVolume contentColorVolume:
                        if (HeifPropertyParser.AssociateOnce(item.ContentColorVolume, handling, "item", itemId, property.Key, true))
                        {
                            item.ContentColorVolume = contentColorVolume;
                        }

                        break;
                    case HeifAmbientViewingEnvironment ambientViewingEnvironment:
                        if (HeifPropertyParser.AssociateOnce(item.AmbientViewingEnvironment, handling, "item", itemId, property.Key, true))
                        {
                            item.AmbientViewingEnvironment = ambientViewingEnvironment;
                        }

                        break;
                    case HeifReferenceViewingEnvironment referenceViewingEnvironment:
                        if (HeifPropertyParser.AssociateOnce(item.ReferenceViewingEnvironment, handling, "item", itemId, property.Key, true))
                        {
                            item.ReferenceViewingEnvironment = referenceViewingEnvironment;
                        }

                        break;
                    case HeifNominalDiffuseWhite nominalDiffuseWhite:
                        if (HeifPropertyParser.AssociateOnce(item.NominalDiffuseWhite, handling, "item", itemId, property.Key, true))
                        {
                            item.NominalDiffuseWhite = nominalDiffuseWhite;
                        }

                        break;
                    case HeifCleanAperture cleanAperture:
                        if (HeifPropertyParser.AssociateOnce(item.CleanAperture, handling, "item", itemId, property.Key, false))
                        {
                            item.CleanAperture = cleanAperture;
                        }

                        break;
                    case byte rotationAngle when property.Key == Heif4CharCode.Irot:
                        if (HeifPropertyParser.AssociateOnce(item.RotationAngle, handling, "item", itemId, property.Key, false))
                        {
                            item.RotationAngle = rotationAngle;
                        }

                        break;
                    case byte mirrorAxis:
                        if (HeifPropertyParser.AssociateOnce(item.MirrorAxis, handling, "item", itemId, property.Key, false))
                        {
                            item.MirrorAxis = mirrorAxis;
                        }

                        break;
                }
            }
        }

        reader.EnsureEnd();
    }

    /// <summary>
    /// Gets the association rule of an item property type. This switch is the only table of the essential-flag rules.
    /// </summary>
    /// <param name="propertyType">The property box type.</param>
    /// <returns>The association rule.</returns>
    private static AssociationRule GetAssociationRule(Heif4CharCode propertyType) => propertyType switch
    {
        // AVIF 1.1 section 2.3.2.1.1 requires that a1op is essential. HEIF section 6.5.11.1 has the same requirement for lsel. A reader that ignores either
        // selector decodes a different image.
        Heif4CharCode.A1op or Heif4CharCode.Lsel => AssociationRule.Essential | AssociationRule.Av1Only,

        // AVIF 1.1 section 2.3.2.3.2 requires that a1lx is not essential. A decoder can read the complete item payload and ignore the optional layer
        // boundaries.
        Heif4CharCode.A1lx => AssociationRule.NotEssential | AssociationRule.Av1Only,

        // A transformative property changes the presented pixels. Thus a reader that ignores the property presents a different image.
        Heif4CharCode.Clap or Heif4CharCode.Irot or Heif4CharCode.Imir => AssociationRule.Essential,
        Heif4CharCode.Av1C => AssociationRule.Av1Only,
        Heif4CharCode.Pasp
            or Heif4CharCode.Clli
            or Heif4CharCode.Mdcv
            or Heif4CharCode.Cclv
            or Heif4CharCode.Amve
            or Heif4CharCode.Reve
            or Heif4CharCode.Ndwt => AssociationRule.Metadata,
        _ => AssociationRule.None
    };

    /// <summary>
    /// Validates the extents of one planned image item. The method reports a truncated item as damaged image data.
    /// </summary>
    /// <param name="source">The item data source.</param>
    /// <param name="item">The planned image item.</param>
    private void ValidateItemExtents(HeifItemDataSource source, HeifItem item)
    {
        if (!source.ValidateExtents(item))
        {
            this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError($"The data of item {item.Id} is truncated or empty.");
        }
    }

    /// <summary>
    /// Reads one item that <see cref="HeifItemDataSource.ValidateExtents"/> validated.
    /// </summary>
    /// <param name="stream">The complete seekable HEIF container stream.</param>
    /// <param name="source">The item data source.</param>
    /// <param name="item">The validated item.</param>
    /// <returns>An owner that holds the contiguous item payload.</returns>
    private IMemoryOwner<byte> ReadItemData(BufferedReadStream stream, HeifItemDataSource source, HeifItem item)
    {
        // The method reads only the selected item. The caller disposes the owner when the codec or metadata consumer
        // finishes.
        IMemoryOwner<byte> itemMemory = this.configuration.MemoryAllocator.Allocate<byte>(item.DataLength);
        try
        {
            source.Read(stream, item, itemMemory.GetSpan()[..item.DataLength]);
            return itemMemory;
        }
        catch
        {
            itemMemory.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Resolves item extents and reconstructs the primary image.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel format.</typeparam>
    /// <param name="stream">The complete seekable HEIF container stream.</param>
    /// <param name="cancellationToken">The token that cancels the item assembly and the payload decode.</param>
    /// <returns>The image reconstructed from the selected item.</returns>
    private Image<TPixel> DecodePrimaryItem<TPixel>(BufferedReadStream stream, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        HeifDecodePlan plan = HeifItemGraphValidator.Validate(this.items, this.itemLinks, this.primaryItem, this.Options.SegmentIntegrityHandling);

        // The decoder validates the extents of each planned item before it allocates the image or an item buffer.
        HeifItemDataSource source = new(this.fileStartOffset, stream.Length, this.itemData);
        this.ValidateItemExtents(source, plan.Primary);
        foreach (HeifItem tile in plan.Tiles)
        {
            this.ValidateItemExtents(source, tile);
        }

        if (plan.AlphaItem is not null && plan.AlphaItem != plan.Primary)
        {
            this.ValidateItemExtents(source, plan.AlphaItem);
        }

        foreach (HeifItem alphaTile in plan.AlphaTiles)
        {
            this.ValidateItemExtents(source, alphaTile);
        }

        IMemoryOwner<byte> ItemDataReader(HeifItem item) => this.ReadItemData(stream, source, item);

        // The alpha grid descriptor is item data. Thus the validator validates the descriptor after the extents of the alpha item.
        plan = HeifItemGraphValidator.ValidateAlphaGrid(plan, ItemDataReader, this.Options.SegmentIntegrityHandling);

        Image<TPixel> image = this.DecodeImageItem<TPixel>(plan, ItemDataReader, cancellationToken);

        try
        {
            this.ApplyItemColorMetadata(image.Metadata, plan);
            if (!this.Options.SkipMetadata)
            {
                ApplyItemHdrMetadata(image.Metadata, plan);
                this.ApplyAssociatedMetadata(stream, source, image.Metadata, plan.Primary);
            }

            if (!this.Options.SkipMetadata && IccProfile.TryGetIccProfileForColorConversion(image.Metadata.IccProfile, out _))
            {
                // ICC conversion has already produced full-range sRGB pixels. The source YUV matrix and
                // transfer description must not be reapplied when those pixels are written to another format.
                image.Metadata.CicpProfile = new CicpProfile(1, 13, 0, true);
                image.Frames.RootFrame.Metadata.CicpProfile = image.Metadata.CicpProfile;
            }
            else if (!this.Options.SkipMetadata && image.Metadata.CicpProfile is not null)
            {
                // Without an ICC transform, packed pixels retain the source primaries and transfer function.
                // Keep the primaries and transfer description, but do not label RGB as encoded YUV.
                CicpProfile sourceProfile = image.Metadata.CicpProfile;
                image.Metadata.CicpProfile = new CicpProfile(
                    (byte)sourceProfile.ColorPrimaries,
                    (byte)sourceProfile.TransferCharacteristics,
                    (byte)CicpMatrixCoefficients.Identity,
                    true);

                image.Frames.RootFrame.Metadata.CicpProfile = image.Metadata.CicpProfile;
            }

            HeifMetadata meta = image.Metadata.GetHeifMetadata();
            if (this.Options.SkipMetadata)
            {
                // AV1 still needs encoded color information to reconstruct pixels. Omit its exposed CICP/HDR
                // metadata, but retain ICC for restoring the original color space during encoding.
                image.Metadata.CicpProfile = null;
                image.Frames.RootFrame.Metadata.CicpProfile = null;
                meta.ContentLightLevel = null;
                meta.MasteringDisplayColorVolume = null;
                meta.ContentColorVolume = null;
                meta.AmbientViewingEnvironment = null;
                meta.ReferenceViewingEnvironment = null;
                meta.NominalDiffuseWhite = null;
            }

            return image;
        }
        catch
        {
            // Ownership transfers to the caller only after every auxiliary plane has been composed successfully.
            image.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Applies the ICC profile of the item to the metadata. If SkipMetadata is <see langword="false"/>, the method also applies the CICP profile.
    /// </summary>
    /// <param name="metadata">The image metadata receiving the profiles.</param>
    /// <param name="plan">The decode plan of the primary image.</param>
    private void ApplyItemColorMetadata(ImageMetadata metadata, HeifDecodePlan plan)
    {
        // The derived grid or its coded tile items can have color properties. The grid has priority. The method uses the first tile only when the grid does not
        // have the profile. For a coded primary item, the coded item is the primary item. The item model is private to the decoder and does not change after
        // property resolution. Thus the metadata uses the same profile instances and does not copy their storage.
        IccProfile? iccProfile = plan.Primary.IccProfile ?? plan.CodedItem.IccProfile;
        if (iccProfile is not null)
        {
            metadata.IccProfile = iccProfile;
        }

        CicpProfile? cicpProfile = plan.Primary.CicpProfile ?? plan.CodedItem.CicpProfile;
        if (!this.Options.SkipMetadata && cicpProfile is not null)
        {
            metadata.CicpProfile = cicpProfile;
        }
    }

    /// <summary>
    /// Applies the high-dynamic-range metadata and the pixel aspect ratio associated with a presented still-image item.
    /// </summary>
    /// <param name="metadata">The image metadata receiving the high-dynamic-range description and the aspect ratio.</param>
    /// <param name="plan">The decode plan of the primary image.</param>
    private static void ApplyItemHdrMetadata(ImageMetadata metadata, HeifDecodePlan plan)
    {
        // A derived grid can describe the complete presentation. Fall back to the first coded tile only when the
        // grid does not carry its own value, matching the precedence used for its color-profile properties.
        HeifItem imageItem = plan.Primary;
        HeifItem codedItem = plan.CodedItem;
        HeifContentLightLevel? contentLightLevel = imageItem.ContentLightLevel ?? codedItem.ContentLightLevel;
        if (contentLightLevel is not null)
        {
            metadata.GetHeifMetadata().ContentLightLevel = contentLightLevel;
        }

        HeifMasteringDisplayColorVolume? masteringDisplayColorVolume = imageItem.MasteringDisplayColorVolume
            ?? codedItem.MasteringDisplayColorVolume;

        if (masteringDisplayColorVolume is not null)
        {
            metadata.GetHeifMetadata().MasteringDisplayColorVolume = masteringDisplayColorVolume;
        }

        HeifContentColorVolume? contentColorVolume = imageItem.ContentColorVolume ?? codedItem.ContentColorVolume;
        if (contentColorVolume is not null)
        {
            metadata.GetHeifMetadata().ContentColorVolume = contentColorVolume;
        }

        HeifAmbientViewingEnvironment? ambientViewingEnvironment = imageItem.AmbientViewingEnvironment
            ?? codedItem.AmbientViewingEnvironment;

        if (ambientViewingEnvironment is not null)
        {
            metadata.GetHeifMetadata().AmbientViewingEnvironment = ambientViewingEnvironment;
        }

        HeifReferenceViewingEnvironment? referenceViewingEnvironment = imageItem.ReferenceViewingEnvironment
            ?? codedItem.ReferenceViewingEnvironment;

        if (referenceViewingEnvironment is not null)
        {
            metadata.GetHeifMetadata().ReferenceViewingEnvironment = referenceViewingEnvironment;
        }

        HeifNominalDiffuseWhite? nominalDiffuseWhite = imageItem.NominalDiffuseWhite ?? codedItem.NominalDiffuseWhite;
        if (nominalDiffuseWhite is not null)
        {
            metadata.GetHeifMetadata().NominalDiffuseWhite = nominalDiffuseWhite;
        }

        ApplyPixelAspectRatioMetadata(metadata, imageItem.PixelAspectRatio ?? codedItem.PixelAspectRatio, imageItem.RotationAngle);
    }

    /// <summary>
    /// Applies registered pixel spacing to ImageSharp's aspect-ratio resolution metadata.
    /// </summary>
    /// <param name="metadata">The image metadata receiving the aspect ratio.</param>
    /// <param name="pixelAspectRatio">The optional registered horizontal and vertical spacing.</param>
    /// <param name="rotationAngle">The optional counter-clockwise quarter-turn count.</param>
    private static void ApplyPixelAspectRatioMetadata(
        ImageMetadata metadata,
        HeifPixelAspectRatio? pixelAspectRatio,
        byte? rotationAngle)
    {
        if (pixelAspectRatio is null)
        {
            return;
        }

        // ImageMetadata expresses pixel width:height as vertical-density:horizontal-density. A quarter-turn exchanges
        // the displayed pixel axes, so it also exchanges which spacing value supplies each density.
        bool swapsAxes = rotationAngle is 1 or 3;
        metadata.HorizontalResolution = swapsAxes
            ? pixelAspectRatio.HorizontalSpacing
            : pixelAspectRatio.VerticalSpacing;

        metadata.VerticalResolution = swapsAxes
            ? pixelAspectRatio.VerticalSpacing
            : pixelAspectRatio.HorizontalSpacing;

        metadata.ResolutionUnits = PixelResolutionUnit.AspectRatio;
    }

    /// <summary>
    /// Applies Exif and XMP metadata items that describe a decoded color image item.
    /// </summary>
    /// <remarks>
    /// Metadata items are ancillary data. The method reports an unknown source item and a truncated item as ancillary errors. If the mode ignores the error,
    /// the method skips the item.
    /// </remarks>
    /// <param name="stream">The complete seekable HEIF container stream.</param>
    /// <param name="source">The item data source.</param>
    /// <param name="metadata">The decoded image metadata receiving the profiles.</param>
    /// <param name="colorItem">The color image item described by the metadata links.</param>
    private void ApplyAssociatedMetadata(BufferedReadStream stream, HeifItemDataSource source, ImageMetadata metadata, HeifItem colorItem)
    {
        SegmentIntegrityHandling handling = this.Options.SegmentIntegrityHandling;
        foreach (HeifItemLink link in this.itemLinks)
        {
            if (link.Type != Heif4CharCode.Cdsc || !link.DestinationIds.Contains(colorItem.Id))
            {
                continue;
            }

            HeifItem? metadataItem = this.FindItemById(link.SourceId);
            if (metadataItem is null)
            {
                handling.ThrowOrIgnoreAncillaryError($"The descriptive item reference names undeclared item {link.SourceId}.");
                continue;
            }

            bool isExif = metadataItem.Type == Heif4CharCode.Exif;
            bool isXmp = metadataItem.Type == Heif4CharCode.Mime
                && string.Equals(metadataItem.ContentType, "application/rdf+xml", StringComparison.Ordinal);

            if (!isExif && !isXmp)
            {
                continue;
            }

            if (!source.ValidateExtents(metadataItem))
            {
                handling.ThrowOrIgnoreAncillaryError($"The data of metadata item {metadataItem.Id} is truncated or empty.");
                continue;
            }

            using IMemoryOwner<byte> itemMemory = this.ReadItemData(stream, source, metadataItem);
            if (isExif)
            {
                this.ApplyExifProfile(metadata, itemMemory.GetSpan());
            }
            else
            {
                metadata.XmpProfile = XmpReader.ReadProfile(itemMemory.GetSpan());
            }
        }
    }

    /// <summary>
    /// Validates the HEIF Exif TIFF-header offset and applies the contained TIFF payload.
    /// </summary>
    /// <param name="metadata">The image metadata receiving the Exif profile.</param>
    /// <param name="itemData">The complete HEIF Exif item including its four-byte offset field.</param>
    private void ApplyExifProfile(ImageMetadata metadata, ReadOnlySpan<byte> itemData)
    {
        if (itemData.Length < 8)
        {
            this.Options.SegmentIntegrityHandling.ThrowOrIgnoreAncillaryError("The HEIF Exif item is truncated.");
            return;
        }

        uint declaredTiffHeaderOffset = BinaryPrimitives.ReadUInt32BigEndian(itemData);
        ReadOnlySpan<byte> exifData = itemData[4..];
        int actualTiffHeaderOffset = -1;

        // Annex A stores the offset to the first TIFF byte-order marker. Find the first valid TIFF signature, and
        // require the declared offset to identify that same header.
        for (int i = 0; i <= exifData.Length - 4; i++)
        {
            bool isBigEndianTiff = exifData[i] == (byte)'M' &&
                exifData[i + 1] == (byte)'M' &&
                exifData[i + 2] == 0 &&
                exifData[i + 3] == 42;

            bool isLittleEndianTiff = exifData[i] == (byte)'I' &&
                exifData[i + 1] == (byte)'I' &&
                exifData[i + 2] == 42 &&
                exifData[i + 3] == 0;

            if (isBigEndianTiff || isLittleEndianTiff)
            {
                actualTiffHeaderOffset = i;
                break;
            }
        }

        if (actualTiffHeaderOffset < 0 || declaredTiffHeaderOffset != (uint)actualTiffHeaderOffset)
        {
            this.Options.SegmentIntegrityHandling.ThrowOrIgnoreAncillaryError("The HEIF Exif item has an invalid TIFF-header offset.");
            return;
        }

        metadata.TrySetExifProfile(ExifReader.ReadProfile(exifData[actualTiffHeaderOffset..]));
    }

    /// <summary>
    /// Decodes the primary image item from its assembled payload.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel format.</typeparam>
    /// <param name="plan">The decode plan of the primary image.</param>
    /// <param name="itemDataReader">The function that reads one validated item payload.</param>
    /// <param name="cancellationToken">The token that cancels the payload decode.</param>
    /// <returns>The decoded image.</returns>
    private Image<TPixel> DecodeImageItem<TPixel>(HeifDecodePlan plan, Func<HeifItem, IMemoryOwner<byte>> itemDataReader, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // The item graph validator validated that a grid has AV1 tiles and that a coded primary item is an AV1 item. A coded primary item creates the
        // image. Thus, no image exists before its payload decodes, and damage in that payload throws in each mode.
        HeifItem item = plan.Primary;
        IHeifItemDecoder<TPixel> decoder = item.Type == Heif4CharCode.Grid
            ? new GridHeifItemDecoder<TPixel>(plan.Tiles, plan.TilePremultiplied, itemDataReader)
            : new Av1HeifItemDecoder<TPixel>(false);

        using IMemoryOwner<byte> itemMemory = itemDataReader(item);
        Size extent = item.Extent;
        Rectangle sourceRectangle = plan.SourceRectangle;
        HeifPixelTransform transform = new(item.RotationAngle ?? 0, item.MirrorAxis);
        Size outputSize = transform.GetDestinationSize(sourceRectangle.Size);

        // The item decoder allocates the output image through the destination callback only after it has validated
        // the spatial extents against the payload. Metadata is collected first and then owned by that image.
        ImageMetadata metadata = new();
        Image<TPixel>? image = null;

        try
        {
            this.ApplyItemColorMetadata(metadata, plan);

            // The native decoder transforms before packing TPixel. Preserve retains the original profile,
            // while Convert removes it in ImageDecoder; neither requires a second pass over image pixels.
            IccProfile.TryGetIccProfileForColorConversion(metadata.IccProfile, out IccProfile? profile);

            // The item graph validator owns the alpha rules. The AV1 code reports a damaged alpha payload. In the
            // IgnoreImageData mode, the decode then gives no alpha plane, and the image is opaque.
            using Av1FrameBuffer<byte>? alphaFrame = this.DecodeAlphaPlane<TPixel>(plan, itemDataReader, cancellationToken);
            decoder.DecodeItemData(
                this.payloadOptions,
                this.chromaUpsampling,
                item,
                itemMemory.GetSpan(),
                item.CicpProfile,
                profile,
                alphaFrame,
                extent,
                sourceRectangle,
                alphaFrame is not null && plan.Premultiplied,
                sourceRectangle,
                transform,
                () =>
                {
                    image = new Image<TPixel>(this.configuration, outputSize.Width, outputSize.Height, metadata);
                    return image.Frames.RootFrame.PixelBuffer.GetRegion(new Rectangle(Point.Empty, outputSize));
                },
                metadata,
                cancellationToken);

            // Every item decoder requests its destination once on success, so the image exists here.
            image!.Metadata.GetHeifMetadata().HasAlpha = alphaFrame is not null;
            image.Frames.RootFrame.Metadata.CicpProfile = image.Metadata.CicpProfile;
            return image;
        }
        catch
        {
            image?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Gets the dimensions of the primary image after the decoder applies its clean-aperture and rotation properties.
    /// </summary>
    /// <param name="plan">The decode plan of the primary image.</param>
    /// <returns>The item dimensions after the optional crop and quarter-turn rotation.</returns>
    private static Size GetPresentationExtent(HeifDecodePlan plan)
    {
        Size extent = plan.SourceRectangle.Size;
        byte? rotationAngle = plan.Primary.RotationAngle;
        return rotationAngle is not null && (rotationAngle.Value & 1) != 0
            ? new Size(extent.Height, extent.Width)
            : extent;
    }

    /// <summary>
    /// Gets the dimensions of a sequence sample after its clean-aperture and rotation properties are applied.
    /// </summary>
    /// <param name="track">The selected color track whose samples share the presentation properties.</param>
    /// <returns>The displayed frame dimensions.</returns>
    private static Size GetSequencePresentationExtent(HeifSequenceTrack track)
    {
        Size codedExtent = new(track.CodedWidth, track.CodedHeight);
        Size extent = track.CleanAperture is not null ? track.CleanAperture.Value.ToRectangle(codedExtent).Size : codedExtent;
        return track.RotationAngle is not null && (track.RotationAngle.Value & 1) != 0
            ? new Size(extent.Height, extent.Width)
            : extent;
    }

    /// <summary>
    /// Decodes and composes the alpha plane of the decode plan.
    /// </summary>
    /// <typeparam name="TPixel">The destination color pixel type.</typeparam>
    /// <param name="plan">The decode plan of the primary image.</param>
    /// <param name="itemDataReader">The function that reads one validated item payload.</param>
    /// <param name="cancellationToken">The token that cancels the auxiliary payload decode.</param>
    /// <returns>
    /// The auxiliary plane that the caller owns. <see langword="null"/> if the image has no alpha plane, or if the alpha payload gives no frame because of
    /// damage that the mode ignores.
    /// </returns>
    private Av1FrameBuffer<byte>? DecodeAlphaPlane<TPixel>(
        HeifDecodePlan plan,
        Func<HeifItem, IMemoryOwner<byte>> itemDataReader,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        HeifItem? alphaItem = plan.AlphaItem;
        if (alphaItem is null)
        {
            return null;
        }

        // An alpha grid uses the row and column layout of the grid descriptor in the payload of the alpha item. For alpha tiles of a color grid, the alpha item
        // is the color grid. The grid decoder does not use tile premultiplication for an alpha plane. The color item gives the image without the alpha plane.
        // Thus the alpha decoder decodes for an existing image.
        IHeifAlphaItemDecoder decoder = plan.AlphaTiles.Length > 0
            ? new GridHeifItemDecoder<TPixel>(plan.AlphaTiles, [], itemDataReader)
            : new Av1HeifItemDecoder<TPixel>(true);

        using IMemoryOwner<byte> itemMemory = itemDataReader(alphaItem);
        return decoder.DecodeAlphaItemData(this.payloadOptions, alphaItem, itemMemory.GetSpan(), cancellationToken);
    }

    /// <summary>
    /// Finds an item by its file-defined identifier.
    /// </summary>
    /// <param name="itemId">The item identifier.</param>
    /// <returns>The matching item, or <see langword="null"/> when it has not been declared.</returns>
    private HeifItem? FindItemById(uint itemId)
        => this.items.GetValueOrDefault(itemId);
}
