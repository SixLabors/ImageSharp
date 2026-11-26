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

    /// <summary>
    /// Selects the integrity policy for a chunk that extends past the end of its parent container or list.
    /// </summary>
    private enum ChunkBoundPolicy
    {
        /// <summary>
        /// The parent holds image data, and the decoder did not decode a frame resource yet. For a chunk that extends past the parent, the decoder throws in
        /// each mode, because it has no pixels to keep.
        /// </summary>
        ImageDataBeforeResource,

        /// <summary>
        /// The parent holds image data, and the decoder decoded at least one frame resource. The image data mode controls the error.
        /// </summary>
        ImageData,

        /// <summary>
        /// The parent holds ancillary data. The ancillary mode controls the error.
        /// </summary>
        Ancillary
    }

    /// <summary>
    /// Gets a value indicating whether the frames are raw bitmaps. Without AF_ICON, each icon chunk holds a raw DIB with
    /// no ICO or CUR directory.
    /// </summary>
    /// <remarks>
    /// The ACON specification defines each icon chunk as an embedded .CUR file and does not define raw frames. See the
    /// Multimedia Data Standards Update, April 15, 1994, page 9:
    /// https://www.mmsp.ece.mcgill.ca/Documents/AudioFormats/WAVE/Docs/RIFFNEW.pdf
    /// A raw frame holds the bitmap of an icon image: the color data, then the AND mask. The height covers both.
    /// The decoder reports each raw frame as a CUR frame that holds a bitmap, as the encoder writes it.
    /// </remarks>
    private bool HasRawFrames => !this.header.Flags.HasFlag(AniHeaderFlags.IsIcon);

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
            this.ProcessFrameChunks(stream, resources, (format, frameStream) =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                Image<TPixel> resource = this.DecodeFrame<TPixel>(format, frameOptions, frameStream, cancellationToken);
                this.Dimensions = new Size(Math.Max(this.Dimensions.Width, resource.Width), Math.Max(this.Dimensions.Height, resource.Height));

                return resource;
            });

            // The decoder gets the spans one time, because the sequence and rate lookup occurs for each animation step. A file without a resource adds no
            // frame, so the check after the loop throws.
            ReadOnlySpan<uint> sequence = this.sequence is null ? [] : this.sequence.GetSpan();
            ReadOnlySpan<uint> rates = this.rates is null ? [] : this.rates.GetSpan();
            int stepCount = sequence.IsEmpty ? resources.Count : sequence.Length;
            int maxFrames = (int)this.Options.MaxFrames;
            outputFrames.EnsureCapacity(Math.Min(maxFrames, resources.Count));

            for (int step = 0; step < stepCount && outputFrames.Count < maxFrames; step++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!this.TryGetStep(resources, sequence, rates, step, out int resourceIndex, out uint frameDelay))
                {
                    continue;
                }

                (AniFrameFormat format, Image<TPixel> resourceImage) = resources[resourceIndex].GetValueOrDefault();

                for (int i = 0; i < resourceImage.Frames.Count && outputFrames.Count < maxFrames; i++)
                {
                    ImageFrame<TPixel> source = resourceImage.Frames[i];

                    // Clone resource metadata for every playback step so repeated frames retain independent profiles and format metadata.
                    ImageFrameMetadata frameMetadata = source.Metadata.DeepClone();

                    if (this.HasRawFrames)
                    {
                        // Raw BMP stores ICC on the image. ICO/CUR frames already carry their own profiles;
                        // using their image-level fallback would copy the first entry's profile onto unprofiled siblings.
                        frameMetadata.IccProfile ??= resourceImage.Metadata.IccProfile?.DeepClone();
                    }

                    ImageFrame<TPixel> target = new(this.Options.Configuration, this.Dimensions, frameMetadata);

                    // ANI flattens differently sized ICO/CUR variants into one ImageSharp frame collection.
                    // The common canvas preserves that invariant, while encoding dimensions retain the source size.
                    for (int y = 0; y < source.Height; y++)
                    {
                        source.PixelBuffer.DangerousGetRowSpan(y).CopyTo(target.PixelBuffer.DangerousGetRowSpan(y));
                    }

                    BmpMetadata? bitmapMetadata = this.HasRawFrames ? resourceImage.Metadata.GetBmpMetadata() : null;

                    // Steps that play the same stored frame get the same sequence number, so the encoder stores that frame once.
                    AniFrameMetadata metadata = CreateFrameMetadata(source.Metadata, format, resourceIndex + 1, frameDelay, source.Size, bitmapMetadata);
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
        this.ProcessFrameChunks(stream, resources, (format, frameStream) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            ImageInfo info = this.IdentifyFrame(format, frameOptions, frameStream, cancellationToken);
            this.Dimensions = new Size(Math.Max(this.Dimensions.Width, info.Width), Math.Max(this.Dimensions.Height, info.Height));

            return info;
        });

        // Identify uses the same step-to-resource projection as Decode, but it does not allocate pixels. A file without a resource adds no frame, so the check
        // after the loop throws.
        List<ImageFrameMetadata> outputFrames = [];
        ReadOnlySpan<uint> sequence = this.sequence is null ? [] : this.sequence.GetSpan();
        ReadOnlySpan<uint> rates = this.rates is null ? [] : this.rates.GetSpan();
        int stepCount = sequence.IsEmpty ? resources.Count : sequence.Length;
        int maxFrames = (int)this.Options.MaxFrames;
        _ = outputFrames.EnsureCapacity(Math.Min(maxFrames, resources.Count));

        for (int step = 0; step < stepCount && outputFrames.Count < maxFrames; step++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!this.TryGetStep(resources, sequence, rates, step, out int resourceIndex, out uint frameDelay))
            {
                continue;
            }

            (AniFrameFormat format, ImageInfo info) = resources[resourceIndex].GetValueOrDefault();

            if (this.HasRawFrames)
            {
                // Raw BMP Identify supplies image-level metadata without a frame collection. Retain its profile on the ANI frame.
                ImageFrameMetadata target = new() { IccProfile = info.Metadata.IccProfile?.DeepClone() };
                target.SetFormatMetadata(
                    AniFormat.Instance,
                    CreateFrameMetadata(null, format, resourceIndex + 1, frameDelay, info.Size, info.Metadata.GetBmpMetadata()));
                outputFrames.Add(target);
                continue;
            }

            for (int i = 0; i < info.FrameMetadataCollection.Count && outputFrames.Count < maxFrames; i++)
            {
                ImageFrameMetadata source = info.FrameMetadataCollection[i];
                ImageFrameMetadata target = source.DeepClone();

                target.SetFormatMetadata(AniFormat.Instance, CreateFrameMetadata(source, format, resourceIndex + 1, frameDelay, info.Size, null));
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
    /// Resolves one animation step to the frame resource that it plays and to its display rate.
    /// </summary>
    /// <typeparam name="T">The resource type.</typeparam>
    /// <param name="resources">The resource slots in physical order. A slot is empty if the decoder did not read its resource.</param>
    /// <param name="sequence">The values of the sequence chunk, or an empty span if the file has no sequence chunk.</param>
    /// <param name="rates">The values of the rate chunk, or an empty span if the file has no rate chunk.</param>
    /// <param name="step">The step index.</param>
    /// <param name="resourceIndex">The index of the slot that the step plays. If the method returns <see langword="true"/>, the slot holds a resource.</param>
    /// <param name="frameDelay">The display rate of the step in sixtieths of a second.</param>
    /// <returns>
    /// <see langword="true"/> if the step plays a resource that the decoder read. <see langword="false"/> if the step refers to a missing resource and the mode
    /// ignores ancillary errors.
    /// </returns>
    private bool TryGetStep<T>(
        List<(AniFrameFormat Format, T Resource)?> resources,
        ReadOnlySpan<uint> sequence,
        ReadOnlySpan<uint> rates,
        int step,
        out int resourceIndex,
        out uint frameDelay)
    {
        // If the file has no sequence chunk, step n plays resource n. A sequence chunk always holds at least one value.
        uint index = sequence.IsEmpty ? (uint)step : sequence[step];
        frameDelay = step < rates.Length ? rates[step] : this.aniMetadata.DisplayRate;
        resourceIndex = (int)index;

        if (index >= (uint)resources.Count || !resources[resourceIndex].HasValue)
        {
            // A bad sequence entry is recoverable ancillary data. The decoder can still decode the other valid steps.
            this.Options.SegmentIntegrityHandling.ThrowOrIgnoreAncillaryError("The ANI sequence references a missing frame resource.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Parses the RIFF container. It reads the header, the sequence, the rates and the information list. It records the bounds of each frame list for the
    /// embedded decoding that follows.
    /// </summary>
    /// <param name="stream">The ANI stream.</param>
    private void ParseContainer(BufferedReadStream stream)
    {
        long containerStart = stream.Position;
        Span<byte> riffHeader = this.buffer[..AniConstants.RiffHeaderSize];
        if (stream.Read(riffHeader) != riffHeader.Length)
        {
            throw new InvalidImageContentException("Not enough bytes to read the RIFF header.");
        }

        if (!riffHeader[..4].SequenceEqual(AniConstants.RiffFourCc)
            || !riffHeader.Slice(8, 4).SequenceEqual(AniConstants.AniFormTypeFourCc))
        {
            throw new InvalidImageContentException("The stream does not contain an ANI RIFF container.");
        }

        // The RIFF size does not include the identifier and the size field at the start of the file. Some real-world ANI files include those eight bytes in
        // error. Thus the physical stream length stays the hard read boundary. A size less than 12 leaves no room for the first chunk header, so the
        // header check below throws. The full header read puts containerStart + 8 inside the stream. Thus the remaining length cannot be negative, and the
        // declared end is added only when it is less than the stream length.
        uint declaredSize = BinaryPrimitives.ReadUInt32LittleEndian(riffHeader[4..]);
        long remaining = stream.Length - containerStart - 8;
        long containerEnd = declaredSize < remaining ? containerStart + 8 + declaredSize : stream.Length;
        long position = stream.Position;
        bool headerFound = false;

        while (this.TryReadChunk(stream, ref position, containerEnd, ChunkBoundPolicy.ImageDataBeforeResource, out AniRiffChunkHeader chunk))
        {
            switch ((AniChunkType)chunk.FourCc)
            {
                case AniChunkType.Header:
                    this.ReadAniHeader(stream, chunk.Size);
                    headerFound = true;
                    break;
                case AniChunkType.Sequence:
                    // Ordering and timing affect presentation, not pixel decoding, so malformed chunks follow ancillary handling.
                    this.ReadUInt32Values(stream, chunk.Size, "sequence", ref this.sequence);
                    break;
                case AniChunkType.Rate:
                    this.ReadUInt32Values(stream, chunk.Size, "rate", ref this.rates);
                    break;
                case AniChunkType.List:
                    this.ReadList(stream, stream.Position + chunk.Size);
                    break;
            }
        }

        if (!headerFound)
        {
            throw new InvalidImageContentException("The ANI file does not contain an animation header.");
        }
    }

    /// <summary>
    /// Parses the mandatory 36-byte ANI header and copies its observable values to the image metadata.
    /// </summary>
    /// <param name="stream">The ANI stream.</param>
    /// <param name="chunkSize">The size of the ANI header chunk payload that the stream holds.</param>
    private void ReadAniHeader(BufferedReadStream stream, uint chunkSize)
    {
        if (chunkSize < AniHeader.Size)
        {
            throw new InvalidImageContentException("The ANI animation header is truncated.");
        }

        // The check above and the RIFF walk prove that the stream holds the header.
        Span<byte> data = this.buffer;
        stream.Read(data);
        this.header = AniHeader.Parse(data);

        if (this.header.BytesInHeader < AniHeader.Size || this.header.BytesInHeader > chunkSize)
        {
            throw new InvalidImageContentException("The ANI animation header declares an invalid size.");
        }

        this.aniMetadata.DisplayRate = this.header.DisplayRate;
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

        // The check above proves that the stream holds the list type.
        Span<byte> typeData = this.buffer[..sizeof(uint)];
        stream.Read(typeData);
        AniListType type = (AniListType)BinaryPrimitives.ReadUInt32LittleEndian(typeData);

        switch (type)
        {
            case AniListType.Frames:
                // Defer nested decoding until the complete container has supplied any later seq/rate chunks.
                this.frameLists.Add((stream.Position, listEnd));
                break;
            case AniListType.Info when !this.Options.SkipMetadata:
                this.ReadInfoList(stream, listEnd);
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
        long position = stream.Position;

        try
        {
            while (this.TryReadChunk(stream, ref position, listEnd, ChunkBoundPolicy.Ancillary, out AniRiffChunkHeader chunk))
            {
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
            }
        }
        finally
        {
            textOwner?.Dispose();
        }
    }

    /// <summary>
    /// Reads a sequence chunk or a rate chunk into reusable memory that the allocator owns.
    /// </summary>
    /// <param name="stream">The ANI stream.</param>
    /// <param name="chunkSize">The size of the chunk payload that the stream holds.</param>
    /// <param name="description">The chunk description that the error messages use.</param>
    /// <param name="owner">The buffer to reuse or replace.</param>
    private void ReadUInt32Values(BufferedReadStream stream, uint chunkSize, string description, ref IMemoryOwner<uint>? owner)
    {
        // seq and rate payloads are DWORD arrays; trailing bytes cannot form a valid entry.
        if (chunkSize % sizeof(uint) is not 0)
        {
            this.Options.SegmentIntegrityHandling.ThrowOrIgnoreAncillaryError($"The ANI {description} chunk has an invalid size.");
            return;
        }

        // MaxFrames controls retained animation steps, but its default is intentionally unbounded. Apply a separate
        // byte limit before allocation so an oversized control chunk follows ancillary integrity handling.
        if (chunkSize > AniConstants.MaxAncillaryChunkSize)
        {
            this.Options.SegmentIntegrityHandling.ThrowOrIgnoreAncillaryError($"The ANI {description} chunk is too large.");
            return;
        }

        int count = (int)Math.Min(chunkSize / sizeof(uint), this.Options.MaxFrames);
        if (count is 0)
        {
            this.Options.SegmentIntegrityHandling.ThrowOrIgnoreAncillaryError($"The ANI {description} chunk does not contain any values.");
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
            // The RIFF walk proves that the stream holds the chunk payload.
            Span<uint> values = valuesOwner.GetSpan()[..count];
            stream.Read(MemoryMarshal.AsBytes(values));

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
    /// <param name="chunkSize">The size of the text chunk payload that the stream holds.</param>
    /// <param name="owner">The reusable text buffer.</param>
    /// <param name="value">The decoded ASCII text, if the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> if the method read the text. Otherwise, <see langword="false"/>.</returns>
    private bool TryReadText(BufferedReadStream stream, uint chunkSize, ref IMemoryOwner<byte>? owner, out string? value)
    {
        value = null;

        // INFO text is optional metadata. Reject or skip oversized values before renting their backing buffer.
        if (chunkSize > AniConstants.MaxAncillaryChunkSize)
        {
            this.Options.SegmentIntegrityHandling.ThrowOrIgnoreAncillaryError("The ANI information text chunk is too large.");
            return false;
        }

        int length = (int)chunkSize;

        // Retain the largest text buffer encountered because INFO values are decoded one at a time.
        if (owner is null || owner.GetSpan().Length < length)
        {
            owner?.Dispose();
            owner = this.Options.Configuration.MemoryAllocator.Allocate<byte>(length);
        }

        // The RIFF walk proves that the stream holds the chunk payload.
        Span<byte> data = owner.GetSpan()[..length];
        stream.Read(data);

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
        IconFrameStream frameStream = new(stream);
        ReadOnlySpan<uint> sequence = this.sequence is null ? [] : this.sequence.GetSpan();
        int decodedResourceCount = 0;
        int maxDecodedResources = (int)this.Options.MaxFrames;
        IMemoryOwner<uint>? sortedSequenceOwner = null;

        try
        {
            ReadOnlySpan<uint> requiredResources = sequence;
            if (!sequence.IsEmpty)
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
            uint lastRequiredResource = sequence.IsEmpty ? 0 : requiredResources[^1];

            foreach ((long start, long end) in this.frameLists)
            {
                long position = start;

                // Before the first resource decodes, the decoder has no pixels to keep. Thus, until then, the decoder throws in each mode for a chunk that
                // extends past the list.
                while (this.TryReadChunk(
                    stream,
                    ref position,
                    end,
                    decodedResourceCount > 0 ? ChunkBoundPolicy.ImageData : ChunkBoundPolicy.ImageDataBeforeResource,
                    out AniRiffChunkHeader chunk))
                {
                    if ((AniFrameChunkType)chunk.FourCc is not AniFrameChunkType.Icon)
                    {
                        continue;
                    }

                    int resourceIndex = resources.Count;

                    // Sequence entries are indices into the physical resource table. Thus an ignored damaged resource keeps an empty slot.
                    resources.Add(null);

                    if (!sequence.IsEmpty)
                    {
                        while (requiredResourceIndex < requiredResources.Length && requiredResources[requiredResourceIndex] < (uint)resourceIndex)
                        {
                            requiredResourceIndex++;
                        }
                    }

                    // Without a sequence chunk, the decoder decodes the resources in physical order. With a sequence chunk, it decodes only the resources that
                    // the sequence refers to.
                    bool shouldDecode = sequence.IsEmpty
                        || (requiredResourceIndex < requiredResources.Length && requiredResources[requiredResourceIndex] == (uint)resourceIndex);

                    if (shouldDecode)
                    {
                        // Child decoders can seek to the offsets in their embedded headers. The bounded view prevents a seek out of the icon chunk. The child
                        // decoders get the same integrity mode, so they own the damage in their resources.
                        frameStream.Reset(stream.Position, chunk.Size);
                        if (this.TryGetFrameFormat(frameStream, out AniFrameFormat format))
                        {
                            // The format probe reads the directory prefix, but the selected child decoder needs the complete resource.
                            frameStream.Position = 0;
                            resources[resourceIndex] = (format, action(format, frameStream));
                            decodedResourceCount++;
                        }
                    }

                    // Each decoded resource adds at least one output frame, so a file without a sequence needs no more resources than the frame limit. A
                    // sequence cannot refer to an index after its last required resource.
                    if ((sequence.IsEmpty && decodedResourceCount == maxDecodedResources)
                        || (!sequence.IsEmpty && (uint)resourceIndex == lastRequiredResource))
                    {
                        return;
                    }
                }

                // The walk stops before the end of the list only at a chunk that extends past the list. Then the physical index of each later resource is not
                // known, so the decoder keeps only the earlier resources.
                if (position + AniConstants.ChunkHeaderSize <= end)
                {
                    return;
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
    /// <param name="format">Receives the embedded resource format.</param>
    /// <returns>
    /// <see langword="true"/> if the format is known. <see langword="false"/> if the resource is truncated or uses an
    /// unknown format and the mode ignores image data errors.
    /// </returns>
    private bool TryGetFrameFormat(Stream stream, out AniFrameFormat format)
    {
        // A raw frame has no ICO/CUR directory prefix to inspect. See HasRawFrames.
        format = AniFrameFormat.Cur;
        if (this.HasRawFrames)
        {
            return true;
        }

        // The directory type selects the ICO or CUR decoder, and that decoder owns the other directory checks. A resource that is too short to hold a directory
        // has no type.
        Span<byte> directory = this.buffer[..IconDir.Size];
        IconFileType type = stream.Read(directory) == directory.Length ? IconDir.Parse(directory).Type : default;
        if (type is not (IconFileType.ICO or IconFileType.CUR))
        {
            this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError("The ANI file contains an unsupported icon resource.");
            return false;
        }

        format = type == IconFileType.ICO ? AniFrameFormat.Ico : AniFrameFormat.Cur;
        return true;
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
    private Image<TPixel> DecodeFrame<TPixel>(AniFrameFormat format, DecoderOptions options, Stream stream, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (this.HasRawFrames)
        {
            return new BmpDecoderCore(IconDecoderCore.CreateBitmapDecoderOptions(options)).Decode<TPixel>(options.Configuration, stream, cancellationToken);
        }

        return format is AniFrameFormat.Ico
            ? new IcoDecoderCore(options).Decode<TPixel>(options.Configuration, stream, cancellationToken)
            : new CurDecoderCore(options).Decode<TPixel>(options.Configuration, stream, cancellationToken);
    }

    /// <summary>
    /// Identifies one embedded ANI frame resource.
    /// </summary>
    /// <param name="format">The embedded resource format.</param>
    /// <param name="options">The nested decoder options.</param>
    /// <param name="stream">The bounded resource stream.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The identified resource.</returns>
    private ImageInfo IdentifyFrame(AniFrameFormat format, DecoderOptions options, Stream stream, CancellationToken cancellationToken)
    {
        if (this.HasRawFrames)
        {
            return new BmpDecoderCore(IconDecoderCore.CreateBitmapDecoderOptions(options)).Identify(options.Configuration, stream, cancellationToken);
        }

        return format is AniFrameFormat.Ico
            ? new IcoDecoderCore(options).Identify(options.Configuration, stream, cancellationToken)
            : new CurDecoderCore(options).Identify(options.Configuration, stream, cancellationToken);
    }

    /// <summary>
    /// Creates ANI metadata for one flattened output frame.
    /// </summary>
    /// <param name="source">The embedded frame metadata, when available.</param>
    /// <param name="format">The embedded resource format.</param>
    /// <param name="sequenceNumber">The animation sequence number.</param>
    /// <param name="frameDelay">The display rate in sixtieths of a second.</param>
    /// <param name="size">The embedded frame size.</param>
    /// <param name="bitmapMetadata">The BMP metadata of a raw bitmap resource, or <see langword="null"/>.</param>
    /// <returns>The ANI frame metadata.</returns>
    private static AniFrameMetadata CreateFrameMetadata(
        ImageFrameMetadata? source,
        AniFrameFormat format,
        int sequenceNumber,
        uint frameDelay,
        Size size,
        BmpMetadata? bitmapMetadata)
    {
        AniFrameMetadata metadata = new()
        {
            FrameDelay = frameDelay,
            SequenceNumber = sequenceNumber,
            FrameFormat = format
        };

        if (bitmapMetadata is not null)
        {
            // A raw bitmap keeps its depth, color type and color table in the image-level BMP metadata. The decoder
            // reports the frame as a CUR frame with a bitmap of the same depth. Thus the encoder writes the same pixels at
            // the same depth. A 5-6-5 bitmap becomes 24-bit, because an icon bitmap must be BI_RGB.
            metadata.Compression = IconFrameCompression.Bmp;
            (metadata.BmpBitsPerPixel, metadata.BmpColorType) = IconDecoderCore.GetBitmapLayout(bitmapMetadata);
            metadata.ColorTable = IconDecoderCore.GetBitmapColorTable(bitmapMetadata);
            metadata.EncodingWidth = IconDirEntry.ClampEncodingDimension(size.Width);
            metadata.EncodingHeight = IconDirEntry.ClampEncodingDimension(size.Height);

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
                metadata.BmpColorType = icoMetadata.BmpColorType;
                metadata.ColorTable = icoMetadata.ColorTable;

                break;
            case AniFrameFormat.Cur:
                CurFrameMetadata curMetadata = source.GetCurMetadata();
                metadata.EncodingWidth = curMetadata.EncodingWidth;
                metadata.EncodingHeight = curMetadata.EncodingHeight;
                metadata.Compression = curMetadata.Compression;
                metadata.BmpBitsPerPixel = curMetadata.BmpBitsPerPixel;
                metadata.BmpColorType = curMetadata.BmpColorType;
                metadata.HotspotX = curMetadata.HotspotX;
                metadata.HotspotY = curMetadata.HotspotY;
                metadata.ColorTable = curMetadata.ColorTable;

                break;
        }

        return metadata;
    }

    /// <summary>
    /// Creates decoder options for embedded resources without applying the outer ANI resize twice.
    /// </summary>
    /// <returns>The embedded frame decoder options.</returns>
    private DecoderOptions CreateFrameDecoderOptions()
        => new(this.Options.Configuration)
        {
            MaxFrames = this.Options.MaxFrames,
            SkipMetadata = this.Options.SkipMetadata,
            SegmentIntegrityHandling = this.Options.SegmentIntegrityHandling,
            ColorProfileHandling = this.Options.ColorProfileHandling
        };

    /// <summary>
    /// Reads the header of the next chunk in a RIFF container or list. Each walk of the ANI file uses this method, so it owns the bounds of each chunk.
    /// </summary>
    /// <remarks>
    /// A chunk that extends past the end of its parent is damage in the data that the parent holds, so <paramref name="policy"/> selects the integrity mode. In
    /// an information list, the ancillary mode controls the error, and the walk of the list stops. In the container and in a frame list, the image data mode
    /// controls the error. If the parent ends at the end of the stream, the chunk also ends there. Its reader gets the bytes that are present, so a truncated
    /// frame resource keeps the pixels that its child decoder can decode. If the parent ends before the stream ends, the walk of the parent stops, because the
    /// position of each later chunk is not known.
    /// </remarks>
    /// <param name="stream">The ANI stream. If the method returns <see langword="true"/>, the stream is at the start of the chunk payload.</param>
    /// <param name="position">
    /// The position of the next chunk header. If the method returns <see langword="true"/>, it moves to the chunk after this chunk. If a chunk extends past the
    /// parent, it does not move.
    /// </param>
    /// <param name="parentEnd">The exclusive end of the parent payload. It is not after the end of the stream.</param>
    /// <param name="policy">The integrity policy for a chunk that extends past the end of the parent.</param>
    /// <param name="chunk">The chunk header. Its size is the size of the payload that the stream holds.</param>
    /// <returns><see langword="true"/> if the parent holds another chunk. Otherwise, <see langword="false"/>.</returns>
    private bool TryReadChunk(BufferedReadStream stream, ref long position, long parentEnd, ChunkBoundPolicy policy, out AniRiffChunkHeader chunk)
    {
        if (position + AniConstants.ChunkHeaderSize > parentEnd)
        {
            // Fewer bytes than a chunk header remain. A missing pad byte after the last chunk also ends the parent here.
            chunk = default;
            return false;
        }

        // The check above proves that the stream holds the chunk header.
        stream.Position = position;
        Span<byte> data = this.buffer[..AniConstants.ChunkHeaderSize];
        stream.Read(data);
        chunk = AniRiffChunkHeader.Parse(data);

        long dataStart = stream.Position;
        long dataEnd = dataStart + chunk.Size;
        if (dataEnd > parentEnd)
        {
            if (policy is ChunkBoundPolicy.Ancillary)
            {
                this.Options.SegmentIntegrityHandling.ThrowOrIgnoreAncillaryError("An ANI RIFF chunk extends beyond its containing list.");
                return false;
            }

            if (parentEnd != stream.Length)
            {
                bool imageExists = policy is ChunkBoundPolicy.ImageData;
                this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError(imageExists, "An ANI RIFF chunk extends beyond its containing list.");
                return false;
            }

            this.Options.SegmentIntegrityHandling.ThrowOrIgnoreImageDataError("The ANI file ends inside a RIFF chunk.");
            chunk.Size = (uint)(parentEnd - dataStart);
            position = parentEnd;
            return true;
        }

        // RIFF aligns each chunk to a 16-bit boundary. The pad byte is not part of the declared size. A missing pad byte after the last chunk of a parent is
        // the end of the parent, so the next call returns false.
        position = dataEnd + (chunk.Size & 1);
        return true;
    }
}
