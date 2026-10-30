// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using System.IO.Compression;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Xmp;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;

/// <summary>
/// Reads JPEG XL codestream bytes across bounded container fragments without joining their payloads in memory.
/// </summary>
internal sealed class JxlCodestreamReader
{
    /// <summary>
    /// The high bit that identifies the final partial codestream box.
    /// </summary>
    private const uint LastFragment = 0x80000000U;

    /// <summary>
    /// The mask that removes the final-fragment flag from a sequence number.
    /// </summary>
    private const uint FragmentIndexMask = LastFragment - 1;

    private readonly BufferedReadStream stream;
    private readonly List<(uint Type, long Offset, long Length)> boxes = [];
    private readonly long codestreamOrigin;

    // Container ranges are indexed in logical order as fragment sequencing discovers them.
    // Seeking reuses these validated ranges without revisiting box headers or retaining payload bytes.
    private List<(long LogicalOffset, long PhysicalOffset, long Length)>? fragments;
    private int fragmentIndex = -1;
    private long fragmentRemaining;

    // Only version-one files can need this index. Entries retain file ranges, not copied codestream payloads.
    private Dictionary<uint, (long Offset, long Length, bool IsLast)>? pendingFragments;

    private readonly uint version;
    private readonly bool isContainer;

    // Box position, sequence index and final-fragment flag describe discovery, which only moves forward.
    // The current payload can move backward through cached ranges without changing those values.
    private long nextBoxPosition;
    private uint nextFragmentIndex;
    private bool lastFragment;
    private bool hasCodestream;
    private bool hasLevel;

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlCodestreamReader"/> class and reads the file signature and type.
    /// </summary>
    /// <param name="stream">The buffered, seekable input owned by the decoder core.</param>
    public JxlCodestreamReader(BufferedReadStream stream)
    {
        this.stream = stream;
        long start = stream.Position;
        this.codestreamOrigin = start;
        this.MaximumLength = stream.Length - start;

        // Inspect the same leading bytes for both file forms. A raw codestream must be rewound so
        // the bit reader, rather than this container reader, consumes and validates its complete image header.
        InlineArray16<byte> storage = default;
        Span<byte> header = storage;
        IsoBoxReader.ReadExactly(stream, header[..2], "The JPEG XL signature is incomplete.");

        if (header[..2].SequenceEqual(JxlConstants.CodestreamSignature))
        {
            this.fragmentRemaining = stream.Length - start;
            this.lastFragment = true;
            this.hasCodestream = true;
            stream.Position = start;
            return;
        }

        IsoBoxReader.ReadExactly(stream, header[2..12], "The JPEG XL container signature is incomplete.");
        if (!header[..12].SequenceEqual(JxlConstants.ContainerSignature))
        {
            throw new InvalidImageContentException("The input does not contain a JPEG XL signature.");
        }

        this.isContainer = true;
        long length = IsoBoxReader.ReadHeader(stream, stream.Length, header, out uint type, true);
        if (type != JxlConstants.FileTypeBox || length < 12)
        {
            throw new InvalidImageContentException("The JPEG XL signature must be followed by a complete file type box.");
        }

        this.nextBoxPosition = stream.Position + length;
        IsoBoxReader.ReadExactly(stream, header[..8], "The JPEG XL file type is incomplete.");

        // Version zero requires physical fragment order. Version one can reorder fragment indices;
        // both retain the same logical codestream and need no second full-image input buffer.
        uint brand = BinaryPrimitives.ReadUInt32BigEndian(header);
        this.version = BinaryPrimitives.ReadUInt32BigEndian(header[4..]);
        if (brand != JxlConstants.MajorBrand || this.version > 1)
        {
            throw new InvalidImageContentException("The JPEG XL container brand or version is not supported.");
        }
    }

    /// <summary>
    /// Gets the logical byte position in the codestream.
    /// </summary>
    public long Position { get; private set; }

    /// <summary>
    /// Gets an upper bound on logical codestream bytes, excluding bytes before this image's signature.
    /// Container headers and metadata can make the actual codestream shorter.
    /// </summary>
    public long MaximumLength { get; }

