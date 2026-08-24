// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Cur;
using SixLabors.ImageSharp.Formats.Ico;
using SixLabors.ImageSharp.Formats.Icon;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Ani;

/// <summary>
/// Performs ANI decoding and identification.
/// </summary>
internal sealed class AniDecoderCore : ImageDecoderCore, IDisposable
{
    private readonly List<(long Start, long End)> frameLists = new(1);
    private readonly ImageMetadata imageMetadata;
    private readonly AniMetadata aniMetadata;
    private AniHeader header;
    private IMemoryOwner<uint>? sequence;
    private IMemoryOwner<uint>? rates;

    /// <summary>
    /// Reusable storage for the fixed ANI header and smaller RIFF values.
    /// </summary>
    private InlineArray36<byte> buffer;

    /// <summary>
    /// Initializes a new instance of the <see cref="AniDecoderCore"/> class.
    /// </summary>
    /// <param name="options">The general decoder options.</param>
    public AniDecoderCore(DecoderOptions options)
        : base(options)
    {
        // The decoded ANI metadata must belong to the same ImageMetadata instance transferred to Image or ImageInfo.
        this.imageMetadata = new ImageMetadata();
        this.aniMetadata = this.imageMetadata.GetAniMetadata();
    }

    /// <inheritdoc/>
    protected override Image<TPixel> Decode<TPixel>(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        this.ParseContainer(stream);

        DecoderOptions frameOptions = this.CreateFrameDecoderOptions();
        List<(AniFrameFormat Format, Image<TPixel> Image)?> resources = [];
        List<ImageFrame<TPixel>> outputFrames = [];

        // Until Image accepts the frame collection, this method remains responsible for disposing every constructed output frame.
        bool outputFramesOwned = false;

        try
        {
            // Container parsing runs first because seq/rate chunks can occur after the frame list and affect how resources are projected.
            resources.EnsureCapacity((int)Math.Min(this.header.FrameCount, this.Options.MaxFrames));
            this.ProcessFrameChunks(stream, resources, (format, frameStream) =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                Image<TPixel> resource = DecodeFrame<TPixel>(format, frameOptions, frameStream, cancellationToken);
                this.Dimensions = new Size(Math.Max(this.Dimensions.Width, resource.Width), Math.Max(this.Dimensions.Height, resource.Height));

                return resource;
            });

            if (resources.Count is 0)
            {
                throw new InvalidImageContentException("The ANI file does not contain any frame resources.");
            }

            // Keep the owners alive and resolve their spans once; sequence and rate lookup occurs for every animation step.
            IMemoryOwner<uint>? sequenceOwner = this.sequence;
            bool hasSequence = sequenceOwner is not null;
            ReadOnlySpan<uint> sequence = sequenceOwner is null ? [] : sequenceOwner.GetSpan();
            ReadOnlySpan<uint> rates = this.rates is null ? [] : this.rates.GetSpan();
            int stepCount = hasSequence ? sequence.Length : resources.Count;
            int maxFrames = (int)this.Options.MaxFrames;
            outputFrames.EnsureCapacity(Math.Min(maxFrames, resources.Count));

            for (int step = 0; step < stepCount && outputFrames.Count < maxFrames; step++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                uint resourceIndex = hasSequence ? sequence[step] : (uint)step;
                if (resourceIndex >= resources.Count || resources[(int)resourceIndex] is not { } resource)
                {
                    // A bad ordering entry is recoverable ancillary data: the remaining valid steps can still be decoded.
                    this.ExecuteAncillarySegmentAction(() => throw new InvalidImageContentException("The ANI sequence references a missing frame resource."));

                    continue;
                }

                (AniFrameFormat format, Image<TPixel> resourceImage) = resource;
                uint frameDelay = step < rates.Length ? rates[step] : this.aniMetadata.DisplayRate;

                for (int i = 0; i < resourceImage.Frames.Count && outputFrames.Count < maxFrames; i++)
                {
                    ImageFrame<TPixel> source = resourceImage.Frames[i];
                    ImageFrame<TPixel> target = new(this.Options.Configuration, this.Dimensions);

                    // ANI flattens differently sized ICO/CUR variants into one ImageSharp frame collection.
                    // The common canvas preserves that invariant, while encoding dimensions retain the source size.
                    for (int y = 0; y < source.Height; y++)
                    {
                        source.PixelBuffer.DangerousGetRowSpan(y).CopyTo(target.PixelBuffer.DangerousGetRowSpan(y));
                    }

                    AniFrameMetadata metadata = CreateFrameMetadata(source.Metadata, format, step + 1, frameDelay, source.Size);
                    target.Metadata.SetFormatMetadata(AniFormat.Instance, metadata);
                    outputFrames.Add(target);
                }
            }

            if (outputFrames.Count is 0)
            {
                throw new InvalidImageContentException("The ANI file does not contain any decodable animation steps.");
            }

            // Image takes ownership of the supplied frames; only the temporary decoded resources remain locally owned.
            Image<TPixel> image = new(this.Options.Configuration, this.imageMetadata, outputFrames);
            outputFramesOwned = true;

            return image;
        }
        finally
        {
            // Embedded images are temporary resource containers; their pixels have already been copied to the flattened output frames.
            foreach ((AniFrameFormat Format, Image<TPixel> Image)? resource in resources)
            {
                if (resource is { } value)
                {
                    value.Image.Dispose();
                }
            }

            // Construction failures occur before Image can own the frames, so the partial collection must be released here.
            if (!outputFramesOwned)
            {
                foreach (ImageFrame<TPixel> frame in outputFrames)
                {
                    frame.Dispose();
                }
            }
        }
    }

    /// <inheritdoc/>
    protected override ImageInfo Identify(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        this.ParseContainer(stream);

        DecoderOptions frameOptions = this.CreateFrameDecoderOptions();
        List<(AniFrameFormat Format, ImageInfo Info)?> resources = [];
        resources.EnsureCapacity((int)Math.Min(this.header.FrameCount, this.Options.MaxFrames));
        this.ProcessFrameChunks(stream, resources, (format, frameStream) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            ImageInfo info = IdentifyFrame(format, frameOptions, frameStream, cancellationToken);
            this.Dimensions = new Size(Math.Max(this.Dimensions.Width, info.Width), Math.Max(this.Dimensions.Height, info.Height));

            return info;
        });

        if (resources.Count is 0)
        {
            throw new InvalidImageContentException("The ANI file does not contain any frame resources.");
        }

        // Identification mirrors decode without allocating pixels, while preserving the same step-to-resource projection.
        List<ImageFrameMetadata> outputFrames = [];
        IMemoryOwner<uint>? sequenceOwner = this.sequence;
        bool hasSequence = sequenceOwner is not null;
        ReadOnlySpan<uint> sequence = sequenceOwner is null ? [] : sequenceOwner.GetSpan();
        ReadOnlySpan<uint> rates = this.rates is null ? [] : this.rates.GetSpan();
        int stepCount = hasSequence ? sequence.Length : resources.Count;
        int maxFrames = (int)this.Options.MaxFrames;
        _ = outputFrames.EnsureCapacity(Math.Min(maxFrames, resources.Count));

        for (int step = 0; step < stepCount && outputFrames.Count < maxFrames; step++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            uint resourceIndex = hasSequence ? sequence[step] : (uint)step;
            if (resourceIndex >= resources.Count || resources[(int)resourceIndex] is not { } resource)
            {
                // Sequence errors are ancillary during identification for the same reason as decoding: other steps remain usable.
                this.ExecuteAncillarySegmentAction(() => throw new InvalidImageContentException("The ANI sequence references a missing frame resource."));

                continue;
            }

            (AniFrameFormat format, ImageInfo info) = resource;
            uint frameDelay = step < rates.Length ? rates[step] : this.aniMetadata.DisplayRate;

            if (info.FrameMetadataCollection.Count is 0)
            {
                // Some embedded decoders expose only resource-level dimensions, so synthesize the one required ANI frame entry.
                ImageFrameMetadata target = new();
                target.SetFormatMetadata(AniFormat.Instance, CreateFrameMetadata(null, format, step + 1, frameDelay, info.Size));
                outputFrames.Add(target);
                continue;
            }

            for (int i = 0; i < info.FrameMetadataCollection.Count && outputFrames.Count < maxFrames; i++)
            {
                ImageFrameMetadata source = info.FrameMetadataCollection[i];
                ImageFrameMetadata target = new();
                target.SetFormatMetadata(AniFormat.Instance, CreateFrameMetadata(source, format, step + 1, frameDelay, info.Size));
                outputFrames.Add(target);
            }
        }

        if (outputFrames.Count is 0)
        {
            throw new InvalidImageContentException("The ANI file does not contain any identifiable animation steps.");
        }

        return new ImageInfo(this.Dimensions, this.imageMetadata, outputFrames);
    }

    /// <summary>
    /// Parses the RIFF container and records frame-list boundaries for subsequent embedded decoding.
    /// </summary>
    /// <param name="stream">The ANI stream.</param>
    private void ParseContainer(BufferedReadStream stream)
    {
        // Parser-owned chunk state is replaced by the container currently being scanned.
        this.frameLists.Clear();
        this.sequence?.Dispose();
        this.rates?.Dispose();
        this.sequence = null;
        this.rates = null;

        long containerStart = stream.Position;
        Span<byte> riffHeader = this.buffer[..AniConstants.RiffHeaderSize];
        ReadExactly(stream, riffHeader, "RIFF header");

        if (!riffHeader[..4].SequenceEqual(AniConstants.RiffFourCc)
            || !riffHeader.Slice(8, 4).SequenceEqual(AniConstants.AniFormTypeFourCc))
        {
            throw new InvalidImageContentException("The stream does not contain an ANI RIFF container.");
        }

        uint declaredSize = BinaryPrimitives.ReadUInt32LittleEndian(riffHeader[4..]);
        if (declaredSize < sizeof(uint))
        {
            throw new InvalidImageContentException("The ANI RIFF container size is invalid.");
        }

        // RIFF size excludes the initial identifier and size field. Some real-world ANI files incorrectly
        // include those eight bytes, so the physical stream length remains the hard read boundary.
        long declaredEnd = checked(containerStart + 8 + declaredSize);
        long containerEnd = Math.Min(declaredEnd, stream.Length);
        bool headerFound = false;

        while (stream.Position + AniConstants.ChunkHeaderSize <= containerEnd)
        {
            AniRiffChunkHeader chunk = this.ReadChunkHeader(stream);
            long dataEnd = GetChunkDataEnd(stream, chunk.Size, containerEnd);

            switch ((AniChunkType)chunk.FourCc)
            {
                case AniChunkType.Header:
                    this.ReadAniHeader(stream, chunk.Size);
                    headerFound = true;
                    break;
                case AniChunkType.Sequence:
                    // Ordering and timing affect presentation, not pixel decoding, so malformed chunks follow ancillary handling.
                    this.ExecuteAncillarySegmentAction(() => this.ReadUInt32Values(stream, chunk.Size, "sequence", ref this.sequence));
                    break;
                case AniChunkType.Rate:
                    this.ExecuteAncillarySegmentAction(() => this.ReadUInt32Values(stream, chunk.Size, "rate", ref this.rates));
                    break;
                case AniChunkType.List:
                    this.ReadList(stream, dataEnd);
                    break;
            }

            stream.Position = GetPaddedEnd(dataEnd, chunk.Size, containerEnd);
        }

        if (!headerFound)
        {
            throw new InvalidImageContentException("The ANI file does not contain an animation header.");
        }
    }

    /// <summary>
    /// Parses the mandatory 36-byte ANI header and copies its observable values to image metadata.
    /// </summary>
    /// <param name="stream">The ANI stream.</param>
    /// <param name="chunkSize">The ANI header chunk size.</param>
    private void ReadAniHeader(BufferedReadStream stream, uint chunkSize)
    {
        if (chunkSize < AniHeader.Size)
        {
            throw new InvalidImageContentException("The ANI animation header is truncated.");
        }

        Span<byte> data = this.buffer;
        ReadExactly(stream, data, "ANI header");
        this.header = AniHeader.Parse(data);

        if (this.header.BytesInHeader < AniHeader.Size || this.header.BytesInHeader > chunkSize)
        {
            throw new InvalidImageContentException("The ANI animation header declares an invalid size.");
        }

        this.aniMetadata.Width = this.header.Width;
        this.aniMetadata.Height = this.header.Height;
        this.aniMetadata.BitCount = this.header.BitCount;
        this.aniMetadata.Planes = this.header.Planes;
        this.aniMetadata.DisplayRate = this.header.DisplayRate;
        this.aniMetadata.Flags = this.header.Flags;
    }

    /// <summary>
    /// Reads a RIFF list type and records or parses its contents.
    /// </summary>
    /// <param name="stream">The ANI stream.</param>
    /// <param name="listEnd">The exclusive end of the list payload.</param>
    private void ReadList(BufferedReadStream stream, long listEnd)
    {
        if (listEnd - stream.Position < sizeof(uint))
        {
            throw new InvalidImageContentException("The ANI file contains a truncated RIFF list.");
        }

        Span<byte> typeData = this.buffer[..sizeof(uint)];
        ReadExactly(stream, typeData, "RIFF list type");
        AniListType type = (AniListType)BinaryPrimitives.ReadUInt32LittleEndian(typeData);

        switch (type)
        {
            case AniListType.Frames:
                // Defer nested decoding until the complete container has supplied any later seq/rate chunks.
                this.frameLists.Add((stream.Position, listEnd));
                break;
            case AniListType.Info when !this.Options.SkipMetadata:
                this.ExecuteAncillarySegmentAction(() => this.ReadInfoList(stream, listEnd));
                break;
        }
    }

    /// <summary>
    /// Parses the optional ANI name and artist information.
    /// </summary>
    /// <param name="stream">The ANI stream.</param>
    /// <param name="listEnd">The exclusive end of the information list.</param>
    private void ReadInfoList(BufferedReadStream stream, long listEnd)
    {
        // INAM and IART are consumed sequentially, so one grow-only buffer covers every text chunk in the list.
        IMemoryOwner<byte>? textOwner = null;

        try
        {
            while (stream.Position + AniConstants.ChunkHeaderSize <= listEnd)
            {
                AniRiffChunkHeader chunk = this.ReadChunkHeader(stream);
                long dataEnd = GetChunkDataEnd(stream, chunk.Size, listEnd);

                switch ((AniInfoChunkType)chunk.FourCc)
                {
                    case AniInfoChunkType.Name:
                        if (this.TryReadText(stream, chunk.Size, ref textOwner, out string? name))
                        {
                            this.aniMetadata.Name = name;
                        }

                        break;
                    case AniInfoChunkType.Artist:
                        if (this.TryReadText(stream, chunk.Size, ref textOwner, out string? artist))
                        {
                            this.aniMetadata.Artist = artist;
                        }

                        break;
                }

                stream.Position = GetPaddedEnd(dataEnd, chunk.Size, listEnd);
            }
        }
        finally
        {
            textOwner?.Dispose();
        }
    }

    /// <summary>
    /// Reads a sequence or rate chunk into reusable allocator-owned memory.
    /// </summary>
    /// <param name="stream">The ANI stream.</param>
    /// <param name="chunkSize">The chunk payload size.</param>
    /// <param name="description">The chunk description used in error messages.</param>
    /// <param name="owner">The buffer to reuse or replace.</param>
    private void ReadUInt32Values(BufferedReadStream stream, uint chunkSize, string description, ref IMemoryOwner<uint>? owner)
    {
        // seq and rate payloads are DWORD arrays; trailing bytes cannot form a valid entry.
        if (chunkSize % sizeof(uint) is not 0)
        {
            this.ThrowOrIgnoreNonStrictSegmentError($"The ANI {description} chunk has an invalid size.");
            return;
        }

        // MaxFrames controls retained animation steps, but its default is intentionally unbounded. Apply a separate
        // byte limit before allocation so an oversized control chunk follows ancillary integrity handling.
        if (chunkSize > AniConstants.MaxAncillaryChunkSize)
        {
            this.ThrowOrIgnoreNonStrictSegmentError($"The ANI {description} chunk is too large.");
            return;
        }

        int count = (int)Math.Min(chunkSize / sizeof(uint), this.Options.MaxFrames);
        if (count is 0)
        {
            this.ThrowOrIgnoreNonStrictSegmentError($"The ANI {description} chunk does not contain any values.");
            return;
        }

        IMemoryOwner<uint> valuesOwner;
        bool replaceOwner;

        // Duplicate chunks can overwrite an equal-sized allocation. A different size uses a replacement so a failed read
        // leaves the last valid chunk available to non-strict decoding.
        if (owner is not null && owner.GetSpan().Length == count)
        {
            valuesOwner = owner;
            replaceOwner = false;
        }
        else
        {
            valuesOwner = this.Options.Configuration.MemoryAllocator.Allocate<uint>(count);
            replaceOwner = true;
        }

        bool success = false;

        // A newly allocated replacement is not published until the entire payload has been read and normalized.
        try
        {
            Span<uint> values = valuesOwner.GetSpan()[..count];
            Span<byte> data = MemoryMarshal.AsBytes(values);
            if (stream.Read(data) != data.Length)
            {
                this.ThrowOrIgnoreNonStrictSegmentError($"Not enough bytes to read the ANI {description} chunk.");
                return;
            }

            if (!BitConverter.IsLittleEndian)
            {
                // RIFF integers are always little-endian; normalize once here so hot step loops use native uint indexing.
                for (int i = 0; i < values.Length; i++)
                {
                    values[i] = BinaryPrimitives.ReverseEndianness(values[i]);
                }
            }

            success = true;
        }
        finally
        {
            if (!success && replaceOwner)
            {
                valuesOwner.Dispose();
            }
        }

        if (replaceOwner)
        {
            owner?.Dispose();
            owner = valuesOwner;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.sequence?.Dispose();
        this.rates?.Dispose();
        this.sequence = null;
        this.rates = null;
    }

    /// <summary>
    /// Tries to read a null-terminated ANI information string.
    /// </summary>
    /// <param name="stream">The ANI stream.</param>
    /// <param name="chunkSize">The text chunk payload size.</param>
    /// <param name="owner">The reusable text buffer.</param>
    /// <param name="value">The decoded ASCII text when successful.</param>
    /// <returns><see langword="true"/> when the text was read successfully; otherwise, <see langword="false"/>.</returns>
    private bool TryReadText(BufferedReadStream stream, uint chunkSize, ref IMemoryOwner<byte>? owner, out string? value)
    {
        value = null;

        // INFO text is optional metadata. Reject or skip oversized values before renting their backing buffer.
        if (chunkSize > AniConstants.MaxAncillaryChunkSize)
        {
            this.ThrowOrIgnoreNonStrictSegmentError("The ANI information text chunk is too large.");
            return false;
        }

        int length = (int)chunkSize;

        // Retain the largest text buffer encountered because INFO values are decoded one at a time.
        if (owner is null || owner.GetSpan().Length < length)
        {
            owner?.Dispose();
            owner = this.Options.Configuration.MemoryAllocator.Allocate<byte>(length);
        }

        Span<byte> data = owner.GetSpan()[..length];
        if (stream.Read(data) != data.Length)
        {
            this.ThrowOrIgnoreNonStrictSegmentError("Not enough bytes to read the ANI information text.");
            return false;
        }

        // RIFF text is null-terminated, but the declared chunk may include bytes after the first terminator.
        int terminator = data.IndexOf((byte)0);
        value = Encoding.ASCII.GetString(terminator < 0 ? data : data[..terminator]);
        return true;
    }

    /// <summary>
    /// Processes each embedded frame-resource chunk without allowing its decoder to read adjacent RIFF data.
    /// </summary>
    /// <typeparam name="T">The parsed resource type.</typeparam>
    /// <param name="stream">The ANI stream.</param>
    /// <param name="resources">The destination resource slots.</param>
    /// <param name="action">The operation to perform for each resource format and bounded stream.</param>
    private void ProcessFrameChunks<T>(BufferedReadStream stream, List<(AniFrameFormat Format, T Resource)?> resources, Func<AniFrameFormat, Stream, T> action)
        where T : class
    {
        // Child decoding is synchronous, so one bounded stream object can be repositioned for every physical resource.
        AniFrameStream frameStream = new(stream);
        ReadOnlySpan<uint> sequence = this.sequence is null ? [] : this.sequence.GetSpan();
        bool hasSequence = this.sequence is not null;
        int decodedResourceCount = 0;
        int maxDecodedResources = (int)this.Options.MaxFrames;
        IMemoryOwner<uint>? sortedSequenceOwner = null;

        try
        {
            ReadOnlySpan<uint> requiredResources = sequence;
            if (hasSequence)
            {
                bool isSorted = true;
                for (int i = 1; i < sequence.Length; i++)
                {
                    if (sequence[i] < sequence[i - 1])
                    {
                        isSorted = false;
                        break;
                    }
                }

                if (!isSorted)
                {
                    // Playback order can reference resources arbitrarily. A sorted allocator-owned copy turns the physical
                    // resource scan into a linear merge instead of searching the complete sequence for every icon chunk.
                    sortedSequenceOwner = this.Options.Configuration.MemoryAllocator.Allocate<uint>(sequence.Length);
                    Span<uint> sortedSequence = sortedSequenceOwner.GetSpan();
                    sequence.CopyTo(sortedSequence);
                    sortedSequence.Sort();
                    requiredResources = sortedSequence;
                }
            }

            int requiredResourceIndex = 0;
            uint lastRequiredResource = hasSequence ? requiredResources[^1] : 0;

            foreach ((long start, long end) in this.frameLists)
            {
                stream.Position = start;

                while (stream.Position + AniConstants.ChunkHeaderSize <= end)
                {
                    AniRiffChunkHeader chunk = this.ReadChunkHeader(stream);
                    long dataStart = stream.Position;
                    long dataEnd = GetChunkDataEnd(stream, chunk.Size, end);

                    if ((AniFrameChunkType)chunk.FourCc is AniFrameChunkType.Icon)
                    {
                        int resourceIndex = resources.Count;

                        // Sequence entries index the physical resource table, so ignored corrupt resources retain an empty slot.
                        resources.Add(null);

                        if (hasSequence)
                        {
                            while (requiredResourceIndex < requiredResources.Length && requiredResources[requiredResourceIndex] < (uint)resourceIndex)
                            {
                                requiredResourceIndex++;
                            }
                        }

                        // Unsequenced resources are consumed in physical order; sequenced files need only the referenced indices.
                        bool shouldDecode = !hasSequence
                            || (requiredResourceIndex < requiredResources.Length && requiredResources[requiredResourceIndex] == (uint)resourceIndex);

                        if (shouldDecode)
                        {
                            this.ExecuteImageDataSegmentAction(() =>
                            {
                                // Child decoders may seek according to embedded offsets; the bounded view prevents crossing the icon chunk.
                                frameStream.Reset(dataStart, chunk.Size);
                                AniFrameFormat format = this.GetFrameFormat(frameStream);

                                // Format probing consumes the directory prefix, while the selected child decoder requires the complete resource.
                                frameStream.Position = 0;
                                resources[resourceIndex] = (format, action(format, frameStream));
                            });

                            if (resources[resourceIndex] is not null)
                            {
                                decodedResourceCount++;
                            }
                        }

                        // Every decoded resource contributes at least one output frame, while a sequence cannot reference later indices.
                        if ((!hasSequence && decodedResourceCount == maxDecodedResources)
                            || (hasSequence && (uint)resourceIndex == lastRequiredResource))
                        {
                            return;
                        }
                    }

                    stream.Position = GetPaddedEnd(dataEnd, chunk.Size, end);
                }
            }
        }
        finally
        {
            sortedSequenceOwner?.Dispose();
        }
    }

    /// <summary>
    /// Determines the embedded resource format from the ANI header and ICO/CUR directory prefix.
    /// </summary>
    /// <param name="stream">The bounded frame-resource stream.</param>
    /// <returns>The embedded resource format.</returns>
    private AniFrameFormat GetFrameFormat(Stream stream)
    {
        // Without AF_ICON, the icon chunk payload is a raw DIB and has no ICO/CUR directory prefix to inspect.
        if (!this.header.Flags.HasFlag(AniHeaderFlags.IsIcon))
        {
            return AniFrameFormat.Bmp;
        }

        Span<byte> iconHeader = this.buffer[..AniConstants.IconDirHeaderSize];
        if (stream.Read(iconHeader) != iconHeader.Length)
        {
            throw new InvalidImageContentException("The ANI file contains a truncated ICO or CUR resource.");
        }

        IconFileType type = (IconFileType)BinaryPrimitives.ReadUInt16LittleEndian(iconHeader[2..]);
        return type switch
        {
            IconFileType.ICO => AniFrameFormat.Ico,
            IconFileType.CUR => AniFrameFormat.Cur,
            _ => throw new InvalidImageContentException("The ANI file contains an unsupported icon resource.")
        };
    }

    /// <summary>
    /// Decodes one embedded ANI frame resource.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel type.</typeparam>
    /// <param name="format">The embedded resource format.</param>
    /// <param name="options">The nested decoder options.</param>
    /// <param name="stream">The bounded resource stream.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The decoded resource.</returns>
    private static Image<TPixel> DecodeFrame<TPixel>(AniFrameFormat format, DecoderOptions options, Stream stream, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
        => format switch
        {
            AniFrameFormat.Ico => new IcoDecoderCore(options).Decode<TPixel>(options.Configuration, stream, cancellationToken),
            AniFrameFormat.Cur => new CurDecoderCore(options).Decode<TPixel>(options.Configuration, stream, cancellationToken),
            AniFrameFormat.Bmp => new BmpDecoderCore(new BmpDecoderOptions
            {
                GeneralOptions = options,
                SkipFileHeader = true
            }).Decode<TPixel>(options.Configuration, stream, cancellationToken),
            _ => throw new InvalidImageContentException("The ANI file contains an unsupported frame format.")
        };

    /// <summary>
    /// Identifies one embedded ANI frame resource.
    /// </summary>
    /// <param name="format">The embedded resource format.</param>
    /// <param name="options">The nested decoder options.</param>
    /// <param name="stream">The bounded resource stream.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The identified resource.</returns>
    private static ImageInfo IdentifyFrame(AniFrameFormat format, DecoderOptions options, Stream stream, CancellationToken cancellationToken)
        => format switch
        {
            AniFrameFormat.Ico => new IcoDecoderCore(options).Identify(options.Configuration, stream, cancellationToken),
            AniFrameFormat.Cur => new CurDecoderCore(options).Identify(options.Configuration, stream, cancellationToken),
            AniFrameFormat.Bmp => new BmpDecoderCore(new BmpDecoderOptions
            {
                GeneralOptions = options,
                SkipFileHeader = true
            }).Identify(options.Configuration, stream, cancellationToken),
            _ => throw new InvalidImageContentException("The ANI file contains an unsupported frame format.")
        };

    /// <summary>
    /// Creates ANI metadata for one flattened output frame.
    /// </summary>
    /// <param name="source">The embedded frame metadata, when available.</param>
    /// <param name="format">The embedded resource format.</param>
    /// <param name="sequenceNumber">The animation sequence number.</param>
    /// <param name="frameDelay">The display rate in sixtieths of a second.</param>
    /// <param name="size">The embedded frame size.</param>
    /// <returns>The ANI frame metadata.</returns>
    private static AniFrameMetadata CreateFrameMetadata(ImageFrameMetadata? source, AniFrameFormat format, int sequenceNumber, uint frameDelay, Size size)
    {
        AniFrameMetadata metadata = new()
        {
            FrameDelay = frameDelay,
            SequenceNumber = sequenceNumber,
            FrameFormat = format
        };

        if (source is null)
        {
            metadata.EncodingWidth = NarrowDimension(size.Width);
            metadata.EncodingHeight = NarrowDimension(size.Height);

            return metadata;
        }

        // ColorTable is managed read-only memory and remains valid after the temporary child image is disposed,
        // so the flattened metadata can retain the same view without cloning its backing array.
        switch (format)
        {
            case AniFrameFormat.Ico:
                IcoFrameMetadata icoMetadata = source.GetIcoMetadata();
                metadata.EncodingWidth = icoMetadata.EncodingWidth;
                metadata.EncodingHeight = icoMetadata.EncodingHeight;
                metadata.Compression = icoMetadata.Compression;
                metadata.BmpBitsPerPixel = icoMetadata.BmpBitsPerPixel;
                metadata.ColorTable = icoMetadata.ColorTable;

                break;
            case AniFrameFormat.Cur:
                CurFrameMetadata curMetadata = source.GetCurMetadata();
                metadata.EncodingWidth = curMetadata.EncodingWidth;
                metadata.EncodingHeight = curMetadata.EncodingHeight;
                metadata.Compression = curMetadata.Compression;
                metadata.BmpBitsPerPixel = curMetadata.BmpBitsPerPixel;
                metadata.HotspotX = curMetadata.HotspotX;
                metadata.HotspotY = curMetadata.HotspotY;
                metadata.ColorTable = curMetadata.ColorTable;

                break;
            case AniFrameFormat.Bmp:
                metadata.EncodingWidth = NarrowDimension(size.Width);
                metadata.EncodingHeight = NarrowDimension(size.Height);
                break;
        }

        return metadata;
    }

    /// <summary>
    /// Creates decoder options for embedded resources without applying the outer ANI resize twice.
    /// </summary>
    /// <returns>The embedded frame decoder options.</returns>
    private DecoderOptions CreateFrameDecoderOptions()
        => new()
        {
            Configuration = this.Options.Configuration,
            MaxFrames = this.Options.MaxFrames,
            SkipMetadata = this.Options.SkipMetadata,
            SegmentIntegrityHandling = this.Options.SegmentIntegrityHandling,
            ColorProfileHandling = this.Options.ColorProfileHandling
        };

    /// <summary>
    /// Reads one fixed-size RIFF chunk header.
    /// </summary>
    /// <param name="stream">The ANI stream.</param>
    /// <returns>The parsed chunk header.</returns>
    private AniRiffChunkHeader ReadChunkHeader(BufferedReadStream stream)
    {
        Span<byte> data = this.buffer[..AniConstants.ChunkHeaderSize];
        ReadExactly(stream, data, "RIFF chunk header");
        return AniRiffChunkHeader.Parse(data);
    }

    /// <summary>
    /// Calculates and validates the exclusive end of a RIFF chunk payload.
    /// </summary>
    /// <param name="stream">The ANI stream.</param>
    /// <param name="size">The declared payload size.</param>
    /// <param name="containerEnd">The exclusive parent-container boundary.</param>
    /// <returns>The exclusive payload boundary.</returns>
    private static long GetChunkDataEnd(BufferedReadStream stream, uint size, long containerEnd)
    {
        long end = checked(stream.Position + size);
        if (end > containerEnd)
        {
            throw new InvalidImageContentException("An ANI RIFF chunk extends beyond its containing list.");
        }

        return end;
    }

    /// <summary>
    /// Calculates and validates the word-aligned end of a RIFF chunk.
    /// </summary>
    /// <param name="dataEnd">The exclusive payload boundary.</param>
    /// <param name="size">The declared payload size.</param>
    /// <param name="containerEnd">The exclusive parent-container boundary.</param>
    /// <returns>The exclusive padded chunk boundary.</returns>
    private static long GetPaddedEnd(long dataEnd, uint size, long containerEnd)
    {
        // RIFF aligns each chunk to a 16-bit boundary without including the optional pad byte in the declared size.
        long paddedEnd = dataEnd + (size & 1);
        if (paddedEnd > containerEnd)
        {
            throw new InvalidImageContentException("An ANI RIFF chunk is missing its alignment padding.");
        }

        return paddedEnd;
    }

    /// <summary>
    /// Reads an exact number of bytes or reports a truncated ANI file.
    /// </summary>
    /// <param name="stream">The ANI stream.</param>
    /// <param name="destination">The destination buffer.</param>
    /// <param name="description">The data description used in the error message.</param>
    private static void ReadExactly(BufferedReadStream stream, Span<byte> destination, string description)
    {
        if (stream.Read(destination) != destination.Length)
        {
            throw new InvalidImageContentException($"Not enough bytes to read the {description}.");
        }
    }

    /// <summary>
    /// Converts a pixel dimension to the one-byte ICO/CUR representation.
    /// </summary>
    /// <param name="value">The pixel dimension.</param>
    /// <returns>The encoded dimension, where zero represents 256 pixels or greater.</returns>
    private static byte NarrowDimension(int value) => value > byte.MaxValue ? (byte)0 : (byte)value;
}
