// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Components.Alpha;
using SixLabors.ImageSharp.Formats.Heif.Hevc;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Xmp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

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
    /// The fixed scratch buffer reused for all item-container box headers in this decode operation.
    /// </summary>
    private readonly byte[] boxHeaderScratch;

    /// <summary>
    /// The item identifier selected by the primary-item box.
    /// </summary>
    private uint primaryItem;

    /// <summary>
    /// The item declarations parsed from the item-information box.
    /// </summary>
    private readonly List<HeifItem> items;

    /// <summary>
    /// The typed relationships parsed from the item-reference box.
    /// </summary>
    private readonly List<HeifItemLink> itemLinks;

    /// <summary>
    /// The absolute stream offset of the item-data box payload, or <c>-1</c> when no item-data box exists.
    /// </summary>
    private long itemDataOffset = -1;

    /// <summary>
    /// The number of bytes in the item-data box payload.
    /// </summary>
    private long itemDataLength;

    /// <summary>
    /// Initializes a new instance of the <see cref="HeifDecoderCore" /> class.
    /// </summary>
    /// <param name="options">The decoder options.</param>
    public HeifDecoderCore(DecoderOptions options)
        : base(options)
    {
        this.configuration = options.Configuration;

        // HEIF owns final presentation resizing and ICC conversion after item/grid composition and container-profile
        // selection. Nested codecs retain every other general policy but must not apply either operation independently.
        this.payloadOptions = options.TargetSize is null && options.ColorProfileHandling == ColorProfileHandling.Preserve
            ? options
            : new DecoderOptions
            {
                Configuration = options.Configuration,
                Sampler = options.Sampler,
                SkipMetadata = options.SkipMetadata,
                MaxFrames = options.MaxFrames,
                SegmentIntegrityHandling = options.SegmentIntegrityHandling,
                ColorProfileHandling = ColorProfileHandling.Preserve
            };

        this.metadata = new ImageMetadata();
        this.boxReader = new HeifBoxReader(this.configuration.MemoryAllocator);
        this.sequenceParser = new HeifSequenceParser(options);
        this.boxHeaderScratch = new byte[8];
        this.items = [];
        this.itemLinks = [];
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
        this.itemDataOffset = -1;
        this.itemDataLength = 0;

        // Item locations are absolute file offsets or idat-relative offsets, so payload bytes need not be adjacent to
        // the metadata box. Complete the top-level scan before resolving and decoding the primary item.
        while (stream.Position < stream.Length)
        {
            long boxLength = HeifBoxReader.ReadHeader(stream, stream.Length, this.boxHeaderScratch, out Heif4CharCode boxType, true);
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
                    // Some files have trailing zeros, skiping to EOF.
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
        this.itemDataOffset = -1;
        this.itemDataLength = 0;

        // Identification reads only the container model. Payload boxes remain skipped because dimensions and format
        // metadata come from item declarations and associated properties rather than reconstructed pixels.
        while (stream.Position < stream.Length)
        {
            long boxLength = HeifBoxReader.ReadHeader(stream, stream.Length, this.boxHeaderScratch, out Heif4CharCode boxType, true);
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

        HeifItem? item = this.FindItemById(this.primaryItem);
        if (item is null)
        {
            throw new ImageFormatException("No primary item found");
        }

        this.UpdateMetadata(this.metadata, item);

        Size presentationExtent = GetPresentationExtent(item);
        return new ImageInfo(new(presentationExtent.Width, presentationExtent.Height), this.metadata);
    }

    /// <summary>
    /// Reads and validates the leading file-type box against the image presentations supported by this decoder.
    /// </summary>
    /// <param name="stream">The container stream positioned at its first top-level box.</param>
    /// <returns>The declared supported image presentation, or <see cref="HeifFileType.Unsupported"/>.</returns>
    private HeifFileType ReadFileTypeBox(BufferedReadStream stream)
    {
        long boxLength = HeifBoxReader.ReadHeader(stream, stream.Length, this.boxHeaderScratch, out Heif4CharCode boxType, true);
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
    /// Locates and parses the single movie box of a supported HEIC or AVIF image sequence.
    /// </summary>
    /// <param name="stream">The complete container stream positioned after its file-type box.</param>
    /// <returns>The bounded selected image-sequence model.</returns>
    private HeifSequence ParseImageSequence(BufferedReadStream stream)
    {
        HeifSequence? sequence = null;
        while (stream.Position < stream.Length)
        {
            long boxLength = HeifBoxReader.ReadHeader(stream, stream.Length, this.boxHeaderScratch, out Heif4CharCode boxType, true);
            if (boxType == Heif4CharCode.Moov)
            {
                if (sequence is not null)
                {
                    throw new InvalidImageContentException("The HEIF image sequence contains more than one movie box.");
                }

                sequence = this.sequenceParser.Parse(stream, boxLength);
            }
            else
            {
                // Sequence samples use absolute file offsets, so unrelated top-level payloads never need buffering.
                HeifBoxReader.Skip(stream, boxLength);
            }
        }

        return sequence ?? throw new InvalidImageContentException("The HEIF image sequence contains no movie box.");
    }

    /// <summary>
    /// Creates image and frame metadata from a parsed HEIC or AVIF image sequence without decoding its samples.
    /// </summary>
    /// <param name="sequence">The parsed selected image sequence.</param>
    /// <returns>The identified dimensions and bounded visible-frame metadata.</returns>
    private ImageInfo IdentifyImageSequence(HeifSequence sequence)
    {
        HeifSequenceTrack colorTrack = sequence.ColorTrack;
        this.UpdateSequenceMetadata(this.metadata, sequence);
        ImageFrameMetadata[] frameMetadata = CreateSequenceFrameMetadata(colorTrack);
        this.Dimensions = GetSequencePresentationExtent(colorTrack);
        return new ImageInfo(this.Dimensions, this.metadata, frameMetadata);
    }

    /// <summary>
    /// Decodes the retained visible samples of a HEIC or AVIF image sequence into one multi-frame image.
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
        this.UpdateSequenceMetadata(this.metadata, sequence);
        ImageFrame<TPixel>[] colorFrames = this.DecodeVisibleSequenceFrames<TPixel>(
            stream,
            colorTrack,
            cancellationToken,
            out int[] sampleIndices);

        Image<TPixel>? image = null;
        try
        {
            // Each codec frame owns its pixel buffer. The multi-frame image adopts those buffers directly instead
            // of cloning a complete decoded frame on every append.
            image = new Image<TPixel>(this.configuration, this.metadata, colorFrames);
            HeifSequenceTrack? alphaTrack = sequence.AlphaTrack;
            if (alphaTrack is not null)
            {
                int frameIndex = 0;
                using Av1Decoder alphaDecoder = new(this.configuration);
                for (int sampleIndex = 0; sampleIndex < alphaTrack.Samples.Length; sampleIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    HeifSequenceSample alphaSample = alphaTrack.Samples[sampleIndex];
                    if (alphaSample.IsHidden ||
                        frameIndex >= colorFrames.Length ||
                        sampleIndices[frameIndex] != sampleIndex)
                    {
                        this.ExecuteImageDataSegmentAction(
                            () => this.DecodeSequenceReference(stream, alphaTrack, alphaSample, alphaDecoder));

                        continue;
                    }

                    this.ExecuteImageDataSegmentAction(
                        () => this.DecodeSequenceAlphaFrame(
                            stream,
                            alphaTrack,
                            alphaSample,
                            alphaDecoder,
                            colorFrames[frameIndex],
                            colorTrack.IsPremultiplied));

                    frameIndex++;
                }
            }

            ApplyPresentationTransforms(
                image,
                colorTrack.CleanAperture,
                colorTrack.RotationAngle,
                colorTrack.MirrorAxis);

            if (!this.Options.SkipMetadata)
            {
                image.Metadata.CicpProfile ??= image.Frames.RootFrame.Metadata.CicpProfile?.DeepClone();
                _ = this.TryConvertIccProfile(image);
            }
            else
            {
                foreach (ImageFrame<TPixel> frame in image.Frames)
                {
                    frame.Metadata.CicpProfile = null;
                }
            }

            this.Dimensions = image.Size;
            return image;
        }
        catch
        {
            if (image is not null)
            {
                image.Dispose();
            }
            else
            {
                // Ownership transfers to Image only after its constructor validates every decoded frame.
                foreach (ImageFrame<TPixel> frame in colorFrames)
                {
                    frame.Dispose();
                }
            }

            throw;
        }
    }

    /// <summary>
    /// Decodes visible samples while preserving their source indices for frame-aligned alpha lookup.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel format.</typeparam>
    /// <param name="stream">The complete seekable HEIF stream.</param>
    /// <param name="track">The selected coded-image track.</param>
    /// <param name="cancellationToken">The token used to cancel work between coded samples.</param>
    /// <param name="sampleIndices">Receives the decode-order sample index for each returned visible frame.</param>
    /// <returns>The exact array of successfully decoded visible frames.</returns>
    private ImageFrame<TPixel>[] DecodeVisibleSequenceFrames<TPixel>(
        BufferedReadStream stream,
        HeifSequenceTrack track,
        CancellationToken cancellationToken,
        out int[] sampleIndices)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int visibleFrameCount = 0;
        foreach (HeifSequenceSample sample in track.Samples)
        {
            visibleFrameCount += sample.IsHidden ? 0 : 1;
        }

        ImageFrame<TPixel>[] frames = new ImageFrame<TPixel>[visibleFrameCount];
        sampleIndices = new int[visibleFrameCount];
        int decodedFrameCount = 0;
        using Av1Decoder decoder = new(this.configuration);
        try
        {
            for (int sampleIndex = 0; sampleIndex < track.Samples.Length; sampleIndex++)
            {
                HeifSequenceSample sample = track.Samples[sampleIndex];
                if (sample.IsHidden)
                {
                    this.ExecuteImageDataSegmentAction(
                        () => this.DecodeSequenceReference(stream, track, sample, decoder));

                    continue;
                }

                cancellationToken.ThrowIfCancellationRequested();
                ImageFrame<TPixel>? frame = null;
                this.ExecuteImageDataSegmentAction(
                    () => frame = this.DecodeSequenceFrame<TPixel>(stream, track, sample, decoder));

                if (frame is null)
                {
                    continue;
                }

                frame.Metadata.GetHeifMetadata().FrameDelay = new Rational(sample.Duration, track.MediaTimescale);
                frames[decodedFrameCount] = frame;
                sampleIndices[decodedFrameCount] = sampleIndex;
                decodedFrameCount++;
            }

            if (decodedFrameCount == 0)
            {
                throw new InvalidImageContentException("The HEIF image sequence contains no decodable visible samples.");
            }

            if (decodedFrameCount != frames.Length)
            {
                // Compaction occurs only in IgnoreImageData mode after a recoverable coded-sample failure.
                Array.Resize(ref frames, decodedFrameCount);
                Array.Resize(ref sampleIndices, decodedFrameCount);
            }

            return frames;
        }
        catch
        {
            // Frames are independently allocated before the final Image adopts them. Retain ownership until this
            // method returns so a later sample failure cannot leak the successfully decoded prefix.
            for (int frameIndex = 0; frameIndex < decodedFrameCount; frameIndex++)
            {
                frames[frameIndex].Dispose();
            }

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
    /// <returns>The independently owned decoded frame.</returns>
    private ImageFrame<TPixel> DecodeSequenceFrame<TPixel>(
        BufferedReadStream stream,
        HeifSequenceTrack track,
        HeifSequenceSample sample,
        Av1Decoder decoder)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (track.CodecType != Heif4CharCode.Av01)
        {
            throw new ImageFormatException($"No decoder is available for image-sequence sample type '{track.CodecType}'.");
        }

        Av1CodecConfiguration codecConfiguration = track.Av1CodecConfiguration
            ?? throw new InvalidImageContentException("The AV1 image-sequence track has no codec configuration.");

        using IMemoryOwner<byte> sampleOwner = this.ReadSequenceSample(stream, track, sample);
        Span<byte> sampleData = sampleOwner.GetSpan()[..sample.Length];

        ImageFrame<TPixel> frame = decoder.DecodeSequenceFrame<TPixel>(
            sampleData,
            track.CicpProfile,
            codecConfiguration);

        if (frame.Width != track.CodedWidth || frame.Height != track.CodedHeight)
        {
            frame.Dispose();
            throw new InvalidImageContentException("The decoded image-sequence sample dimensions do not match its visual sample entry.");
        }

        return frame;
    }

    /// <summary>
    /// Decodes one AV1 auxiliary sample and composes its native luma plane directly into a color frame.
    /// </summary>
    /// <typeparam name="TPixel">The destination color pixel type.</typeparam>
    /// <param name="stream">The complete seekable HEIF stream.</param>
    /// <param name="track">The alpha track supplying the codec configuration and color description.</param>
    /// <param name="sample">The validated alpha sample range.</param>
    /// <param name="decoder">The decoder retaining earlier alpha-sequence references.</param>
    /// <param name="destination">The decoded color frame receiving alpha values.</param>
    /// <param name="premultiplied">Whether stored color samples must be converted to unassociated alpha.</param>
    private void DecodeSequenceAlphaFrame<TPixel>(
        BufferedReadStream stream,
        HeifSequenceTrack track,
        HeifSequenceSample sample,
        Av1Decoder decoder,
        ImageFrame<TPixel> destination,
        bool premultiplied)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (track.CodecType != Heif4CharCode.Av01)
        {
            throw new ImageFormatException($"No decoder is available for image-sequence alpha sample type '{track.CodecType}'.");
        }

        Av1CodecConfiguration codecConfiguration = track.Av1CodecConfiguration
            ?? throw new InvalidImageContentException("The AV1 alpha image-sequence track has no codec configuration.");

        if (!codecConfiguration.IsMonochrome)
        {
            throw new InvalidImageContentException("An AV1 alpha image-sequence track must be encoded as monochrome.");
        }

        using IMemoryOwner<byte> sampleOwner = this.ReadSequenceSample(stream, track, sample);
        Span<byte> sampleData = sampleOwner.GetSpan()[..sample.Length];
        decoder.DecodeSequenceAlpha(
            sampleData,
            track.CicpProfile,
            codecConfiguration,
            new Size(track.CodedWidth, track.CodedHeight),
            destination,
            destination.Size,
            destination.Bounds,
            premultiplied);
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
        if (track.CodecType != Heif4CharCode.Av01)
        {
            throw new ImageFormatException($"No decoder is available for image-sequence sample type '{track.CodecType}'.");
        }

        Av1CodecConfiguration codecConfiguration = track.Av1CodecConfiguration
            ?? throw new InvalidImageContentException("The AV1 image-sequence track has no codec configuration.");

        using IMemoryOwner<byte> sampleOwner = this.ReadSequenceSample(stream, track, sample);
        Span<byte> sampleData = sampleOwner.GetSpan()[..sample.Length];
        decoder.DecodeSequenceReference(sampleData, track.CicpProfile, codecConfiguration);
    }

    /// <summary>
    /// Reads and validates one bounded AV1 sequence sample into allocator-owned codec input storage.
    /// </summary>
    /// <param name="stream">The complete seekable HEIF stream.</param>
    /// <param name="track">The track supplying the codec configuration and color description.</param>
    /// <param name="sample">The validated sample range.</param>
    /// <returns>The allocator-owned buffer containing the validated coded sample.</returns>
    private IMemoryOwner<byte> ReadSequenceSample(
        BufferedReadStream stream,
        HeifSequenceTrack track,
        HeifSequenceSample sample)
    {
        Av1CodecConfiguration codecConfiguration = track.Av1CodecConfiguration
            ?? throw new InvalidImageContentException("The AV1 image-sequence track has no codec configuration.");

        IMemoryOwner<byte> sampleOwner = this.configuration.MemoryAllocator.Allocate<byte>(sample.Length);
        try
        {
            Span<byte> sampleData = sampleOwner.GetSpan()[..sample.Length];
            stream.Position = sample.Offset;
            HeifBoxReader.ReadExactly(stream, sampleData, "The HEIF image-sequence sample is truncated.");
            codecConfiguration.ValidateSampleData(
                sampleData,
                sample.IsSync,
                track.ContentLightLevel,
                track.MasteringDisplayColorVolume,
                this.Options,
                out _,
                out _);

            return sampleOwner;
        }
        catch
        {
            sampleOwner.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Updates image-level metadata from the selected color and optional alpha sequence tracks.
    /// </summary>
    /// <param name="metadata">The image metadata receiving the sequence description.</param>
    /// <param name="sequence">The parsed selected image sequence.</param>
    private void UpdateSequenceMetadata(ImageMetadata metadata, HeifSequence sequence)
    {
        HeifSequenceTrack colorTrack = sequence.ColorTrack;
        HeifMetadata heifMetadata = metadata.GetHeifMetadata();
        heifMetadata.RepeatCount = colorTrack.RepeatCount;
        heifMetadata.AnimateRootFrame = true;
        heifMetadata.HasAlpha = sequence.AlphaTrack is not null;
        switch (colorTrack.CodecType)
        {
            case Heif4CharCode.Av01:
                Av1CodecConfiguration av1Configuration = colorTrack.Av1CodecConfiguration
                    ?? throw new InvalidImageContentException("The AV1 image-sequence track has no codec configuration.");

                heifMetadata.CompressionMethod = HeifCompressionMethod.Av1;
                heifMetadata.BitDepth = av1Configuration.BitDepth;
                heifMetadata.IsMonochrome = av1Configuration.IsMonochrome;
                break;
            case Heif4CharCode.Hvc1:
                HevcCodecConfiguration hevcConfiguration = colorTrack.HevcCodecConfiguration
                    ?? throw new InvalidImageContentException("The HEVC image-sequence track has no codec configuration.");

                heifMetadata.CompressionMethod = HeifCompressionMethod.Hevc;
                heifMetadata.BitDepth = hevcConfiguration.BitDepth;
                heifMetadata.IsMonochrome = hevcConfiguration.IsMonochrome;
                break;
            default:
                throw new InvalidImageContentException($"The image-sequence sample entry '{colorTrack.CodecType}' is not supported.");
        }

        if (this.Options.SkipMetadata)
        {
            return;
        }

        metadata.IccProfile = colorTrack.IccProfile?.DeepClone();
        metadata.CicpProfile = colorTrack.CicpProfile?.DeepClone();
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
            this.ExecuteAncillarySegmentAction(() => ApplyExifProfile(metadata, trackMetadata.ExifData));
        }

        if (trackMetadata?.XmpData is not null)
        {
            this.ExecuteAncillarySegmentAction(() => metadata.XmpProfile = new XmpProfile(trackMetadata.XmpData));
        }
    }

    /// <summary>
    /// Creates one HEIF frame-metadata entry for each visible retained sequence sample.
    /// </summary>
    /// <param name="track">The selected color track supplying sample durations.</param>
    /// <returns>The exact visible-frame metadata array in presentation order.</returns>
    private static ImageFrameMetadata[] CreateSequenceFrameMetadata(HeifSequenceTrack track)
    {
        int visibleFrameCount = 0;
        foreach (HeifSequenceSample sample in track.Samples)
        {
            visibleFrameCount += sample.IsHidden ? 0 : 1;
        }

        ImageFrameMetadata[] result = new ImageFrameMetadata[visibleFrameCount];
        int frameIndex = 0;
        foreach (HeifSequenceSample sample in track.Samples)
        {
            if (sample.IsHidden)
            {
                continue;
            }

            ImageFrameMetadata frameMetadata = new();
            frameMetadata.GetHeifMetadata().FrameDelay = new Rational(sample.Duration, track.MediaTimescale);
            result[frameIndex++] = frameMetadata;
        }

        return result;
    }

    /// <summary>
    /// Updates identification metadata from the primary item or its decodable thumbnail fallback.
    /// </summary>
    /// <param name="metadata">The destination image metadata.</param>
    /// <param name="item">The primary item whose visible representation is being identified.</param>
    private void UpdateMetadata(ImageMetadata metadata, HeifItem item)
    {
        HeifItem presentationItem = item;
        HeifItem metadataItem = item;
        if (item.Type == Heif4CharCode.Grid)
        {
            // A grid is a derived image rather than a compression method. Its dimg references identify the coded
            // tile items whose decoder determines the compression reported for the primary presentation.
            HeifItem? gridTile = this.FindDecodableGridTile<Rgba32>(item);
            HeifItem? thumbnail = gridTile is null ? this.FindDecodableThumbnail<Rgba32>(item) : null;
            metadataItem = gridTile ?? thumbnail ?? item;
            presentationItem = thumbnail ?? item;
            if (gridTile is not null)
            {
                Av1CodecConfiguration? gridConfiguration = gridTile.Type == Heif4CharCode.Av01
                    ? gridTile.Av1CodecConfiguration
                        ?? throw new InvalidImageContentException($"AV1 image grid tile {gridTile.Id} has no codec configuration property.")
                    : null;

                foreach (HeifItemLink link in this.itemLinks)
                {
                    if (link.Type != Heif4CharCode.Dimg || link.SourceId != item.Id)
                    {
                        continue;
                    }

                    foreach (uint tileId in link.DestinationIds)
                    {
                        HeifItem tile = this.FindItemById(tileId)!;
                        if (tile.Type != gridTile.Type)
                        {
                            throw new InvalidImageContentException("All HEIF image grid tiles must use the same coding format.");
                        }

                        if (gridConfiguration is not null)
                        {
                            Av1CodecConfiguration tileConfiguration = tile.Av1CodecConfiguration
                                ?? throw new InvalidImageContentException($"AV1 image grid tile {tile.Id} has no codec configuration property.");

                            // Identify never reads the derived-image descriptor or coded tile payloads, but it still
                            // validates the shared sample layout needed to describe the displayed grid accurately.
                            if (!gridConfiguration.HasMatchingImageConfiguration(tileConfiguration))
                            {
                                throw new InvalidImageContentException("All AV1 image grid tiles must use matching codec configurations.");
                            }
                        }
                    }
                }
            }
        }
        else if (HeifCompressionFactory.GetDecoder<Rgba32>(item.Type) is null)
        {
            HeifItem? thumbnail = this.FindDecodableThumbnail<Rgba32>(item);
            metadataItem = thumbnail ?? item;
            presentationItem = thumbnail ?? item;
        }

        HeifMetadata meta = metadata.GetHeifMetadata();
        HeifCompressionMethod compressionMethod = HeifCompressionMethod.Hevc;
        if (metadataItem.Type == Heif4CharCode.Av01)
        {
            Av1CodecConfiguration codecConfiguration = metadataItem.Av1CodecConfiguration
                ?? throw new InvalidImageContentException($"AV1 image item {metadataItem.Id} has no codec configuration property.");

            compressionMethod = HeifCompressionMethod.Av1;
            meta.BitDepth = codecConfiguration.BitDepth;
            meta.IsMonochrome = codecConfiguration.IsMonochrome;
        }
        else if (metadataItem.Type == Heif4CharCode.Hvc1)
        {
            HevcCodecConfiguration codecConfiguration = metadataItem.HevcCodecConfiguration
                ?? throw new InvalidImageContentException($"HEVC image item {metadataItem.Id} has no codec configuration property.");

            if (metadataItem.ChannelBitDepths is not null)
            {
                codecConfiguration.ValidateChannelBitDepths(metadataItem.ChannelBitDepths);
            }

            meta.BitDepth = codecConfiguration.BitDepth;
            meta.IsMonochrome = codecConfiguration.IsMonochrome;
        }
        else if (metadataItem.Type == Heif4CharCode.Jpeg)
        {
            compressionMethod = HeifCompressionMethod.LegacyJpeg;
        }

        meta.CompressionMethod = compressionMethod;
        meta.HasAlpha = this.FindAlphaItem(presentationItem) is not null
            || (presentationItem.Type == Heif4CharCode.Grid && this.FindGridAlphaTiles(presentationItem) is not null);

        if (!this.Options.SkipMetadata)
        {
            this.ApplyItemColorMetadata(metadata, presentationItem);
            this.ApplyItemHdrMetadata(metadata, presentationItem);
            this.ApplyItemPixelAspectRatioMetadata(metadata, presentationItem);
        }
    }

    /// <summary>
    /// Indexes and parses the recognized children of a metadata box.
    /// </summary>
    /// <param name="stream">The stream positioned at the metadata full-box header.</param>
    /// <param name="boxLength">The bounded metadata payload length.</param>
    private void ParseMetadata(BufferedReadStream stream, long boxLength)
    {
        if (boxLength < 4)
        {
            throw new InvalidImageContentException("The metadata box is missing its version and flags.");
        }

        long endPosition = stream.Position + boxLength;
        stream.Skip(4);

        // Physical child order is not a dependency order. Record bounded payload positions first, then parse item
        // declarations before the locations, references, and properties that resolve those identifiers.
        Dictionary<Heif4CharCode, (long Offset, long Length)> boxes = [];
        while (stream.Position < endPosition)
        {
            long length = HeifBoxReader.ReadHeader(stream, endPosition, this.boxHeaderScratch, out Heif4CharCode boxType);
            if (MetadataParseOrder.Contains(boxType))
            {
                // Association and location boxes can precede the item declarations they reference.
                if (!boxes.TryAdd(boxType, (stream.Position, length)))
                {
                    throw new InvalidImageContentException($"The metadata box contains duplicate '{boxType}' boxes.");
                }
            }

            HeifBoxReader.Skip(stream, length);
        }

        foreach (Heif4CharCode boxType in MetadataParseOrder)
        {
            if (!boxes.TryGetValue(boxType, out (long Offset, long Length) box))
            {
                continue;
            }

            stream.Position = box.Offset;
            switch (boxType)
            {
                case Heif4CharCode.Hdlr:
                    this.ParseHandler(stream, box.Length);
                    break;
                case Heif4CharCode.Iinf:
                    this.ParseItemInfo(stream, box.Length);
                    break;
                case Heif4CharCode.Pitm:
                    this.ParsePrimaryItem(stream, box.Length);
                    break;
                case Heif4CharCode.Iref:
                    this.ParseItemReference(stream, box.Length);
                    break;
                case Heif4CharCode.Iloc:
                    this.ParseItemLocation(stream, box.Length);
                    break;
                case Heif4CharCode.Iprp:
                    this.ParseItemProperties(stream, box.Length);
                    break;
                case Heif4CharCode.Idat:
                    if (box.Length == 0)
                    {
                        throw new InvalidImageContentException("The item data box is empty.");
                    }

                    // iloc construction method one addresses bytes from the start of the idat payload, not its header.
                    this.itemDataOffset = box.Offset;
                    this.itemDataLength = box.Length;
                    break;
            }
        }

        stream.Position = endPosition;
    }

    /// <summary>
    /// Validates that the metadata handler describes picture items rather than a timed media track.
    /// </summary>
    /// <param name="stream">The stream positioned at the handler full-box payload.</param>
    /// <param name="boxLength">The bounded handler payload length.</param>
    private void ParseHandler(BufferedReadStream stream, long boxLength)
    {
        using IMemoryOwner<byte> boxMemory = this.boxReader.ReadPayload(stream, boxLength);
        Span<byte> boxBuffer = boxMemory.GetSpan();

        EnsureBufferRemaining(boxBuffer, 0, 12, "handler");

        // The full-box header and pre_defined field precede the handler type. A picture
        // handler keeps this bounded parser in the still-image metadata model.
        int bytesRead = 8;
        Heif4CharCode handlerType = (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(boxBuffer[bytesRead..]);
        if (handlerType != Heif4CharCode.Pict)
        {
            throw new ImageFormatException("Not a picture file.");
        }
    }

    /// <summary>
    /// Parses the item-information box and its item-information entries.
    /// </summary>
    /// <param name="stream">The stream positioned at the item-information full-box payload.</param>
    /// <param name="boxLength">The bounded item-information payload length.</param>
    private void ParseItemInfo(BufferedReadStream stream, long boxLength)
    {
        using IMemoryOwner<byte> boxMemory = this.boxReader.ReadPayload(stream, boxLength);
        Span<byte> boxBuffer = boxMemory.GetSpan();
        EnsureBufferRemaining(boxBuffer, 0, 4, "item info");

        int bytesRead = 0;
        byte version = boxBuffer[bytesRead];
        if (version > 1)
        {
            throw new InvalidImageContentException($"The item info box has unsupported version {version}.");
        }

        bytesRead += 4;
        uint entryCount = ReadUInt16Or32(boxBuffer, version != 0, ref bytesRead);

        for (uint i = 0; i < entryCount; i++)
        {
            bytesRead += this.ParseItemInfoEntry(boxBuffer[bytesRead..]);
        }

        if (bytesRead != boxBuffer.Length)
        {
            throw new InvalidImageContentException("The item info entry count does not consume the item info box.");
        }
    }

    /// <summary>
    /// Parses one versioned item-information entry from a bounded item-information payload.
    /// </summary>
    /// <param name="buffer">The bytes beginning at the item-information-entry box header.</param>
    /// <returns>The complete item-information-entry box length.</returns>
    private int ParseItemInfoEntry(Span<byte> buffer)
    {
        int headerLength = HeifBoxReader.ParseHeader(buffer, out long boxLength, out Heif4CharCode boxType);
        if (boxType != Heif4CharCode.Infe)
        {
            throw new InvalidImageContentException($"The item info box contains unexpected child '{boxType}'.");
        }

        int totalLength = checked(headerLength + (int)boxLength);
        Span<byte> entryBuffer = buffer[..totalLength];
        int bytesRead = headerLength;
        EnsureBufferRemaining(entryBuffer, bytesRead, 4, "item info entry");
        byte version = entryBuffer[bytesRead];
        if (version > 3)
        {
            throw new InvalidImageContentException($"The item info entry has unsupported version {version}.");
        }

        bytesRead += 4;
        HeifItem? item = null;
        if (version is 0 or 1)
        {
            EnsureBufferRemaining(entryBuffer, bytesRead, 4, "item info entry");
            uint itemId = BinaryPrimitives.ReadUInt16BigEndian(entryBuffer[bytesRead..]);
            bytesRead += 2;
            item = new HeifItem(boxType, itemId);

            uint protectionIndex = BinaryPrimitives.ReadUInt16BigEndian(entryBuffer[bytesRead..]);
            bytesRead += 2;
            if (protectionIndex != 0)
            {
                throw new InvalidImageContentException($"Item {itemId} uses unsupported item protection.");
            }

            item.Name = ReadNullTerminatedString(entryBuffer[bytesRead..], out int nameLength);
            bytesRead += nameLength;
            item.ContentType = ReadNullTerminatedString(entryBuffer[bytesRead..], out int contentTypeLength);
            bytesRead += contentTypeLength;

            if (bytesRead < totalLength)
            {
                item.ContentEncoding = ReadNullTerminatedString(entryBuffer[bytesRead..], out int contentEncodingLength);
                bytesRead += contentEncodingLength;
            }
        }

        if (version == 1)
        {
            if (bytesRead < totalLength)
            {
                EnsureBufferRemaining(entryBuffer, bytesRead, 4, "item info entry");
                item!.ExtensionType = BinaryPrimitives.ReadUInt32BigEndian(entryBuffer[bytesRead..]);
                bytesRead += 4;
            }

            if (bytesRead < totalLength)
            {
                // Version-one extension payloads are outside the image item types currently
                // consumed by this decoder, but remain bounded within this entry.
                bytesRead = totalLength;
            }
        }

        if (version >= 2)
        {
            uint itemId = ReadUInt16Or32(entryBuffer, version == 3, ref bytesRead);

            EnsureBufferRemaining(entryBuffer, bytesRead, 6, "item info entry");

            uint protectionIndex = BinaryPrimitives.ReadUInt16BigEndian(entryBuffer[bytesRead..]);
            bytesRead += 2;
            if (protectionIndex != 0)
            {
                throw new InvalidImageContentException($"Item {itemId} uses unsupported item protection.");
            }

            Heif4CharCode itemType = (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(entryBuffer[bytesRead..]);
            bytesRead += 4;
            item = new HeifItem(itemType, itemId);
            item.Name = ReadNullTerminatedString(entryBuffer[bytesRead..], out int nameLength);
            bytesRead += nameLength;
            if (item.Type == Heif4CharCode.Mime)
            {
                item.ContentType = ReadNullTerminatedString(entryBuffer[bytesRead..], out int contentTypeLength);
                bytesRead += contentTypeLength;

                if (bytesRead < totalLength)
                {
                    item.ContentEncoding = ReadNullTerminatedString(entryBuffer[bytesRead..], out int contentEncodingLength);
                    bytesRead += contentEncodingLength;
                }
            }
            else if (item.Type == Heif4CharCode.Uri)
            {
                item.UriType = ReadNullTerminatedString(entryBuffer[bytesRead..], out int uriLength);
                bytesRead += uriLength;
            }
        }

        if (item is not null)
        {
            if (this.FindItemById(item.Id) is not null)
            {
                throw new InvalidImageContentException($"The item info box contains duplicate item ID {item.Id}.");
            }

            this.items.Add(item);
        }

        if (bytesRead != totalLength)
        {
            throw new InvalidImageContentException("The item info entry contains unexpected trailing data.");
        }

        return totalLength;
    }

    /// <summary>
    /// Parses typed relationships between source and destination items.
    /// </summary>
    /// <param name="stream">The stream positioned at the item-reference full-box payload.</param>
    /// <param name="boxLength">The bounded item-reference payload length.</param>
    private void ParseItemReference(BufferedReadStream stream, long boxLength)
    {
        using IMemoryOwner<byte> boxMemory = this.boxReader.ReadPayload(stream, boxLength);
        Span<byte> boxBuffer = boxMemory.GetSpan();
        EnsureBufferRemaining(boxBuffer, 0, 4, "item reference");

        int bytesRead = 0;
        byte version = boxBuffer[bytesRead];
        if (version > 1)
        {
            throw new InvalidImageContentException($"The item reference box has unsupported version {version}.");
        }

        bool largeIds = version == 1;
        bytesRead += 4;
        while (bytesRead < boxLength)
        {
            int referenceHeaderLength = HeifBoxReader.ParseHeader(boxBuffer[bytesRead..], out long referenceLength, out Heif4CharCode linkType);
            int referenceEnd = checked(bytesRead + referenceHeaderLength + (int)referenceLength);
            Span<byte> referenceBuffer = boxBuffer[..referenceEnd];
            bytesRead += referenceHeaderLength;

            if (linkType is not Heif4CharCode.Dimg
                and not Heif4CharCode.Auxl
                and not Heif4CharCode.Prem
                and not Heif4CharCode.Thmb
                and not Heif4CharCode.Cdsc)
            {
                // Unknown reference types do not participate in the bounded image model. Their child-box boundary
                // was validated above, so skip the payload without imposing semantics from a general ISOBMFF reader.
                bytesRead = referenceEnd;
                continue;
            }

            if (this.Options.SkipMetadata && linkType == Heif4CharCode.Cdsc)
            {
                // Descriptive metadata links have no effect when their payloads are not requested. Avoid validating
                // their optional item graph while preserving the surrounding image relationships.
                bytesRead = referenceEnd;
                continue;
            }

            try
            {
                uint sourceId = ReadUInt16Or32(referenceBuffer, largeIds, ref bytesRead);
                if (this.FindItemById(sourceId) is null)
                {
                    throw new InvalidImageContentException($"The item reference box references unknown source item ID {sourceId}.");
                }

                HeifItemLink link = new(linkType, sourceId);

                EnsureBufferRemaining(referenceBuffer, bytesRead, 2, "item reference");
                int count = BinaryPrimitives.ReadUInt16BigEndian(referenceBuffer[bytesRead..]);
                bytesRead += 2;
                for (uint i = 0; i < count; i++)
                {
                    uint destId = ReadUInt16Or32(referenceBuffer, largeIds, ref bytesRead);
                    if (this.FindItemById(destId) is null)
                    {
                        throw new InvalidImageContentException($"The item reference box references unknown destination item ID {destId}.");
                    }

                    link.DestinationIds.Add(destId);
                }

                if (bytesRead != referenceEnd)
                {
                    throw new InvalidImageContentException($"The '{linkType}' item reference length does not match its entry count.");
                }

                this.itemLinks.Add(link);
            }
            catch (Exception ex) when (linkType == Heif4CharCode.Cdsc && ImageDecoderCore.ShouldIgnoreAncillarySegmentError(this.Options, ex))
            {
                // A malformed descriptive link cannot change reconstructed pixels, so non-strict modes omit it.
                bytesRead = referenceEnd;
            }
            catch (Exception ex) when (linkType != Heif4CharCode.Cdsc && ImageDecoderCore.ShouldIgnoreImageDataSegmentError(this.Options, ex))
            {
                // IgnoreImageData permits a malformed optional image relationship to be omitted while retaining
                // independently reconstructable items and thumbnail fallbacks.
                bytesRead = referenceEnd;
            }
        }
    }

    /// <summary>
    /// Reads the identifier of the presentation's primary item.
    /// </summary>
    /// <param name="stream">The stream positioned at the primary-item full-box payload.</param>
    /// <param name="boxLength">The bounded primary-item payload length.</param>
    private void ParsePrimaryItem(BufferedReadStream stream, long boxLength)
    {
        using IMemoryOwner<byte> boxMemory = this.boxReader.ReadPayload(stream, boxLength);
        Span<byte> boxBuffer = boxMemory.GetSpan();
        EnsureBufferRemaining(boxBuffer, 0, 4, "primary item");

        byte version = boxBuffer[0];
        if (version > 1)
        {
            throw new InvalidImageContentException($"The primary item box has unsupported version {version}.");
        }

        int bytesRead = 4;
        this.primaryItem = ReadUInt16Or32(boxBuffer, version == 1, ref bytesRead);
        if (bytesRead != boxBuffer.Length)
        {
            throw new InvalidImageContentException("The primary item box has an invalid length.");
        }
    }

    /// <summary>
    /// Parses the ordered item-property table and applies its item associations.
    /// </summary>
    /// <param name="stream">The stream positioned at the item-properties payload.</param>
    /// <param name="boxLength">The bounded item-properties payload length.</param>
    private void ParseItemProperties(BufferedReadStream stream, long boxLength)
    {
        // Property types may repeat, and ipma can physically precede ipco. Index the bounded
        // children first so associations are always resolved after the ordered property table.
        List<KeyValuePair<Heif4CharCode, object>> properties = new();
        long endBoxPosition = stream.Position + boxLength;
        (long Offset, long Length)? propertyContainer = null;
        List<(long Offset, long Length)> associations = [];
        while (stream.Position < endBoxPosition)
        {
            long containerLength = HeifBoxReader.ReadHeader(stream, endBoxPosition, this.boxHeaderScratch, out Heif4CharCode containerType);
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
        foreach ((long Offset, long Length) association in associations)
        {
            stream.Position = association.Offset;
            this.ParsePropertyAssociation(stream, association.Length, properties);
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
            long itemLength = HeifBoxReader.ReadHeader(stream, endPosition, this.boxHeaderScratch, out Heif4CharCode itemType);
            if (this.Options.SkipMetadata && itemType is Heif4CharCode.Pasp
                or Heif4CharCode.Clli
                or Heif4CharCode.Mdcv
                or Heif4CharCode.Cclv
                or Heif4CharCode.Amve
                or Heif4CharCode.Reve
                or Heif4CharCode.Ndwt)
            {
                // These properties affect only exposed image metadata. Preserve their physical ipco positions while
                // avoiding payload allocation and validation when the caller requested no metadata.
                HeifBoxReader.Skip(stream, itemLength);
                properties.Add(new KeyValuePair<Heif4CharCode, object>(itemType, IgnoredProperty));
                continue;
            }

            if (this.Options.SkipMetadata && itemType == Heif4CharCode.Colr && itemLength >= 4)
            {
                Span<byte> profileTypeBuffer = this.boxHeaderScratch.AsSpan(0, 4);
                HeifBoxReader.ReadExactly(stream, profileTypeBuffer, "The HEIF color-information property is truncated.");
                Heif4CharCode profileType = (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(profileTypeBuffer);
                if (profileType is Heif4CharCode.RICC or Heif4CharCode.Prof)
                {
                    // ICC bytes cannot affect reconstruction when metadata is skipped. Retain only the property index
                    // and leave the potentially large profile payload out of the allocator entirely.
                    HeifBoxReader.Skip(stream, itemLength - 4);
                    properties.Add(new KeyValuePair<Heif4CharCode, object>(Heif4CharCode.Colr, IgnoredProperty));
                    continue;
                }

                stream.Position -= 4;
            }

            using IMemoryOwner<byte> boxMemory = this.boxReader.ReadPayload(stream, itemLength);
            Span<byte> boxBuffer = boxMemory.GetSpan();
            try
            {
                switch (itemType)
                {
                    case Heif4CharCode.Ispe:
                        EnsureBufferRemaining(boxBuffer, 0, 12, "image spatial extents");

                        // The full-box header precedes the unsigned display width and height.
                        uint width = BinaryPrimitives.ReadUInt32BigEndian(boxBuffer[4..]);
                        uint height = BinaryPrimitives.ReadUInt32BigEndian(boxBuffer[8..]);
                        if (width is 0 or > int.MaxValue || height is 0 or > int.MaxValue)
                        {
                            throw new InvalidImageContentException("The image spatial extents property has invalid dimensions.");
                        }

                        properties.Add(new KeyValuePair<Heif4CharCode, object>(Heif4CharCode.Ispe, new Size((int)width, (int)height)));
                        break;
                    case Heif4CharCode.Pasp:
                        object pixelAspectRatio = IgnoredProperty;
                        try
                        {
                            pixelAspectRatio = HeifPropertyParser.ParsePixelAspectRatio(boxBuffer);
                        }
                        catch (Exception ex) when (ImageDecoderCore.ShouldIgnoreAncillarySegmentError(this.Options, ex))
                        {
                            // Keep the understood property index without retaining invalid ancillary metadata.
                        }

                        properties.Add(new KeyValuePair<Heif4CharCode, object>(Heif4CharCode.Pasp, pixelAspectRatio));
                        break;
                    case Heif4CharCode.Pixi:
                        EnsureBufferRemaining(boxBuffer, 0, 5, "pixel information");
                        if (boxBuffer[0] != 0 || boxBuffer[1] != 0 || boxBuffer[2] != 0 || boxBuffer[3] != 0)
                        {
                            throw new InvalidImageContentException("The pixel information property has an unsupported version or flags.");
                        }

                        // The full-box header precedes one bit-depth byte for each channel.
                        int channelCount = boxBuffer[4];
                        if (channelCount == 0)
                        {
                            throw new InvalidImageContentException("The pixel information property has no channels.");
                        }

                        int offset = 5;
                        EnsureBufferRemaining(boxBuffer, offset, channelCount, "pixel information");
                        if (boxBuffer.Length != offset + channelCount)
                        {
                            throw new InvalidImageContentException("The pixel information property contains unexpected trailing data.");
                        }

                        // Property associations are resolved after the pooled box buffer is reused, so retain the
                        // exact channel vector once at this ownership boundary.
                        byte[] channelBitDepths = GC.AllocateUninitializedArray<byte>(channelCount);
                        boxBuffer.Slice(offset, channelCount).CopyTo(channelBitDepths);
                        for (int i = 0; i < channelBitDepths.Length; i++)
                        {
                            if (channelBitDepths[i] == 0)
                            {
                                throw new InvalidImageContentException($"The pixel information property declares zero precision for channel {i}.");
                            }
                        }

                        properties.Add(new KeyValuePair<Heif4CharCode, object>(Heif4CharCode.Pixi, channelBitDepths));

                        break;
                    case Heif4CharCode.AuxC:
                        EnsureBufferRemaining(boxBuffer, 0, 5, "auxiliary type");
                        if (boxBuffer[0] != 0)
                        {
                            throw new InvalidImageContentException($"The auxiliary type property has unsupported version {boxBuffer[0]}.");
                        }

                        // aux_type is a required null-terminated string. Any remaining bytes are the registered
                        // auxiliary subtype payload, which is not needed to identify an alpha image plane.
                        string auxiliaryType = ReadNullTerminatedString(boxBuffer[4..], out _);
                        properties.Add(new KeyValuePair<Heif4CharCode, object>(Heif4CharCode.AuxC, auxiliaryType));
                        break;
                    case Heif4CharCode.Colr:
                        EnsureBufferRemaining(boxBuffer, 0, 4, "color information");
                        Heif4CharCode profileType = (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(boxBuffer);
                        object colorInformation = UnknownProperty;
                        if (profileType is Heif4CharCode.RICC or Heif4CharCode.Prof)
                        {
                            if (!this.Options.SkipMetadata)
                            {
                                EnsureBufferRemaining(boxBuffer, 4, 1, "ICC color information");
                                IccProfile? iccProfile = null;
                                try
                                {
                                    iccProfile = HeifPropertyParser.ParseIccProfile(boxBuffer[4..]);
                                }
                                catch (Exception ex) when (ImageDecoderCore.ShouldIgnoreAncillarySegmentError(this.Options, ex))
                                {
                                    // Keep the understood property index without retaining invalid ancillary metadata.
                                }

                                // A malformed ancillary profile can be ignored by policy while the physical property still
                                // occupies its ipco index and remains understood for essential-association handling.
                                colorInformation = iccProfile ?? IgnoredProperty;
                            }
                            else
                            {
                                colorInformation = IgnoredProperty;
                            }
                        }
                        else if (profileType == Heif4CharCode.Nclx)
                        {
                            colorInformation = HeifPropertyParser.ParseCicpProfile(boxBuffer[4..]);
                        }

                        properties.Add(new KeyValuePair<Heif4CharCode, object>(Heif4CharCode.Colr, colorInformation));

                        break;
                    case Heif4CharCode.Clli:
                        object contentLightLevel = IgnoredProperty;
                        try
                        {
                            contentLightLevel = HeifPropertyParser.ParseContentLightLevel(boxBuffer);
                        }
                        catch (Exception ex) when (ImageDecoderCore.ShouldIgnoreAncillarySegmentError(this.Options, ex))
                        {
                            // Keep the understood property index without retaining invalid ancillary metadata.
                        }

                        properties.Add(new KeyValuePair<Heif4CharCode, object>(Heif4CharCode.Clli, contentLightLevel));
                        break;
                    case Heif4CharCode.Mdcv:
                        object masteringDisplayColorVolume = IgnoredProperty;
                        try
                        {
                            masteringDisplayColorVolume = HeifPropertyParser.ParseMasteringDisplayColorVolume(boxBuffer);
                        }
                        catch (Exception ex) when (ImageDecoderCore.ShouldIgnoreAncillarySegmentError(this.Options, ex))
                        {
                            // Keep the understood property index without retaining invalid ancillary metadata.
                        }

                        properties.Add(new KeyValuePair<Heif4CharCode, object>(Heif4CharCode.Mdcv, masteringDisplayColorVolume));
                        break;
                    case Heif4CharCode.Cclv:
                        object contentColorVolume = IgnoredProperty;
                        try
                        {
                            contentColorVolume = HeifPropertyParser.ParseContentColorVolume(boxBuffer);
                        }
                        catch (Exception ex) when (ImageDecoderCore.ShouldIgnoreAncillarySegmentError(this.Options, ex))
                        {
                            // Keep the understood property index without retaining invalid ancillary metadata.
                        }

                        properties.Add(new KeyValuePair<Heif4CharCode, object>(Heif4CharCode.Cclv, contentColorVolume));
                        break;
                    case Heif4CharCode.Amve:
                        object ambientViewingEnvironment = IgnoredProperty;
                        try
                        {
                            ambientViewingEnvironment = HeifPropertyParser.ParseAmbientViewingEnvironment(boxBuffer);
                        }
                        catch (Exception ex) when (ImageDecoderCore.ShouldIgnoreAncillarySegmentError(this.Options, ex))
                        {
                            // Keep the understood property index without retaining invalid ancillary metadata.
                        }

                        properties.Add(new KeyValuePair<Heif4CharCode, object>(Heif4CharCode.Amve, ambientViewingEnvironment));
                        break;
                    case Heif4CharCode.Reve:
                        object referenceViewingEnvironment = IgnoredProperty;
                        try
                        {
                            referenceViewingEnvironment = HeifPropertyParser.ParseReferenceViewingEnvironment(boxBuffer);
                        }
                        catch (Exception ex) when (ImageDecoderCore.ShouldIgnoreAncillarySegmentError(this.Options, ex))
                        {
                            // Keep the understood property index without retaining invalid ancillary metadata.
                        }

                        properties.Add(new KeyValuePair<Heif4CharCode, object>(Heif4CharCode.Reve, referenceViewingEnvironment));
                        break;
                    case Heif4CharCode.Ndwt:
                        object nominalDiffuseWhite = IgnoredProperty;
                        try
                        {
                            nominalDiffuseWhite = HeifPropertyParser.ParseNominalDiffuseWhite(boxBuffer);
                        }
                        catch (Exception ex) when (ImageDecoderCore.ShouldIgnoreAncillarySegmentError(this.Options, ex))
                        {
                            // Keep the understood property index without retaining invalid ancillary metadata.
                        }

                        properties.Add(new KeyValuePair<Heif4CharCode, object>(Heif4CharCode.Ndwt, nominalDiffuseWhite));
                        break;
                    case Heif4CharCode.Av1C:
                        EnsureBufferRemaining(boxBuffer, 0, 4, "AV1 codec configuration");
                        properties.Add(
                            new KeyValuePair<Heif4CharCode, object>(
                                Heif4CharCode.Av1C,
                                new Av1CodecConfiguration(boxBuffer, this.Options)));

                        break;
                    case Heif4CharCode.A1op:
                        properties.Add(
                            new KeyValuePair<Heif4CharCode, object>(
                                Heif4CharCode.A1op,
                                HeifPropertyParser.ParseAv1OperatingPointSelector(boxBuffer)));

                        break;
                    case Heif4CharCode.Lsel:
                        properties.Add(
                            new KeyValuePair<Heif4CharCode, object>(
                                Heif4CharCode.Lsel,
                                HeifPropertyParser.ParseAv1LayerSelector(boxBuffer)));

                        break;
                    case Heif4CharCode.A1lx:
                        properties.Add(
                            new KeyValuePair<Heif4CharCode, object>(
                                Heif4CharCode.A1lx,
                                HeifPropertyParser.ParseAv1LayeredImageIndex(boxBuffer)));

                        break;
                    case Heif4CharCode.HvcC:
                        properties.Add(
                            new KeyValuePair<Heif4CharCode, object>(
                                Heif4CharCode.HvcC,
                                new HevcCodecConfiguration(boxBuffer)));

                        break;
                    case Heif4CharCode.Clap:
                        properties.Add(
                            new KeyValuePair<Heif4CharCode, object>(
                                Heif4CharCode.Clap,
                                HeifPropertyParser.ParseCleanAperture(boxBuffer)));

                        break;
                    case Heif4CharCode.Irot:
                        properties.Add(new KeyValuePair<Heif4CharCode, object>(Heif4CharCode.Irot, HeifPropertyParser.ParseRotation(boxBuffer)));
                        break;
                    case Heif4CharCode.Imir:
                        properties.Add(new KeyValuePair<Heif4CharCode, object>(Heif4CharCode.Imir, HeifPropertyParser.ParseMirrorAxis(boxBuffer)));
                        break;
                    case Heif4CharCode.Altt:
                    case Heif4CharCode.Iscl:
                    case Heif4CharCode.Rloc:
                    case Heif4CharCode.Udes:
                        // These registered image properties are not arbitrary unknown boxes. Preserve their indices so
                        // container identification remains available while their owning image stage handles the value.
                        properties.Add(new KeyValuePair<Heif4CharCode, object>(itemType, IgnoredProperty));
                        break;
                    default:
                        // Unknown properties still occupy an ipco index and become an error only when marked essential.
                        properties.Add(new KeyValuePair<Heif4CharCode, object>(itemType, UnknownProperty));
                        break;
                }
            }
            catch (Exception ex) when (ImageDecoderCore.ShouldIgnoreImageDataSegmentError(this.Options, ex))
            {
                // Invalid image properties retain their physical association index. Typed association handling ignores
                // the placeholder so another decodable item or the coded-image defaults can remain usable.
                properties.Add(new KeyValuePair<Heif4CharCode, object>(itemType, IgnoredProperty));
            }
        }
    }

    /// <summary>
    /// Applies one-based property indices and essential flags to their referenced items.
    /// </summary>
    /// <param name="stream">The stream positioned at the property-association full-box payload.</param>
    /// <param name="boxLength">The bounded property-association payload length.</param>
    /// <param name="properties">The properties in the order used by association indices.</param>
    private void ParsePropertyAssociation(BufferedReadStream stream, long boxLength, List<KeyValuePair<Heif4CharCode, object>> properties)
    {
        using IMemoryOwner<byte> boxMemory = this.boxReader.ReadPayload(stream, boxLength);
        Span<byte> boxBuffer = boxMemory.GetSpan();
        EnsureBufferRemaining(boxBuffer, 0, 8, "item property association");
        byte version = boxBuffer[0];
        if (version > 1)
        {
            throw new InvalidImageContentException($"The item property association box has unsupported version {version}.");
        }

        bool largePropertyIndex = (boxBuffer[3] & 1) != 0;
        int bytesRead = 4;
        uint entryCount = BinaryPrimitives.ReadUInt32BigEndian(boxBuffer[bytesRead..]);
        bytesRead += 4;
        for (uint entryIndex = 0; entryIndex < entryCount; entryIndex++)
        {
            uint itemId = ReadUInt16Or32(boxBuffer, version == 1, ref bytesRead);
            HeifItem? item = this.FindItemById(itemId);
            if (item is null)
            {
                throw new InvalidImageContentException($"Item property association references unknown item ID {itemId}.");
            }

            EnsureBufferRemaining(boxBuffer, bytesRead, 1, "item property association");
            int associationCount = boxBuffer[bytesRead++];
            for (int i = 0; i < associationCount; i++)
            {
                uint association;
                uint propertyIndexMask;
                uint essentialMask;
                if (largePropertyIndex)
                {
                    EnsureBufferRemaining(boxBuffer, bytesRead, 2, "item property association");
                    association = BinaryPrimitives.ReadUInt16BigEndian(boxBuffer[bytesRead..]);
                    bytesRead += 2;
                    propertyIndexMask = 0x7FFFU;
                    essentialMask = 0x8000U;
                }
                else
                {
                    EnsureBufferRemaining(boxBuffer, bytesRead, 1, "item property association");
                    association = boxBuffer[bytesRead++];
                    propertyIndexMask = 0x7FU;
                    essentialMask = 0x80U;
                }

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

                KeyValuePair<Heif4CharCode, object> prop = properties[(int)propertyIndex];
                if (essential && ReferenceEquals(prop.Value, UnknownProperty))
                {
                    throw new InvalidImageContentException($"Item {itemId} associates unknown essential property '{prop.Key}'.");
                }

                if (!essential && prop.Key is Heif4CharCode.Clap or Heif4CharCode.Irot or Heif4CharCode.Imir)
                {
                    this.ThrowOrIgnoreImageDataSegmentError(
                        $"Item {itemId} associates nonessential transformative property '{prop.Key}'.");

                    continue;
                }

                // AVIF 1.1 section 2.3.2.1.1 requires a1op to be essential, while HEIF section 6.5.11.1
                // imposes the same requirement on lsel because ignoring either selector changes the decoded image.
                if (!essential && prop.Key is Heif4CharCode.A1op or Heif4CharCode.Lsel)
                {
                    this.ThrowOrIgnoreImageDataSegmentError(
                        $"Item {itemId} associates AV1 selector property '{prop.Key}' without marking it essential.");

                    continue;
                }

                // AVIF 1.1 section 2.3.2.3.2 requires a1lx to be nonessential; decoders may consume the complete
                // item payload without using its optional layer-boundary optimization.
                if (essential && prop.Key == Heif4CharCode.A1lx)
                {
                    this.ThrowOrIgnoreImageDataSegmentError(
                        $"Item {itemId} marks AV1 layered-image indexing property '{prop.Key}' as essential.");

                    continue;
                }

                switch (prop.Key)
                {
                    case Heif4CharCode.Ispe:
                        if (prop.Value is Size extent)
                        {
                            item.SetExtent(extent);
                        }

                        break;
                    case Heif4CharCode.Pasp:
                        if (prop.Value is HeifPixelAspectRatio pixelAspectRatio)
                        {
                            if (item.PixelAspectRatio is not null)
                            {
                                this.ThrowOrIgnoreNonStrictSegmentError(
                                    $"Item {itemId} associates more than one pixel aspect ratio property.");

                                break;
                            }

                            item.PixelAspectRatio = pixelAspectRatio;
                        }

                        break;
                    case Heif4CharCode.Pixi:
                        if (prop.Value is byte[] channelBitDepths)
                        {
                            if (item.ChannelBitDepths is not null)
                            {
                                this.ThrowOrIgnoreImageDataSegmentError(
                                    $"Item {itemId} associates more than one pixel information property.");

                                break;
                            }

                            int bitsPerPixel = 0;
                            for (int channel = 0; channel < channelBitDepths.Length; channel++)
                            {
                                bitsPerPixel += channelBitDepths[channel];
                            }

                            item.ChannelCount = channelBitDepths.Length;
                            item.ChannelBitDepths = channelBitDepths;
                            item.BitsPerPixel = bitsPerPixel;
                        }

                        break;
                    case Heif4CharCode.Av1C:
                        if (prop.Value is Av1CodecConfiguration av1CodecConfiguration)
                        {
                            if (item.Type != Heif4CharCode.Av01)
                            {
                                this.ThrowOrIgnoreImageDataSegmentError(
                                    $"Item {itemId} associates an AV1 codec configuration with non-AV1 item type '{item.Type}'.");

                                break;
                            }

                            if (item.Av1CodecConfiguration is not null)
                            {
                                this.ThrowOrIgnoreImageDataSegmentError(
                                    $"Item {itemId} associates more than one AV1 codec configuration property.");

                                break;
                            }

                            item.Av1CodecConfiguration = av1CodecConfiguration;
                        }

                        break;
                    case Heif4CharCode.A1op:
                        if (prop.Value is Av1OperatingPointSelector operatingPointSelector)
                        {
                            if (item.Type != Heif4CharCode.Av01)
                            {
                                this.ThrowOrIgnoreImageDataSegmentError(
                                    $"Item {itemId} associates an AV1 operating-point selector with non-AV1 item type '{item.Type}'.");

                                break;
                            }

                            if (item.Av1OperatingPointSelector is not null)
                            {
                                this.ThrowOrIgnoreImageDataSegmentError(
                                    $"Item {itemId} associates more than one AV1 operating-point selector property.");

                                break;
                            }

                            item.Av1OperatingPointSelector = operatingPointSelector;
                        }

                        break;
                    case Heif4CharCode.Lsel:
                        if (prop.Value is Av1LayerSelector layerSelector)
                        {
                            if (item.Type != Heif4CharCode.Av01)
                            {
                                this.ThrowOrIgnoreImageDataSegmentError(
                                    $"Item {itemId} associates an AV1 layer selector with non-AV1 item type '{item.Type}'.");

                                break;
                            }

                            if (item.Av1LayerSelector is not null)
                            {
                                this.ThrowOrIgnoreImageDataSegmentError(
                                    $"Item {itemId} associates more than one AV1 layer selector property.");

                                break;
                            }

                            item.Av1LayerSelector = layerSelector;
                        }

                        break;
                    case Heif4CharCode.A1lx:
                        if (prop.Value is Av1LayeredImageIndex layeredImageIndex)
                        {
                            if (item.Type != Heif4CharCode.Av01)
                            {
                                this.ThrowOrIgnoreImageDataSegmentError(
                                    $"Item {itemId} associates AV1 layered-image indexing with non-AV1 item type '{item.Type}'.");

                                break;
                            }

                            if (item.Av1LayeredImageIndex is not null)
                            {
                                this.ThrowOrIgnoreImageDataSegmentError(
                                    $"Item {itemId} associates more than one AV1 layered-image indexing property.");

                                break;
                            }

                            item.Av1LayeredImageIndex = layeredImageIndex;
                        }

                        break;
                    case Heif4CharCode.HvcC:
                        if (prop.Value is HevcCodecConfiguration hevcCodecConfiguration)
                        {
                            if (item.Type != Heif4CharCode.Hvc1)
                            {
                                this.ThrowOrIgnoreImageDataSegmentError(
                                    $"Item {itemId} associates an HEVC codec configuration with non-HEVC item type '{item.Type}'.");

                                break;
                            }

                            if (item.HevcCodecConfiguration is not null)
                            {
                                this.ThrowOrIgnoreImageDataSegmentError(
                                    $"Item {itemId} associates more than one HEVC codec configuration property.");

                                break;
                            }

                            item.HevcCodecConfiguration = hevcCodecConfiguration;
                        }

                        break;
                    case Heif4CharCode.AuxC:
                        if (prop.Value is string auxiliaryType)
                        {
                            if (item.AuxiliaryType is not null)
                            {
                                this.ThrowOrIgnoreImageDataSegmentError(
                                    $"Item {itemId} associates more than one auxiliary type property.");

                                break;
                            }

                            item.AuxiliaryType = auxiliaryType;
                        }

                        break;
                    case Heif4CharCode.Colr:
                        if (prop.Value is IccProfile iccProfile)
                        {
                            if (item.IccProfile is not null)
                            {
                                this.ThrowOrIgnoreNonStrictSegmentError(
                                    $"Item {itemId} associates more than one ICC color property.");

                                break;
                            }

                            item.IccProfile = iccProfile;
                        }
                        else if (prop.Value is CicpProfile cicpProfile)
                        {
                            if (item.CicpProfile is not null)
                            {
                                this.ThrowOrIgnoreImageDataSegmentError(
                                    $"Item {itemId} associates more than one CICP color property.");

                                break;
                            }

                            item.CicpProfile = cicpProfile;
                        }

                        break;
                    case Heif4CharCode.Clli:
                        if (prop.Value is HeifContentLightLevel contentLightLevel)
                        {
                            if (item.ContentLightLevel is not null)
                            {
                                this.ThrowOrIgnoreNonStrictSegmentError(
                                    $"Item {itemId} associates more than one content light level property.");

                                break;
                            }

                            item.ContentLightLevel = contentLightLevel;
                        }

                        break;
                    case Heif4CharCode.Mdcv:
                        if (prop.Value is HeifMasteringDisplayColorVolume masteringDisplayColorVolume)
                        {
                            if (item.MasteringDisplayColorVolume is not null)
                            {
                                this.ThrowOrIgnoreNonStrictSegmentError(
                                    $"Item {itemId} associates more than one mastering display color-volume property.");

                                break;
                            }

                            item.MasteringDisplayColorVolume = masteringDisplayColorVolume;
                        }

                        break;
                    case Heif4CharCode.Cclv:
                        if (prop.Value is HeifContentColorVolume contentColorVolume)
                        {
                            if (item.ContentColorVolume is not null)
                            {
                                this.ThrowOrIgnoreNonStrictSegmentError(
                                    $"Item {itemId} associates more than one content color-volume property.");

                                break;
                            }

                            item.ContentColorVolume = contentColorVolume;
                        }

                        break;
                    case Heif4CharCode.Amve:
                        if (prop.Value is HeifAmbientViewingEnvironment ambientViewingEnvironment)
                        {
                            if (item.AmbientViewingEnvironment is not null)
                            {
                                this.ThrowOrIgnoreNonStrictSegmentError(
                                    $"Item {itemId} associates more than one ambient viewing-environment property.");

                                break;
                            }

                            item.AmbientViewingEnvironment = ambientViewingEnvironment;
                        }

                        break;
                    case Heif4CharCode.Reve:
                        if (prop.Value is HeifReferenceViewingEnvironment referenceViewingEnvironment)
                        {
                            if (item.ReferenceViewingEnvironment is not null)
                            {
                                this.ThrowOrIgnoreNonStrictSegmentError(
                                    $"Item {itemId} associates more than one reference viewing-environment property.");

                                break;
                            }

                            item.ReferenceViewingEnvironment = referenceViewingEnvironment;
                        }

                        break;
                    case Heif4CharCode.Ndwt:
                        if (prop.Value is HeifNominalDiffuseWhite nominalDiffuseWhite)
                        {
                            if (item.NominalDiffuseWhite is not null)
                            {
                                this.ThrowOrIgnoreNonStrictSegmentError(
                                    $"Item {itemId} associates more than one nominal diffuse-white property.");

                                break;
                            }

                            item.NominalDiffuseWhite = nominalDiffuseWhite;
                        }

                        break;
                    case Heif4CharCode.Clap:
                        if (prop.Value is HeifCleanAperture cleanAperture)
                        {
                            if (item.CleanAperture is not null)
                            {
                                this.ThrowOrIgnoreImageDataSegmentError(
                                    $"Item {itemId} associates more than one clean aperture property.");

                                break;
                            }

                            item.CleanAperture = cleanAperture;
                        }

                        break;
                    case Heif4CharCode.Irot:
                        if (prop.Value is byte rotationAngle)
                        {
                            if (item.RotationAngle is not null)
                            {
                                this.ThrowOrIgnoreImageDataSegmentError(
                                    $"Item {itemId} associates more than one image rotation property.");

                                break;
                            }

                            item.RotationAngle = rotationAngle;
                        }

                        break;
                    case Heif4CharCode.Imir:
                        if (prop.Value is byte mirrorAxis)
                        {
                            if (item.MirrorAxis is not null)
                            {
                                this.ThrowOrIgnoreImageDataSegmentError(
                                    $"Item {itemId} associates more than one image mirror property.");

                                break;
                            }

                            item.MirrorAxis = mirrorAxis;
                        }

                        break;
                }
            }
        }

        if (bytesRead != boxBuffer.Length)
        {
            throw new InvalidImageContentException("The item property association box contains unexpected trailing data.");
        }
    }

    /// <summary>
    /// Parses the construction method, base offset, and ordered extents for every declared item.
    /// </summary>
    /// <param name="stream">The stream positioned at the item-location full-box payload.</param>
    /// <param name="boxLength">The bounded item-location payload length.</param>
    private void ParseItemLocation(BufferedReadStream stream, long boxLength)
    {
        using IMemoryOwner<byte> boxMemory = this.boxReader.ReadPayload(stream, boxLength);
        Span<byte> boxBuffer = boxMemory.GetSpan();
        int bytesRead = 0;
        EnsureBufferRemaining(boxBuffer, bytesRead, 6, "item location");
        byte version = boxBuffer[bytesRead];
        if (version > 2)
        {
            throw new InvalidImageContentException($"The item location box has unsupported version {version}.");
        }

        bytesRead += 4;

        // The first two payload bytes pack four-bit integer widths for extent offset, extent length, base offset,
        // and, for versions one and two, extent index. A zero width represents an implicit zero value.
        byte b1 = boxBuffer[bytesRead];
        bytesRead++;
        byte b2 = boxBuffer[bytesRead];
        bytesRead++;
        int offsetSize = (b1 >> 4) & 0x0f;
        int lengthSize = b1 & 0x0f;
        int baseOffsetSize = (b2 >> 4) & 0x0f;
        int indexSize = 0;
        if (version is 1 or 2)
        {
            indexSize = b2 & 0x0f;
        }

        if (!IsSupportedFieldSize(offsetSize)
            || !IsSupportedFieldSize(lengthSize)
            || !IsSupportedFieldSize(baseOffsetSize)
            || !IsSupportedFieldSize(indexSize))
        {
            throw new InvalidImageContentException("The item location box uses an invalid integer field size.");
        }

        EnsureBufferRemaining(boxBuffer, bytesRead, version == 2 ? 4 : 2, "item location");
        uint itemCount = ReadUInt16Or32(boxBuffer, version == 2, ref bytesRead);
        HashSet<uint> locatedItemIds = [];
        for (uint i = 0; i < itemCount; i++)
        {
            EnsureBufferRemaining(boxBuffer, bytesRead, version == 2 ? 4 : 2, "item location");
            uint itemId = ReadUInt16Or32(boxBuffer, version == 2, ref bytesRead);
            HeifItem? item = this.FindItemById(itemId);
            if (item is null)
            {
                throw new InvalidImageContentException($"The item location box references unknown item ID {itemId}.");
            }

            if (!locatedItemIds.Add(itemId))
            {
                throw new InvalidImageContentException($"The item location box contains duplicate locations for item ID {itemId}.");
            }

            HeifLocationOffsetOrigin constructionMethod = HeifLocationOffsetOrigin.FileOffset;
            if (version is 1 or 2)
            {
                EnsureBufferRemaining(boxBuffer, bytesRead, 2, "item location");
                ushort constructionField = BinaryPrimitives.ReadUInt16BigEndian(boxBuffer[bytesRead..]);
                bytesRead += 2;
                if ((constructionField & 0xFFF0) != 0)
                {
                    throw new InvalidImageContentException("The item location box has nonzero reserved construction bits.");
                }

                constructionMethod = (HeifLocationOffsetOrigin)(constructionField & 0x0F);
                if (constructionMethod is not HeifLocationOffsetOrigin.FileOffset and not HeifLocationOffsetOrigin.ItemDataOffset)
                {
                    throw new InvalidImageContentException($"The item location box uses unsupported construction method {(int)constructionMethod}.");
                }
            }

            EnsureBufferRemaining(boxBuffer, bytesRead, 2, "item location");
            uint dataReferenceIndex = BinaryPrimitives.ReadUInt16BigEndian(boxBuffer[bytesRead..]);
            bytesRead += 2;
            if (dataReferenceIndex != 0)
            {
                throw new InvalidImageContentException("External item data references are not supported.");
            }

            long baseOffset = ReadUIntVariable(boxBuffer, baseOffsetSize, ref bytesRead);
            EnsureBufferRemaining(boxBuffer, bytesRead, 2, "item location");
            uint extentCount = BinaryPrimitives.ReadUInt16BigEndian(boxBuffer[bytesRead..]);
            bytesRead += 2;
            for (uint j = 0; j < extentCount; j++)
            {
                if (version is 1 or 2 && indexSize > 0)
                {
                    // Extent indices select referenced-item extents only for construction method two. Methods zero
                    // and one still carry the field when configured, so consume it to preserve the following offsets.
                    ReadUIntVariable(boxBuffer, indexSize, ref bytesRead);
                }

                long extentOffset = ReadUIntVariable(boxBuffer, offsetSize, ref bytesRead);
                long extentLength = ReadUIntVariable(boxBuffer, lengthSize, ref bytesRead);
                HeifLocation loc = new(constructionMethod, baseOffset, extentOffset, extentLength);
                item.DataLocations.Add(loc);
            }
        }

        if (bytesRead != boxBuffer.Length)
        {
            throw new InvalidImageContentException("The item location box contains unexpected trailing data.");
        }
    }

    /// <summary>
    /// Determines whether an item-location integer width can be represented by the supported reader primitives.
    /// </summary>
    /// <param name="size">The width in bytes from an item-location size nibble.</param>
    /// <returns><see langword="true"/> for the registered zero, 32-bit, and 64-bit widths.</returns>
    private static bool IsSupportedFieldSize(int size) => size is 0 or 4 or 8;

    /// <summary>
    /// Reads a version-selected 16-bit or 32-bit unsigned identifier or count.
    /// </summary>
    /// <param name="buffer">The bounded box payload.</param>
    /// <param name="isLarge">Indicates that the field is 32 bits rather than 16 bits.</param>
    /// <param name="bytesRead">The running payload offset, advanced past the field.</param>
    /// <returns>The decoded unsigned value.</returns>
    private static uint ReadUInt16Or32(Span<byte> buffer, bool isLarge, ref int bytesRead)
    {
        int fieldLength = isLarge ? 4 : 2;
        EnsureBufferRemaining(buffer, bytesRead, fieldLength, "versioned integer field");

        uint result;
        if (isLarge)
        {
            result = BinaryPrimitives.ReadUInt32BigEndian(buffer[bytesRead..]);
            bytesRead += 4;
        }
        else
        {
            result = BinaryPrimitives.ReadUInt16BigEndian(buffer[bytesRead..]);
            bytesRead += 2;
        }

        return result;
    }

    /// <summary>
    /// Reads a zero-width, 32-bit, or 64-bit unsigned item-location field into the supported stream range.
    /// </summary>
    /// <param name="buffer">The bounded item-location payload.</param>
    /// <param name="numBytes">The field width selected by the item-location size nibble.</param>
    /// <param name="bytesRead">The running payload offset, advanced past the field.</param>
    /// <returns>The decoded nonnegative stream offset or length.</returns>
    private static long ReadUIntVariable(Span<byte> buffer, int numBytes, ref int bytesRead)
    {
        EnsureBufferRemaining(buffer, bytesRead, numBytes, "item location");
        ulong result = numBytes switch
        {
            0 => 0,
            4 => BinaryPrimitives.ReadUInt32BigEndian(buffer[bytesRead..]),
            8 => BinaryPrimitives.ReadUInt64BigEndian(buffer[bytesRead..]),
            _ => throw new InvalidImageContentException("The item location box uses an invalid integer field size.")
        };

        if (result > long.MaxValue)
        {
            throw new InvalidImageContentException("An item location offset exceeds the supported stream range.");
        }

        bytesRead += numBytes;
        return (long)result;
    }

    /// <summary>
    /// Resolves item extents, selects the primary or supported thumbnail decoder, and reconstructs the image.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel format.</typeparam>
    /// <param name="stream">The complete seekable HEIF container stream.</param>
    /// <param name="cancellationToken">The token used to cancel item assembly and payload decoding.</param>
    /// <returns>The image reconstructed from the selected item.</returns>
    private Image<TPixel> DecodePrimaryItem<TPixel>(BufferedReadStream stream, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using DisposableDictionary<uint, IMemoryOwner<byte>> buffers = new(this.items.Count);
        foreach (HeifItem item in this.items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool isMetadataItem = item.Type is Heif4CharCode.Exif or Heif4CharCode.Mime;
            if (this.Options.SkipMetadata && isMetadataItem)
            {
                // Metadata items are not codec inputs. Leave their extents on the stream when metadata loading is disabled.
                continue;
            }

            IMemoryOwner<byte>? extentMemory = null;
            try
            {
                long itemLength = 0;
                foreach (HeifLocation loc in item.DataLocations)
                {
                    if (loc.Length < 0 || itemLength > int.MaxValue - loc.Length)
                    {
                        throw new InvalidImageContentException($"Item {item.Id} data is too large to buffer.");
                    }

                    itemLength += loc.Length;
                }

                if (itemLength == 0)
                {
                    continue;
                }

                // One logical item is the concatenation of its extents in declared order. Materialize only that item data,
                // never the enclosing file or mdat box, so codec readers receive the contiguous payload they expect.
                int bufferLength = (int)itemLength;
                extentMemory = this.configuration.MemoryAllocator.Allocate<byte>(bufferLength);
                Span<byte> itemBuffer = extentMemory.GetSpan()[..bufferLength];
                int writeOffset = 0;
                foreach (HeifLocation loc in item.DataLocations)
                {
                    if (loc.BaseOffset < 0 || loc.Offset < 0 || loc.BaseOffset > long.MaxValue - loc.Offset)
                    {
                        throw new InvalidImageContentException($"Item {item.Id} has an invalid extent offset.");
                    }

                    long relativeOffset = loc.BaseOffset + loc.Offset;
                    long sourceOffset;
                    long sourceBytesRemaining;
                    if (loc.Origin == HeifLocationOffsetOrigin.FileOffset)
                    {
                        // Construction method zero resolves base_offset + extent_offset from the start of the file.
                        sourceOffset = relativeOffset;
                        sourceBytesRemaining = stream.Length - sourceOffset;
                    }
                    else if (loc.Origin == HeifLocationOffsetOrigin.ItemDataOffset)
                    {
                        if (this.itemDataOffset < 0 || relativeOffset > this.itemDataLength)
                        {
                            throw new InvalidImageContentException($"Item {item.Id} has an extent outside its item data box.");
                        }

                        // Construction method one resolves the same relative value from the idat payload start.
                        sourceOffset = this.itemDataOffset + relativeOffset;
                        sourceBytesRemaining = this.itemDataLength - relativeOffset;
                    }
                    else
                    {
                        throw new InvalidImageContentException($"Item {item.Id} uses an unsupported location origin.");
                    }

                    HeifBoxReader.EnsureInsideParent(loc.Length, sourceBytesRemaining);
                    stream.Position = sourceOffset;
                    int extentLength = (int)loc.Length;
                    int bytesRead = stream.Read(itemBuffer.Slice(writeOffset, extentLength));
                    if (bytesRead != extentLength)
                    {
                        throw new InvalidImageContentException($"Item {item.Id} extent is truncated.");
                    }

                    writeOffset += extentLength;
                }

                buffers.Add(item.Id, extentMemory);
                extentMemory = null;
            }
            catch (Exception ex) when (isMetadataItem && ImageDecoderCore.ShouldIgnoreAncillarySegmentError(this.Options, ex))
            {
                // A failed optional metadata extent is discarded without weakening image-item extent validation.
                extentMemory?.Dispose();
            }
            catch (Exception ex) when (!isMetadataItem && ImageDecoderCore.ShouldIgnoreImageDataSegmentError(this.Options, ex))
            {
                // Keep the item declaration but omit its unreadable payload. The presentation can still use a valid
                // thumbnail, omit an auxiliary plane, or reject the file later when no decodable color item remains.
                extentMemory?.Dispose();
            }
            catch
            {
                // The dictionary takes ownership only after every declared extent has been assembled successfully.
                extentMemory?.Dispose();
                throw;
            }
        }

        HeifItem? rootItem = this.FindItemById(this.primaryItem);
        if (rootItem is null)
        {
            throw new ImageFormatException("No primary HEIF item defined.");
        }

        Image<TPixel>? image = null;
        HeifItem itemToDecode = rootItem;
        IHeifItemDecoder<TPixel>? itemDecoder = this.GetItemDecoder<TPixel>(rootItem, buffers);
        bool supportedItemFound = itemDecoder is not null;
        if (itemDecoder is not null)
        {
            this.ExecuteImageDataSegmentAction(
                () => image = this.DecodeImageItem(rootItem, itemDecoder, buffers, cancellationToken));
        }

        if (image is null)
        {
            // An unsupported primary item always permits its registered thumbnail fallback. IgnoreImageData also
            // reaches this branch after a recoverable primary payload failure, matching other multi-image decoders.
            HeifItem? thumbnailItem = this.FindDecodableThumbnail<TPixel>(rootItem);
            if (thumbnailItem is not null)
            {
                itemDecoder = HeifCompressionFactory.GetDecoder<TPixel>(thumbnailItem.Type);
                supportedItemFound |= itemDecoder is not null;
                if (itemDecoder is not null)
                {
                    itemToDecode = thumbnailItem;
                    this.ExecuteImageDataSegmentAction(
                        () => image = this.DecodeImageItem(thumbnailItem, itemDecoder, buffers, cancellationToken));
                }
            }
        }

        if (image is null || itemDecoder is null)
        {
            if (!supportedItemFound)
            {
                throw new ImageFormatException("No supported image item was found inside this HEIF container.");
            }

            throw new InvalidImageContentException("The HEIF container does not contain a decodable image item.");
        }

        try
        {
            bool hasAlpha = false;
            this.ExecuteImageDataSegmentAction(
                () => hasAlpha = this.DecodeAlphaPlane(itemToDecode, buffers, image.Frames.RootFrame, cancellationToken));

            if (!this.Options.SkipMetadata)
            {
                this.ApplyItemColorMetadata(image.Metadata, itemToDecode);
                this.ApplyItemHdrMetadata(image.Metadata, itemToDecode);
                this.ApplyAssociatedMetadata(image.Metadata, rootItem, buffers);
            }

            // MIAF defines crop, rotation, and mirror as presentation operations in that order. Applying the
            // implemented transforms after alpha composition keeps the auxiliary plane in the same coordinate space.
            ApplyPresentationTransforms(image, itemToDecode);

            if (!this.Options.SkipMetadata)
            {
                this.ApplyItemPixelAspectRatioMetadata(image.Metadata, itemToDecode);

                // ICC conversion belongs to the presented RGB image. Running it after alpha, grid composition, crop,
                // rotation, and mirroring keeps still images aligned with the sequence path and avoids converting
                // pixels removed by a clean-aperture crop.
                _ = this.TryConvertIccProfile(image);
            }

            // The decoder determines the compression of the pixels that were actually returned, including grid tiles
            // and a thumbnail fallback when the primary image compression is not available.
            HeifMetadata meta = image.Metadata.GetHeifMetadata();
            meta.CompressionMethod = itemDecoder.CompressionMethod;
            meta.HasAlpha = hasAlpha;
            if (this.Options.SkipMetadata)
            {
                // AV1 item decoders still parse encoded metadata to enforce codec/container equivalence and select
                // the correct conversion. Remove the exposed values so the decoder option suppresses metadata.
                image.Metadata.CicpProfile = null;
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
    /// Applies the color profiles associated with a presented still-image item.
    /// </summary>
    /// <param name="metadata">The image metadata receiving the profiles.</param>
    /// <param name="colorItem">The color image item whose pixels are presented.</param>
    private void ApplyItemColorMetadata(ImageMetadata metadata, HeifItem colorItem)
    {
        // Color properties can be associated with the derived grid or its coded tile items. Prefer the presentation
        // grid and use the first decodable tile only when the grid does not provide the corresponding profile.
        HeifItem? gridTile = colorItem.Type == Heif4CharCode.Grid
            ? this.FindDecodableGridTile<Rgba32>(colorItem)
            : null;

        IccProfile? iccProfile = colorItem.IccProfile ?? gridTile?.IccProfile;
        if (iccProfile is not null)
        {
            metadata.IccProfile = iccProfile.DeepClone();
        }

        CicpProfile? cicpProfile = colorItem.CicpProfile ?? gridTile?.CicpProfile;
        if (cicpProfile is not null)
        {
            metadata.CicpProfile = cicpProfile.DeepClone();
        }
    }

    /// <summary>
    /// Applies high-dynamic-range metadata associated with a presented still-image item.
    /// </summary>
    /// <param name="metadata">The image metadata receiving the high-dynamic-range description.</param>
    /// <param name="imageItem">The image item whose pixels are presented.</param>
    private void ApplyItemHdrMetadata(ImageMetadata metadata, HeifItem imageItem)
    {
        HeifItem? gridTile = imageItem.Type == Heif4CharCode.Grid
            ? this.FindDecodableGridTile<Rgba32>(imageItem)
            : null;

        // A derived grid can describe the complete presentation. Fall back to the first coded tile only when the
        // grid does not carry its own value, matching the precedence used for its color-profile properties.
        HeifContentLightLevel? contentLightLevel = imageItem.ContentLightLevel ?? gridTile?.ContentLightLevel;
        if (contentLightLevel is not null)
        {
            metadata.GetHeifMetadata().ContentLightLevel = contentLightLevel;
        }

        HeifMasteringDisplayColorVolume? masteringDisplayColorVolume = imageItem.MasteringDisplayColorVolume
            ?? gridTile?.MasteringDisplayColorVolume;

        if (masteringDisplayColorVolume is not null)
        {
            metadata.GetHeifMetadata().MasteringDisplayColorVolume = masteringDisplayColorVolume;
        }

        HeifContentColorVolume? contentColorVolume = imageItem.ContentColorVolume ?? gridTile?.ContentColorVolume;
        if (contentColorVolume is not null)
        {
            metadata.GetHeifMetadata().ContentColorVolume = contentColorVolume;
        }

        HeifAmbientViewingEnvironment? ambientViewingEnvironment = imageItem.AmbientViewingEnvironment
            ?? gridTile?.AmbientViewingEnvironment;

        if (ambientViewingEnvironment is not null)
        {
            metadata.GetHeifMetadata().AmbientViewingEnvironment = ambientViewingEnvironment;
        }

        HeifReferenceViewingEnvironment? referenceViewingEnvironment = imageItem.ReferenceViewingEnvironment
            ?? gridTile?.ReferenceViewingEnvironment;

        if (referenceViewingEnvironment is not null)
        {
            metadata.GetHeifMetadata().ReferenceViewingEnvironment = referenceViewingEnvironment;
        }

        HeifNominalDiffuseWhite? nominalDiffuseWhite = imageItem.NominalDiffuseWhite ?? gridTile?.NominalDiffuseWhite;
        if (nominalDiffuseWhite is not null)
        {
            metadata.GetHeifMetadata().NominalDiffuseWhite = nominalDiffuseWhite;
        }
    }

    /// <summary>
    /// Applies the pixel aspect ratio associated with a presented still-image item.
    /// </summary>
    /// <param name="metadata">The image metadata receiving the aspect ratio.</param>
    /// <param name="imageItem">The image item whose pixels are presented.</param>
    private void ApplyItemPixelAspectRatioMetadata(ImageMetadata metadata, HeifItem imageItem)
    {
        HeifItem? gridTile = imageItem.Type == Heif4CharCode.Grid
            ? this.FindDecodableGridTile<Rgba32>(imageItem)
            : null;

        HeifPixelAspectRatio? pixelAspectRatio = imageItem.PixelAspectRatio ?? gridTile?.PixelAspectRatio;
        if (pixelAspectRatio is null)
        {
            return;
        }

        ApplyPixelAspectRatioMetadata(metadata, pixelAspectRatio, imageItem.RotationAngle);
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
    /// <param name="metadata">The decoded image metadata receiving the profiles.</param>
    /// <param name="colorItem">The color image item described by the metadata links.</param>
    /// <param name="buffers">The assembled payloads for the container's declared items.</param>
    private void ApplyAssociatedMetadata(
        ImageMetadata metadata,
        HeifItem colorItem,
        DisposableDictionary<uint, IMemoryOwner<byte>> buffers)
    {
        foreach (HeifItemLink link in this.itemLinks)
        {
            if (link.Type != Heif4CharCode.Cdsc || !link.DestinationIds.Contains(colorItem.Id))
            {
                continue;
            }

            HeifItem? metadataItem = this.FindItemById(link.SourceId);
            if (metadataItem is null || !buffers.TryGetValue(metadataItem.Id, out IMemoryOwner<byte>? itemMemory))
            {
                continue;
            }

            if (metadataItem.Type == Heif4CharCode.Exif)
            {
                this.ExecuteAncillarySegmentAction(() => ApplyExifProfile(metadata, itemMemory.GetSpan()));
            }
            else if (metadataItem.Type == Heif4CharCode.Mime &&
                string.Equals(metadataItem.ContentType, "application/rdf+xml", StringComparison.Ordinal))
            {
                this.ExecuteAncillarySegmentAction(() =>
                {
                    Span<byte> itemData = itemMemory.GetSpan();

                    // XmpProfile retains its input array after the assembled item buffer is returned to its pool.
                    byte[] ownedData = GC.AllocateUninitializedArray<byte>(itemData.Length);
                    itemData.CopyTo(ownedData);
                    metadata.XmpProfile = new XmpProfile(ownedData);
                });
            }
        }
    }

    /// <summary>
    /// Validates the HEIF Exif TIFF-header offset and applies the contained TIFF payload.
    /// </summary>
    /// <param name="metadata">The image metadata receiving the Exif profile.</param>
    /// <param name="itemData">The complete HEIF Exif item including its four-byte offset field.</param>
    private static void ApplyExifProfile(ImageMetadata metadata, ReadOnlySpan<byte> itemData)
    {
        if (itemData.Length < 8)
        {
            throw new InvalidImageContentException("The HEIF Exif item is truncated.");
        }

        uint declaredTiffHeaderOffset = BinaryPrimitives.ReadUInt32BigEndian(itemData);
        ReadOnlySpan<byte> exifData = itemData[4..];
        int actualTiffHeaderOffset = -1;

        // Annex A stores the offset to the first TIFF byte-order marker. Match libavif by finding the first valid
        // TIFF signature and requiring the declared offset to identify that same header.
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
            throw new InvalidImageContentException("The HEIF Exif item has an invalid TIFF-header offset.");
        }

        ReadOnlySpan<byte> tiffData = exifData[actualTiffHeaderOffset..];

        // ExifProfile retains its input array after the assembled item buffers are disposed at the end of decode.
        byte[] ownedData = GC.AllocateUninitializedArray<byte>(tiffData.Length);
        tiffData.CopyTo(ownedData);
        metadata.ExifProfile = new ExifProfile(ownedData);
    }

    /// <summary>
    /// Selects the registered coded-image or grid decoder for an image item.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel format.</typeparam>
    /// <param name="item">The coded or derived image item.</param>
    /// <param name="buffers">The assembled payloads available to a grid decoder and its tiles.</param>
    /// <returns>The selected decoder, or <see langword="null"/> when the item cannot be reconstructed.</returns>
    private IHeifItemDecoder<TPixel>? GetItemDecoder<TPixel>(HeifItem item, DisposableDictionary<uint, IMemoryOwner<byte>> buffers)
        where TPixel : unmanaged, IPixel<TPixel>
        => item.Type == Heif4CharCode.Grid && this.FindDecodableGridTile<TPixel>(item) is not null
            ? new GridHeifItemDecoder<TPixel>(this.items, this.itemLinks, buffers)
            : HeifCompressionFactory.GetDecoder<TPixel>(item.Type);

    /// <summary>
    /// Decodes one image item from its assembled payload.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel format.</typeparam>
    /// <param name="item">The image item to decode.</param>
    /// <param name="decoder">The decoder selected for the item.</param>
    /// <param name="buffers">The assembled item payloads.</param>
    /// <param name="cancellationToken">The token used to cancel the payload decode.</param>
    /// <returns>The decoded image.</returns>
    private Image<TPixel> DecodeImageItem<TPixel>(
        HeifItem item,
        IHeifItemDecoder<TPixel> decoder,
        DisposableDictionary<uint, IMemoryOwner<byte>> buffers,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (!buffers.TryGetValue(item.Id, out IMemoryOwner<byte>? itemMemory))
        {
            throw new InvalidImageContentException($"Item {item.Id} has no data extents.");
        }

        Image<TPixel> image = decoder.DecodeItemData(
            this.payloadOptions,
            item,
            itemMemory.GetSpan(),
            item.CicpProfile,
            cancellationToken);

        try
        {
            HeifItemDecoderUtilities.ScaleToItemExtent(image, item);
            return image;
        }
        catch
        {
            image.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Gets the dimensions of an image item after its clean-aperture and rotation properties are applied.
    /// </summary>
    /// <param name="item">The image item whose presentation dimensions are requested.</param>
    /// <returns>The item dimensions after the optional crop and quarter-turn rotation.</returns>
    private static Size GetPresentationExtent(HeifItem item)
    {
        Size extent = item.CleanAperture is not null ? item.CleanAperture.Value.ToRectangle(item.Extent).Size : item.Extent;
        return item.RotationAngle is not null && (item.RotationAngle.Value & 1) != 0
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
    /// Applies the clean-aperture, rotation, and mirror properties associated with an image item.
    /// </summary>
    /// <typeparam name="TPixel">The image pixel format.</typeparam>
    /// <param name="image">The decoded image item.</param>
    /// <param name="item">The item carrying the presentation properties.</param>
    private static void ApplyPresentationTransforms<TPixel>(Image<TPixel> image, HeifItem item)
        where TPixel : unmanaged, IPixel<TPixel>
        => ApplyPresentationTransforms(image, item.CleanAperture, item.RotationAngle, item.MirrorAxis);

    /// <summary>
    /// Applies shared clean-aperture, rotation, and mirror properties to every frame of an image presentation.
    /// </summary>
    /// <typeparam name="TPixel">The image pixel format.</typeparam>
    /// <param name="image">The decoded image presentation.</param>
    /// <param name="cleanAperture">The optional clean-aperture crop.</param>
    /// <param name="rotationAngle">The optional counter-clockwise quarter-turn count.</param>
    /// <param name="mirrorAxis">The optional horizontal or vertical mirror axis.</param>
    private static void ApplyPresentationTransforms<TPixel>(
        Image<TPixel> image,
        HeifCleanAperture? cleanAperture,
        byte? rotationAngle,
        byte? mirrorAxis)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (cleanAperture is not null)
        {
            Rectangle cropRectangle = cleanAperture.Value.ToRectangle(image.Size);
            if (cropRectangle != image.Bounds)
            {
                image.Mutate(context => context.Crop(cropRectangle));
            }
        }

        if (rotationAngle is not null)
        {
            // HEIF angles count quarter turns counter-clockwise, while ImageSharp's optimized rotate modes are clockwise.
            RotateMode rotation = rotationAngle.Value switch
            {
                1 => RotateMode.Rotate270,
                2 => RotateMode.Rotate180,
                3 => RotateMode.Rotate90,
                _ => RotateMode.None
            };

            if (rotation != RotateMode.None)
            {
                image.Mutate(context => context.Rotate(rotation));
            }
        }

        if (mirrorAxis is not null)
        {
            // Axis zero reflects top-to-bottom around the horizontal axis; axis one reflects left-to-right.
            FlipMode flip = mirrorAxis.Value == 0 ? FlipMode.Vertical : FlipMode.Horizontal;
            image.Mutate(context => context.Flip(flip));
        }
    }

    /// <summary>
    /// Decodes and composes the direct or per-grid-tile alpha auxiliary associated with a color image item.
    /// </summary>
    /// <typeparam name="TPixel">The destination color pixel type.</typeparam>
    /// <param name="colorItem">The color image item whose alpha plane is requested.</param>
    /// <param name="buffers">The assembled item payloads.</param>
    /// <param name="destination">The decoded color frame receiving alpha values.</param>
    /// <param name="cancellationToken">The token used to cancel the auxiliary payload decode.</param>
    /// <returns><see langword="true"/> when an auxiliary alpha plane was decoded and composed.</returns>
    private bool DecodeAlphaPlane<TPixel>(
        HeifItem colorItem,
        DisposableDictionary<uint, IMemoryOwner<byte>> buffers,
        ImageFrame<TPixel> destination,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        HeifItem? alphaItem = this.FindAlphaItem(colorItem);
        if (alphaItem is not null)
        {
            // libavif releases through 1.3 omitted alpha transform associations, so accept complete absence for
            // compatibility. If either property is present, it must match the color item before plane composition.
            bool alphaHasTransforms = alphaItem.CleanAperture is not null ||
                alphaItem.RotationAngle is not null ||
                alphaItem.MirrorAxis is not null;

            bool cleanAperturesMatch = alphaItem.CleanAperture is null
                ? colorItem.CleanAperture is null
                : colorItem.CleanAperture is not null && alphaItem.CleanAperture.Value.Equals(colorItem.CleanAperture.Value);

            if (alphaHasTransforms &&
                (!cleanAperturesMatch || alphaItem.RotationAngle != colorItem.RotationAngle || alphaItem.MirrorAxis != colorItem.MirrorAxis))
            {
                throw new ImageFormatException("The alpha auxiliary image and color image use different presentation transforms.");
            }

            IHeifItemDecoder<TPixel>? itemDecoder = this.GetItemDecoder<TPixel>(alphaItem, buffers);
            if (itemDecoder is not IHeifAlphaItemDecoder<TPixel> decoder)
            {
                throw new ImageFormatException($"The alpha auxiliary item uses unsupported item type '{alphaItem.Type}'.");
            }

            bool premultiplied = this.itemLinks.Any(
                link => link.Type == Heif4CharCode.Prem
                    && link.SourceId == colorItem.Id
                    && link.DestinationIds.Contains(alphaItem.Id));

            if (!buffers.TryGetValue(alphaItem.Id, out IMemoryOwner<byte>? itemMemory))
            {
                throw new InvalidImageContentException($"Item {alphaItem.Id} has no data extents.");
            }

            decoder.DecodeAlphaItemData(
                this.payloadOptions,
                alphaItem,
                itemMemory.GetSpan(),
                destination,
                destination.Size,
                destination.Bounds,
                premultiplied,
                cancellationToken);

            return true;
        }

        if (colorItem.Type != Heif4CharCode.Grid)
        {
            return false;
        }

        List<uint>? alphaTileIds = this.FindGridAlphaTiles(colorItem);
        if (alphaTileIds is null)
        {
            return false;
        }

        if (!buffers.TryGetValue(colorItem.Id, out IMemoryOwner<byte>? gridMemory))
        {
            throw new InvalidImageContentException($"Item {colorItem.Id} has no data extents.");
        }

        // The color grid descriptor defines the same row/column layout and output canvas for per-tile alpha
        // auxiliaries. Supplying their IDs lets the existing grid compositor preserve that normative ordering.
        GridHeifItemDecoder<TPixel> gridDecoder = new(
            this.items,
            this.itemLinks,
            buffers,
            alphaTileIds);

        gridDecoder.DecodeAlphaItemData(
            this.payloadOptions,
            colorItem,
            gridMemory.GetSpan(),
            destination,
            destination.Size,
            destination.Bounds,
            false,
            cancellationToken);

        return true;
    }

    /// <summary>
    /// Validates that a fixed-width field remains within a buffered box payload.
    /// </summary>
    /// <param name="buffer">The bounded box payload.</param>
    /// <param name="offset">The zero-based field offset.</param>
    /// <param name="count">The field width in bytes.</param>
    /// <param name="boxName">The diagnostic name used for malformed input errors.</param>
    private static void EnsureBufferRemaining(ReadOnlySpan<byte> buffer, int offset, int count, string boxName)
    {
        if ((uint)offset > (uint)buffer.Length || (uint)count > (uint)(buffer.Length - offset))
        {
            throw new InvalidImageContentException($"The {boxName} box is truncated.");
        }
    }

    /// <summary>
    /// Finds an item by its file-defined identifier.
    /// </summary>
    /// <param name="itemId">The item identifier.</param>
    /// <returns>The matching item, or <see langword="null"/> when it has not been declared.</returns>
    private HeifItem? FindItemById(uint itemId)
        => this.items.FirstOrDefault(item => item.Id == itemId);

    /// <summary>
    /// Finds the alpha auxiliary image linked to a color image item.
    /// </summary>
    /// <param name="colorItem">The color image item.</param>
    /// <returns>The alpha auxiliary item, or <see langword="null"/> when no registered alpha relationship exists.</returns>
    private HeifItem? FindAlphaItem(HeifItem colorItem)
    {
        HeifItem? alphaItem = null;
        foreach (HeifItemLink link in this.itemLinks)
        {
            if (link.Type != Heif4CharCode.Auxl || !link.DestinationIds.Contains(colorItem.Id))
            {
                continue;
            }

            HeifItem candidate = this.FindItemById(link.SourceId)!;
            if (!HeifConstants.IsAlphaAuxiliaryType(candidate.AuxiliaryType))
            {
                continue;
            }

            if (alphaItem is not null && alphaItem.Id != candidate.Id)
            {
                throw new InvalidImageContentException($"Item {colorItem.Id} has more than one alpha auxiliary image.");
            }

            alphaItem = candidate;
        }

        return alphaItem;
    }

    /// <summary>
    /// Resolves one alpha auxiliary image for each tile of a color grid.
    /// </summary>
    /// <param name="gridItem">The color grid whose tile order defines the alpha grid.</param>
    /// <returns>
    /// The row-major alpha tile identifiers, or <see langword="null"/> when any color tile has no alpha auxiliary.
    /// </returns>
    private List<uint>? FindGridAlphaTiles(HeifItem gridItem)
    {
        List<uint> colorTileIds = [];
        foreach (HeifItemLink link in this.itemLinks)
        {
            if (link.Type == Heif4CharCode.Dimg && link.SourceId == gridItem.Id)
            {
                colorTileIds.AddRange(link.DestinationIds);
            }
        }

        if (colorTileIds.Count == 0)
        {
            return null;
        }

        List<uint> alphaTileIds = new(colorTileIds.Count);
        foreach (uint colorTileId in colorTileIds)
        {
            HeifItem colorTile = this.FindItemById(colorTileId)!;
            HeifItem? alphaTile = this.FindAlphaItem(colorTile);
            if (alphaTile is null)
            {
                // A partial set cannot describe an alpha plane for the complete grid. libavif treats this case as
                // an opaque image rather than mixing opaque cells with auxiliary alpha cells.
                return null;
            }

            bool alphaIsDerivedTile = this.itemLinks.Any(
                link => link.Type == Heif4CharCode.Dimg && link.DestinationIds.Contains(alphaTile.Id));

            if (alphaIsDerivedTile)
            {
                throw new InvalidImageContentException($"Alpha auxiliary item {alphaTile.Id} is already a derived-image tile.");
            }

            alphaTileIds.Add(alphaTile.Id);
        }

        return alphaTileIds;
    }

    /// <summary>
    /// Finds the first tile of a grid when every referenced tile uses a registered still-image decoder.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel format used to select item decoders.</typeparam>
    /// <param name="gridItem">The grid derived-image item.</param>
    /// <returns>The first decodable grid tile, or <see langword="null"/> when the grid has no tiles or any tile cannot be decoded.</returns>
    private HeifItem? FindDecodableGridTile<TPixel>(HeifItem gridItem)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        HeifItem? firstTile = null;
        foreach (HeifItemLink link in this.itemLinks)
        {
            if (link.Type != Heif4CharCode.Dimg || link.SourceId != gridItem.Id)
            {
                continue;
            }

            foreach (uint itemId in link.DestinationIds)
            {
                HeifItem tile = this.FindItemById(itemId)!;
                if (HeifCompressionFactory.GetDecoder<TPixel>(tile.Type) is null)
                {
                    // A partially decodable grid cannot yield the requested canvas. Returning no tile lets the
                    // caller select a thumbnail of the complete primary presentation when one is available.
                    return null;
                }

                firstTile ??= tile;
            }
        }

        return firstTile;
    }

    /// <summary>
    /// Finds a decodable thumbnail that represents the specified master image item.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel format used to select item decoders.</typeparam>
    /// <param name="masterItem">The master image item referenced by the thumbnail.</param>
    /// <returns>A decodable thumbnail item, or <see langword="null"/> when no matching thumbnail is available.</returns>
    private HeifItem? FindDecodableThumbnail<TPixel>(HeifItem masterItem)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // A thumbnail reference points from the thumbnail item to the master image. Restrict fallback to this
        // presentation rather than allowing an unrelated thumbnail elsewhere in the file to be selected.
        HeifItemLink? thumbnailReference = this.itemLinks.FirstOrDefault(
            link => link.Type == Heif4CharCode.Thmb && link.DestinationIds.Contains(masterItem.Id));

        if (thumbnailReference is null)
        {
            return null;
        }

        HeifItem thumbnailItem = this.FindItemById(thumbnailReference.SourceId)!;
        if (HeifCompressionFactory.GetDecoder<TPixel>(thumbnailItem.Type) is null)
        {
            return null;
        }

        return thumbnailItem;
    }

    /// <summary>
    /// Decodes the UTF-8 bytes preceding the first null terminator.
    /// </summary>
    /// <param name="span">The bytes beginning at a required null-terminated string.</param>
    /// <param name="bytesRead">The number of source bytes consumed, including the terminator.</param>
    /// <returns>The decoded string without its terminator.</returns>
    private static string ReadNullTerminatedString(Span<byte> span, out int bytesRead)
    {
        int terminator = span.IndexOf((byte)0);
        if (terminator < 0)
        {
            throw new InvalidImageContentException("A null-terminated item information string is truncated.");
        }

        bytesRead = terminator + 1;
        return Encoding.UTF8.GetString(span[..terminator]);
    }
}