    /// <summary>
    /// Gets the non-codestream boxes encountered so far. Their ranges remain bounded by the physical input.
    /// </summary>
    public IReadOnlyList<(uint Type, long Offset, long Length)> Boxes => this.boxes;

    /// <summary>
    /// Positions the logical codestream at a byte offset without copying fragment payloads.
    /// </summary>
    /// <param name="position">The nonnegative offset from the codestream signature.</param>
    /// <exception cref="InvalidImageContentException">The offset exceeds the available codestream or crosses an invalid container boundary.</exception>
    public void Seek(long position)
    {
        // TOC offsets come from untrusted lengths. The physical file supplies a conservative first bound;
        // fragment traversal below also validates the actual logical end and every required container box.
        if ((ulong)position > (ulong)this.MaximumLength)
        {
            throw new InvalidImageContentException("The JPEG XL seek exceeds its codestream boundary.");
        }

        if (!this.isContainer)
        {
            this.stream.Position = this.codestreamOrigin + position;
            this.Position = position;
            this.fragmentRemaining = this.MaximumLength - position;
            return;
        }

        List<(long LogicalOffset, long PhysicalOffset, long Length)> ranges = this.fragments ?? this.OpenNextFragment();
        (long LogicalOffset, long PhysicalOffset, long Length) last = ranges[^1];
        long frontier = last.LogicalOffset + last.Length;
        if (position > frontier)
        {
            // Discovery state belongs to the furthest opened fragment. Resume there rather than
            // reparsing boxes after a backward seek, which would duplicate metadata and sequence indices.
            this.SelectFragment(ranges, ranges.Count - 1, frontier);
            if (!this.TrySkip(position - frontier))
            {
                throw new InvalidImageContentException("The JPEG XL seek exceeds its codestream boundary.");
            }

            return;
        }

        // Find the first range whose exclusive end follows the requested byte. Empty fragments have
        // no bytes to select. The logical frontier instead uses the last range's exhausted position.
        int lower = 0;
        int upper = ranges.Count;
        while (lower < upper)
        {
            int middle = lower + ((upper - lower) / 2);
            (long LogicalOffset, long PhysicalOffset, long Length) range = ranges[middle];
            if (range.LogicalOffset + range.Length <= position)
            {
                lower = middle + 1;
            }
            else
            {
                upper = middle;
            }
        }

        this.SelectFragment(ranges, Math.Min(lower, ranges.Count - 1), position);
    }

    /// <summary>
    /// Reads from the current fragment, opening the next fragment when needed.
    /// </summary>
    /// <param name="destination">The span that receives the codestream bytes.</param>
    /// <returns>The bytes read, or zero at the logical end. A read can stop at a fragment boundary.</returns>
    public int Read(Span<byte> destination)
    {
        // Return after reading one physical range. A malformed later box must not discard bytes already delivered
        // from this fragment. The core can then apply its image-data recovery policy after decoding available data.
        while (this.fragmentRemaining == 0 && this.MoveToNextFragment())
        {
        }

        int count = (int)Math.Min(this.fragmentRemaining, destination.Length);
        IsoBoxReader.ReadExactly(this.stream, destination[..count], "The JPEG XL codestream is truncated.");
        this.fragmentRemaining -= count;
        this.Position += count;
        return count;
    }

    /// <summary>
    /// Skips logical codestream bytes without copying their payloads into a temporary buffer.
    /// Missing fragments and invalid container boxes remain fatal.
    /// </summary>
    /// <param name="count">The nonnegative byte count supplied by the header parser.</param>
    /// <returns>Whether the complete range was skipped. False leaves the reader at the final logical codestream end.</returns>
    public bool TrySkip(long count)
    {
        while (count != 0)
        {
            while (this.fragmentRemaining == 0 && this.MoveToNextFragment())
            {
            }

            long step = Math.Min(count, this.fragmentRemaining);
            if (step == 0)
            {
                // Only a known final fragment can reach this result. Opening a missing or malformed
                // fragment throws above; callers must not treat those container errors as a short image payload.
                return false;
            }

            this.stream.Seek(step, SeekOrigin.Current);
            this.fragmentRemaining -= step;
            this.Position += step;
            count -= step;
        }

        return true;
    }

