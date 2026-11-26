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

    /// <summary>
    /// The segment integrity mode. It controls if a codestream that ends before its final fragment throws, and if a
    /// damaged metadata box throws.
    /// </summary>
    private readonly SegmentIntegrityHandling integrityHandling;

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
    /// Initializes a new instance of the <see cref="JxlCodestreamReader"/> class. It reads the file signature and type,
    /// and it finds every codestream fragment of a container.
    /// </summary>
    /// <param name="stream">The buffered, seekable input owned by the decoder core.</param>
    /// <param name="integrityHandling">
    /// The segment integrity mode. It controls if a container whose codestream ends before its final fragment throws.
    /// </param>
    public JxlCodestreamReader(BufferedReadStream stream, SegmentIntegrityHandling integrityHandling)
    {
        this.stream = stream;
        this.integrityHandling = integrityHandling;
        long start = stream.Position;
        this.codestreamOrigin = start;

        // Inspect the same leading bytes for both file forms. A raw codestream must be rewound so
        // the bit reader, rather than this container reader, consumes and validates its complete image header.
        InlineArray16<byte> storage = default;
        Span<byte> header = storage;
        IsoBoxReader.ReadExactly(stream, header[..2], "The JPEG XL signature is incomplete.");

        if (header[..2].SequenceEqual(JxlConstants.CodestreamSignature))
        {
            this.Length = stream.Length - start;
            this.fragmentRemaining = this.Length;
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

        // The reader reads the box structure before the decoder reads image data, as the HEIF decoder does. Thus a
        // container error throws here in each mode, and the image data policy of the decoder never sees it.
        while (!this.lastFragment)
        {
            this.Position += this.fragmentRemaining;
            this.fragmentRemaining = 0;
            this.DiscoverNextFragment();
        }

        if (this.pendingFragments is not null && this.pendingFragments.Count != 0)
        {
            throw new InvalidImageContentException("The JPEG XL container contains fragments after its final fragment.");
        }

        // Discovery found each fragment, so the last range is the end of the available logical codestream.
        (long LogicalOffset, long PhysicalOffset, long Length) last = this.fragments![^1];
        this.Length = last.LogicalOffset + last.Length;
        this.SelectFragment(this.fragments, 0, 0);
    }

    /// <summary>
    /// Gets the logical byte position in the codestream.
    /// </summary>
    public long Position { get; private set; }

    /// <summary>
    /// Gets the number of logical codestream bytes that the input holds, but not the bytes before the signature of this image. A truncated container holds only
    /// the bytes of its fragments that the file contains.
    /// </summary>
    public long Length { get; }

    /// <summary>
    /// Gets the non-codestream boxes encountered so far. Their ranges remain bounded by the physical input.
    /// </summary>
    public IReadOnlyList<(uint Type, long Offset, long Length)> Boxes => this.boxes;

    /// <summary>
    /// Positions the logical codestream at a byte offset without copying fragment payloads.
    /// </summary>
    /// <param name="position">The nonnegative offset from the codestream signature.</param>
    /// <returns>Whether the requested position exists. False identifies a position beyond the final codestream end.</returns>
    /// <exception cref="InvalidImageContentException">The offset is negative or crosses an invalid container boundary.</exception>
    public bool TrySeek(long position)
    {
        // TOC offsets come from untrusted lengths. The fragments that the constructor found give the logical end.
        if (position < 0)
        {
            throw new InvalidImageContentException("The JPEG XL seek exceeds its codestream boundary.");
        }

        if (position > this.Length)
        {
            return false;
        }

        if (!this.isContainer)
        {
            this.stream.Position = this.codestreamOrigin + position;
            this.Position = position;
            this.fragmentRemaining = this.Length - position;
            return true;
        }

        // Find the first range whose exclusive end follows the requested byte. Empty fragments have
        // no bytes to select. The logical frontier instead uses the last range's exhausted position.
        List<(long LogicalOffset, long PhysicalOffset, long Length)> ranges = this.fragments!;
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
        return true;
    }

    /// <summary>
    /// Reads from the current fragment, opening the next fragment when needed.
    /// </summary>
    /// <param name="destination">The span that receives the codestream bytes.</param>
    /// <returns>The bytes read, or zero at the logical end. A read can stop at a fragment boundary.</returns>
    public int Read(Span<byte> destination)
    {
        // Return after reading one physical range, so the caller gets the bytes of each fragment in turn.
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
                // The reader is at the end of the last fragment.
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
    /// The constructor already found every fragment, so this method reads only the boxes after the last fragment.
    /// </remarks>
    public void Complete()
    {
        if (!this.isContainer)
        {
            return;
        }

        // Random section reads can leave the current position before the end of the last fragment.
        // Resume at that end. The box and sequence state already describe that point.
        List<(long LogicalOffset, long PhysicalOffset, long Length)> ranges = this.fragments!;
        (long LogicalOffset, long PhysicalOffset, long Length) last = ranges[^1];
        this.SelectFragment(ranges, ranges.Count - 1, last.LogicalOffset + last.Length);

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
            // A jbrd box is a recipe for byte-exact JPEG reconstruction, not metadata for decoded pixels.
            // Its offsets and tables depend on the original coefficients and exact metadata bytes. Pixel edits
            // invalidate that recipe. Container traversal validates its range; pixel decoding skips its contents.
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
                    this.integrityHandling.ThrowOrIgnoreAncillaryError("The JPEG XL compressed metadata type is incomplete.");
                    return;
                }

                InlineArray4<byte> typeStorage = default;
                Span<byte> typeBytes = typeStorage;
                IsoBoxReader.ReadExactly(this.stream, typeBytes, "The JPEG XL compressed metadata type is incomplete.");
                type = BinaryPrimitives.ReadUInt32BigEndian(typeBytes);
                if (type is JxlConstants.CompressedMetadataBox or JxlConstants.JpegReconstructionBox || (type >> 8) == 0x6A786C)
                {
                    this.integrityHandling.ThrowOrIgnoreAncillaryError("The JPEG XL compressed metadata wraps a forbidden box type.");
                    return;
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
                            this.integrityHandling.ThrowOrIgnoreAncillaryError("The JPEG XL metadata exceeds the supported profile size.");
                            return;
                        }

                        decoded.Write(output[..written]);
                        if (status == OperationStatus.Done)
                        {
                            // A completed Brotli stream must consume exactly this box's compressed payload.
                            // Extra compressed bytes cannot be interpreted as another profile or codestream.
                            if (remaining != 0 || offset != available)
                            {
                                this.integrityHandling.ThrowOrIgnoreAncillaryError("The JPEG XL compressed metadata contains trailing bytes.");
                                return;
                            }

                            break;
                        }

                        if (status == OperationStatus.InvalidData || (status == OperationStatus.NeedMoreData && remaining == 0))
                        {
                            this.integrityHandling.ThrowOrIgnoreAncillaryError("The JPEG XL compressed metadata is invalid or incomplete.");
                            return;
                        }
                    }
                }
                finally
                {
                    decoder.Dispose();
                }

                decoded.Position = 0;
                this.ApplyProfile(decoded, decoded.Length, type, metadata);
            }
            else
            {
                this.ApplyProfile(this.stream, length, type, metadata);
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
    private void ApplyProfile(Stream stream, long length, uint type, ImageMetadata metadata)
    {
        if (type == JxlConstants.XmlBox)
        {
            if (!XmpReader.TryReadProfile(stream, length, out XmpProfile? profile))
            {
                this.integrityHandling.ThrowOrIgnoreAncillaryError("The JPEG XL XMP box payload is incomplete.");
                return;
            }

            metadata.XmpProfile = profile ?? metadata.XmpProfile;
            return;
        }

        if (type == JxlConstants.ExifBox)
        {
            if (length < sizeof(uint))
            {
                this.integrityHandling.ThrowOrIgnoreAncillaryError("The JPEG XL EXIF offset field is incomplete.");
                return;
            }

            InlineArray4<byte> storage = default;
            Span<byte> offsetBytes = storage;
            IsoBoxReader.ReadExactly(stream, offsetBytes, "The JPEG XL EXIF offset field is incomplete.");
            uint offset = BinaryPrimitives.ReadUInt32BigEndian(offsetBytes);
            length -= sizeof(uint);
            if (length < 8 || offset > (ulong)(length - 8))
            {
                this.integrityHandling.ThrowOrIgnoreAncillaryError("The JPEG XL EXIF TIFF-header offset exceeds its payload.");
                return;
            }

            // The offset counts from the byte following its own field. Keep the TIFF header and all
            // remaining TIFF data together for ExifProfile; the outer offset is not part of that profile.
            stream.Seek(offset, SeekOrigin.Current);
            length -= offset;
        }

        if (!ExifReader.TryReadProfile(stream, length, out ExifProfile? exifProfile))
        {
            this.integrityHandling.ThrowOrIgnoreAncillaryError("The JPEG XL EXIF box payload is incomplete.");
            return;
        }

        try
        {
            // This code checks the lazy EXIF entries with the ancillary error policy of the box. The shared EXIF parser reports damaged entries with
            // exceptions, so this typed catch is the boundary to that parser.
            _ = exifProfile.Values;
        }
        catch (ImageFormatException ex)
        {
            this.integrityHandling.ThrowOrIgnoreAncillaryError("The JPEG XL EXIF box payload is invalid.", ex);
            return;
        }
        catch (NotSupportedException ex)
        {
            this.integrityHandling.ThrowOrIgnoreAncillaryError("The JPEG XL EXIF box payload is invalid.", ex);
            return;
        }

        metadata.TrySetExifProfile(exifProfile);
    }

    /// <summary>
    /// Discovers the next logical fragment and retains intervening metadata box ranges.
    /// </summary>
    /// <remarks>
    /// The reader treats a codestream that ends before its final fragment as truncated image data. The HEIF decoder
    /// treats a media data box that ends early in the same way. The codestream box can extend past the end of the file,
    /// or the file can end before the next fragment.
    /// The Strict and IgnoreAncillary modes throw. The IgnoreImageData mode keeps the codestream bytes that the file
    /// holds. Other box errors throw in each mode.
    /// </remarks>
    private void DiscoverNextFragment()
    {
        // A version-one fragment found earlier can now become the next logical fragment. Removing its index entry
        // releases only metadata; the payload has remained in the caller's stream throughout.
        if (this.pendingFragments is not null && this.pendingFragments.Remove(this.nextFragmentIndex, out (long Offset, long Length, bool IsLast) pending))
        {
            this.stream.Position = pending.Offset;
            this.fragmentRemaining = pending.Length;
            this.lastFragment = pending.IsLast;
            this.nextFragmentIndex++;
            this.RetainFragment(pending.Offset, pending.Length);
            return;
        }

        InlineArray16<byte> storage = default;
        Span<byte> header = storage;
        while (this.nextBoxPosition < this.stream.Length)
        {
            // BufferedReadStream also monitors cancellation on seeks. Box traversal performs no sample-level checks.
            this.stream.Position = this.nextBoxPosition;
            long length = IsoBoxReader.ReadHeader(this.stream, this.stream.Length, header, out uint type, true, out bool truncated);
            long offset = this.stream.Position;
            this.nextBoxPosition = offset + length;
            if (truncated)
            {
                if (type is not (JxlConstants.CompleteCodestreamBox or JxlConstants.PartialCodestreamBox))
                {
                    throw new InvalidImageContentException("Box size extends beyond its parent boundary.");
                }

                // The box holds the last codestream bytes of the file.
                this.integrityHandling.ThrowOrIgnoreImageDataError("The JPEG XL codestream box extends beyond the end of the file.");
            }

            if (type == JxlConstants.CompleteCodestreamBox)
            {
                if (this.hasCodestream || this.pendingFragments is not null)
                {
                    throw new InvalidImageContentException("A complete JPEG XL codestream cannot be combined with other codestream boxes.");
                }

                this.hasCodestream = true;
                this.lastFragment = true;
                this.fragmentRemaining = length;
                this.RetainFragment(offset, length);
                return;
            }

            if (type == JxlConstants.PartialCodestreamBox)
            {
                if (length < sizeof(uint))
                {
                    if (truncated)
                    {
                        // The file ends inside the sequence number, so the box holds no codestream bytes.
                        break;
                    }

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
                    this.RetainFragment(offset, length);
                    return;
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

        // The file ends before the final fragment. Later fragments that the reader found out of order follow a missing
        // fragment, so the decoder cannot use them.
        this.integrityHandling.ThrowOrIgnoreImageDataError("The JPEG XL codestream is missing a fragment or its final-fragment flag.");
        if (this.fragments is null)
        {
            throw new InvalidImageContentException("The JPEG XL container holds no codestream bytes.");
        }

        this.lastFragment = true;
        this.pendingFragments = null;
    }

    /// <summary>
    /// Moves to the next fragment that the constructor found.
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

        return false;
    }

    /// <summary>
    /// Records a newly discovered payload at the logical frontier and makes it the current fragment.
    /// </summary>
    /// <param name="offset">The validated physical payload start.</param>
    /// <param name="length">The validated payload length, excluding its sequence number.</param>
    private void RetainFragment(long offset, long length)
    {
        List<(long LogicalOffset, long PhysicalOffset, long Length)> ranges = this.fragments ??= [];
        this.fragmentIndex = ranges.Count;
        ranges.Add((this.Position, offset, length));
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