    /// <summary>
    /// Finishes container traversal after frame decoding and retains metadata boxes following the codestream.
    /// </summary>
    /// <remarks>
    /// This skips remaining codestream bytes without decoding them, including frames omitted by MaxFrames.
    /// Box bounds and fragment sequencing remain mandatory after recovery of a short image payload.
    /// </remarks>
    public void Complete()
    {
        if (!this.isContainer)
        {
            return;
        }

        if (this.fragments is not null)
        {
            // Random section reads can leave the current position behind the discovery frontier.
            // Resume from the last validated range; box and sequence state already describe that point.
            (long LogicalOffset, long PhysicalOffset, long Length) last = this.fragments[^1];
            this.SelectFragment(this.fragments, this.fragments.Count - 1, last.LogicalOffset + last.Length);
        }

        // Fragment boundaries belong to the container. Skip bytes by file position; do not materialize unrequested frames.
        while (!this.lastFragment)
        {
            this.Position += this.fragmentRemaining;
            this.fragmentRemaining = 0;
            this.OpenNextFragment();
        }

        this.Position += this.fragmentRemaining;
        this.fragmentRemaining = 0;
        if (this.pendingFragments is not null && this.pendingFragments.Count != 0)
        {
            throw new InvalidImageContentException("The JPEG XL container contains fragments after its final fragment.");
        }

        // Buffered reads can stop inside the last payload. Resume at the next physical box, even when
        // no boxes follow it. Version-one fragments can also finish in an earlier physical payload.
        this.stream.Position = this.nextBoxPosition;

        InlineArray16<byte> storage = default;
        Span<byte> header = storage;
        while (this.nextBoxPosition < this.stream.Length)
        {
            this.stream.Position = this.nextBoxPosition;
            long length = IsoBoxReader.ReadHeader(this.stream, this.stream.Length, header, out uint type, true);
            long offset = this.stream.Position;
            if (type == JxlConstants.SignatureBox)
            {
                // A concatenated container belongs to the next image. Leave its signature available to its decoder.
                this.stream.Position = this.nextBoxPosition;
                break;
            }

            if (type is JxlConstants.FileTypeBox or JxlConstants.CompleteCodestreamBox or JxlConstants.PartialCodestreamBox)
            {
                throw new InvalidImageContentException("The JPEG XL container contains another codestream after its final fragment.");
            }

            this.RetainMetadataBox(type, offset, length);
            this.nextBoxPosition = offset + length;
            this.stream.Position = this.nextBoxPosition;
        }
    }

    /// <summary>
    /// Applies one retained EXIF or XML box through the shared profile APIs and restores the physical stream position.
    /// </summary>
    /// <param name="box">The bounded range retained during container traversal.</param>
    /// <param name="metadata">The image metadata receiving the profile.</param>
    /// <param name="configuration">The decoder's allocator and stream-buffer settings.</param>
    /// <param name="cancellationToken">The operation's cancellation token.</param>
    public void ApplyMetadataBox(
        (uint Type, long Offset, long Length) box,
        ImageMetadata metadata,
        Configuration configuration,
        CancellationToken cancellationToken)
    {
        if (box.Type is not (JxlConstants.ExifBox or JxlConstants.XmlBox or JxlConstants.CompressedMetadataBox))
        {
            return;
        }

        long position = this.stream.Position;
        try
        {
            // Box ranges are borrowed from the input. Read only the requested range and restore the caller's
            // position in finally, even if a shared profile parser rejects this optional metadata.
            this.stream.Position = box.Offset;
            uint type = box.Type;
            long length = box.Length;
            if (type == JxlConstants.CompressedMetadataBox)
            {
                // The outer box is bounded during traversal. Its inner type belongs to this optional payload,
                // so validate it under the core's ancillary policy without reading into the following box.
                if (length < sizeof(uint))
                {
                    throw new InvalidImageContentException("The JPEG XL compressed metadata type is incomplete.");
                }

                InlineArray4<byte> typeStorage = default;
                Span<byte> typeBytes = typeStorage;
                IsoBoxReader.ReadExactly(this.stream, typeBytes, "The JPEG XL compressed metadata type is incomplete.");
                type = BinaryPrimitives.ReadUInt32BigEndian(typeBytes);
                if (type is JxlConstants.CompressedMetadataBox or JxlConstants.JpegReconstructionBox || (type >> 8) == 0x6A786C)
                {
                    throw new InvalidImageContentException("The JPEG XL compressed metadata wraps a forbidden box type.");
                }

                if (type is not (JxlConstants.ExifBox or JxlConstants.XmlBox))
                {
                    return;
                }

                // Brotli does not declare an output size. The existing chunked stream keeps growing storage
                // under the configured allocator until the final profile's exact array size is known.
                using ChunkedMemoryStream decoded = new(configuration.MemoryAllocator);

                // Input and output share one allocation. Limit each half before doubling so any positive
                // configured buffer size remains representable as a single span's signed 32-bit length.
                int chunkSize = Math.Min(configuration.StreamProcessingBufferSize, int.MaxValue / 2);
                using IMemoryOwner<byte> scratch = configuration.MemoryAllocator.Allocate<byte>(2 * chunkSize);
                Span<byte> buffers = scratch.Memory.Span;
                Span<byte> input = buffers[..chunkSize];
                Span<byte> output = buffers[chunkSize..];
                BrotliDecoder decoder = default;
                try
                {
                    int available = 0;
                    int offset = 0;
                    long remaining = length - sizeof(uint);
                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (offset == available && remaining != 0)
                        {
                            available = (int)Math.Min(remaining, input.Length);
                            IsoBoxReader.ReadExactly(this.stream, input[..available], "The JPEG XL compressed metadata is truncated.");
                            offset = 0;
                            remaining -= available;
                        }

                        OperationStatus status = decoder.Decompress(input[offset..available], output, out int consumed, out int written);
                        offset += consumed;
                        if (written > Array.MaxLength - decoded.Length)
                        {
                            throw new InvalidImageContentException("The JPEG XL metadata exceeds the supported profile size.");
                        }

                        decoded.Write(output[..written]);
                        if (status == OperationStatus.Done)
                        {
                            // A completed Brotli stream must consume exactly this box's compressed payload.
                            // Extra compressed bytes cannot be interpreted as another profile or codestream.
                            if (remaining != 0 || offset != available)
                            {
                                throw new InvalidImageContentException("The JPEG XL compressed metadata contains trailing bytes.");
                            }

                            break;
                        }

                        if (status == OperationStatus.InvalidData || (status == OperationStatus.NeedMoreData && remaining == 0))
                        {
                            throw new InvalidImageContentException("The JPEG XL compressed metadata is invalid or incomplete.");
                        }
                    }
                }
                finally
                {
                    decoder.Dispose();
                }

                decoded.Position = 0;
                ApplyProfile(decoded, decoded.Length, type, metadata);
            }
            else
            {
                ApplyProfile(this.stream, length, type, metadata);
            }
        }
        finally
        {
            // The bit reader can still hold prefetched codestream bits. Reading a metadata range must not
            // change where its next physical refill starts, or the next fragment/frame would read the wrong bytes.
            this.stream.Position = position;
        }
    }

    /// <summary>
    /// Reads one bounded profile payload. Each shared profile retains its own final byte array.
    /// </summary>
    /// <param name="stream">The raw box or decompressed metadata stream.</param>
    /// <param name="length">The exact payload byte count.</param>
    /// <param name="type">The EXIF or XML box type.</param>
    /// <param name="metadata">The image metadata receiving the profile.</param>
    private static void ApplyProfile(Stream stream, long length, uint type, ImageMetadata metadata)
    {
        if (type == JxlConstants.ExifBox)
        {
            if (length < sizeof(uint))
            {
                throw new InvalidImageContentException("The JPEG XL EXIF offset field is incomplete.");
            }

            InlineArray4<byte> storage = default;
            Span<byte> offsetBytes = storage;
            IsoBoxReader.ReadExactly(stream, offsetBytes, "The JPEG XL EXIF offset field is incomplete.");
            uint offset = BinaryPrimitives.ReadUInt32BigEndian(offsetBytes);
            length -= sizeof(uint);
            if (length < 8 || offset > (ulong)(length - 8))
            {
                throw new InvalidImageContentException("The JPEG XL EXIF TIFF-header offset exceeds its payload.");
            }

            // The offset counts from the byte following its own field. Keep the TIFF header and all
            // remaining TIFF data together for ExifProfile; the outer offset is not part of that profile.
            stream.Seek(offset, SeekOrigin.Current);
            length -= offset;
        }

        if (length > Array.MaxLength)
        {
            throw new InvalidImageContentException("The JPEG XL metadata exceeds the supported profile size.");
        }

        // ExifProfile and XmpProfile retain this final array. They cannot borrow either the input stream
        // or the allocator-owned Brotli output, which is disposed when metadata application finishes.
        byte[] data = new byte[(int)length];
        IsoBoxReader.ReadExactly(stream, data, "The JPEG XL metadata payload is incomplete.");
        if (type == JxlConstants.ExifBox)
        {
            // EXIF parsing is lazy. Validate its entries before publishing the profile so malformed
            // optional data fails inside the core's ancillary policy, rather than during a later orientation update.
            ExifProfile profile = new(data);
            _ = profile.Values;
            metadata.ExifProfile = profile;
        }
        else
        {
            metadata.XmpProfile = new XmpProfile(data);
        }
    }

    /// <summary>
    /// Opens the next undiscovered logical fragment and retains intervening metadata box ranges.
    /// </summary>
    /// <returns>The discovered fragment ranges, including the newly opened fragment.</returns>
    private List<(long LogicalOffset, long PhysicalOffset, long Length)> OpenNextFragment()
    {
        // A version-one fragment found earlier can now become the next logical fragment. Removing its index entry
        // releases only metadata; the payload has remained in the caller's stream throughout.
        if (this.pendingFragments is not null && this.pendingFragments.Remove(this.nextFragmentIndex, out (long Offset, long Length, bool IsLast) pending))
        {
            this.stream.Position = pending.Offset;
            this.fragmentRemaining = pending.Length;
            this.lastFragment = pending.IsLast;
            this.nextFragmentIndex++;
            return this.RetainFragment(pending.Offset, pending.Length);
        }

        InlineArray16<byte> storage = default;
        Span<byte> header = storage;
        while (this.nextBoxPosition < this.stream.Length)
        {
            // BufferedReadStream also monitors cancellation on seeks. Box traversal performs no sample-level checks.
            this.stream.Position = this.nextBoxPosition;
            long length = IsoBoxReader.ReadHeader(this.stream, this.stream.Length, header, out uint type, true);
            long offset = this.stream.Position;
            this.nextBoxPosition = offset + length;

            if (type == JxlConstants.CompleteCodestreamBox)
            {
                if (this.hasCodestream || this.pendingFragments is not null)
                {
                    throw new InvalidImageContentException("A complete JPEG XL codestream cannot be combined with other codestream boxes.");
                }

                this.hasCodestream = true;
                this.lastFragment = true;
                this.fragmentRemaining = length;
                return this.RetainFragment(offset, length);
            }

            if (type == JxlConstants.PartialCodestreamBox)
            {
                if (length < sizeof(uint))
                {
                    throw new InvalidImageContentException("The JPEG XL fragment sequence number is incomplete.");
                }

                IsoBoxReader.ReadExactly(this.stream, header[..sizeof(uint)], "The JPEG XL fragment sequence number is incomplete.");
                uint sequence = BinaryPrimitives.ReadUInt32BigEndian(header);
                uint index = sequence & FragmentIndexMask;
                bool last = (sequence & LastFragment) != 0;
                offset += sizeof(uint);
                length -= sizeof(uint);
                if (index < this.nextFragmentIndex || (this.version == 0 && index != this.nextFragmentIndex))
                {
                    throw new InvalidImageContentException("The JPEG XL fragment sequence is duplicated or out of order.");
                }

                if (index == this.nextFragmentIndex)
                {
                    this.hasCodestream = true;
                    this.nextFragmentIndex++;
                    this.lastFragment = last;
                    this.fragmentRemaining = length;
                    return this.RetainFragment(offset, length);
                }

                // Version one permits out-of-order fragments. Keep just their validated file ranges; finding a
                // missing earlier fragment requires seeking through box headers, not copying or decoding payloads.
                this.pendingFragments ??= [];
                if (!this.pendingFragments.TryAdd(index, (offset, length, last)))
                {
                    throw new InvalidImageContentException("The JPEG XL fragment sequence contains a duplicate index.");
                }
            }
            else if (type is JxlConstants.FileTypeBox or JxlConstants.SignatureBox)
            {
                throw new InvalidImageContentException("A JPEG XL codestream cannot contain another file header.");
            }
            else
            {
                this.RetainMetadataBox(type, offset, length);
            }
        }

        throw new InvalidImageContentException("The JPEG XL codestream is missing a fragment or its final-fragment flag.");
    }

    /// <summary>
    /// Advances through cached ranges before discovering another container fragment.
    /// </summary>
    /// <returns>Whether another logical fragment is available.</returns>
    private bool MoveToNextFragment()
    {
        if (this.fragments is not null && this.fragmentIndex + 1 < this.fragments.Count)
        {
            int next = this.fragmentIndex + 1;
            this.SelectFragment(this.fragments, next, this.fragments[next].LogicalOffset);
            return true;
        }

        if (this.lastFragment)
        {
            return false;
        }

        this.OpenNextFragment();
        return true;
    }

    /// <summary>
    /// Records a newly discovered payload at the logical frontier and makes it the current fragment.
    /// </summary>
    /// <param name="offset">The validated physical payload start.</param>
    /// <param name="length">The validated payload length, excluding its sequence number.</param>
    /// <returns>The retained range list, which is created only for a container.</returns>
    private List<(long LogicalOffset, long PhysicalOffset, long Length)> RetainFragment(long offset, long length)
    {
        List<(long LogicalOffset, long PhysicalOffset, long Length)> ranges = this.fragments ??= [];
        this.fragmentIndex = ranges.Count;
        ranges.Add((this.Position, offset, length));
        return ranges;
    }

    /// <summary>
    /// Restores one cached payload position without changing fragment discovery or metadata state.
    /// </summary>
    /// <param name="ranges">The validated ranges in logical codestream order.</param>
    /// <param name="index">The selected range index.</param>
    /// <param name="position">The logical position within the range, including its exclusive end.</param>
    private void SelectFragment(List<(long LogicalOffset, long PhysicalOffset, long Length)> ranges, int index, long position)
    {
        (long LogicalOffset, long PhysicalOffset, long Length) range = ranges[index];
        long consumed = position - range.LogicalOffset;
        this.stream.Position = range.PhysicalOffset + consumed;
        this.fragmentRemaining = range.Length - consumed;
        this.fragmentIndex = index;
        this.Position = position;
    }

    /// <summary>
    /// Retains supported metadata ranges while skipping padding and unknown box types without allocations.
    /// </summary>
    /// <param name="type">The box type.</param>
    /// <param name="offset">The bounded payload position.</param>
    /// <param name="length">The bounded payload length.</param>
    private void RetainMetadataBox(uint type, long offset, long length)
    {
        if (type == JxlConstants.LevelBox)
        {
            // The level is container control data, not an optional profile. It must occur once before
            // any codestream box and contain exactly one supported level byte, even with SkipMetadata.
            if (this.hasLevel || this.hasCodestream || this.pendingFragments is not null || length != 1)
            {
                throw new InvalidImageContentException("The JPEG XL level box is duplicated, misplaced or has an invalid length.");
            }

            if (this.stream.ReadByte() is not (5 or 10))
            {
                throw new InvalidImageContentException("The JPEG XL container level is not supported.");
            }

            this.hasLevel = true;
        }

        if (type is JxlConstants.ExifBox or JxlConstants.XmlBox or JxlConstants.CompressedMetadataBox
            or JxlConstants.JpegReconstructionBox or JxlConstants.LevelBox)
        {
            this.boxes.Add((type, offset, length));
        }
    }
}
