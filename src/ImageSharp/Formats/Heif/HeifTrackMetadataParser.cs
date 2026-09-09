// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Parses the Exif and XMP items implicitly associated with one HEIF image-sequence track.
/// </summary>
internal sealed class HeifTrackMetadataParser
{
    /// <summary>
    /// The allocator used for temporary item identifiers and extent descriptors.
    /// </summary>
    private readonly MemoryAllocator allocator;

    /// <summary>
    /// Initializes a new instance of the <see cref="HeifTrackMetadataParser"/> class.
    /// </summary>
    /// <param name="allocator">The allocator used for bounded temporary parser state.</param>
    public HeifTrackMetadataParser(MemoryAllocator allocator) => this.allocator = allocator;

    /// <summary>
    /// Gets the registered MIME content type for XMP metadata without allocating a managed byte array.
    /// </summary>
    private static ReadOnlySpan<byte> XmpContentType => "application/rdf+xml"u8;

    /// <summary>
    /// Parses the bounded metadata items from one track-level metadata box.
    /// </summary>
    /// <param name="stream">The seekable HEIF stream positioned at the metadata full-box header.</param>
    /// <param name="boxLength">The validated metadata payload length.</param>
    /// <param name="scratch">The caller-owned reusable parser scratch.</param>
    /// <returns>The retained Exif and XMP payloads.</returns>
    public HeifSequenceMetadata Parse(Stream stream, long boxLength, Span<byte> scratch)
    {
        long metadataStart = stream.Position;
        long metadataEnd = checked(metadataStart + boxLength);
        HeifBoxReader.EnsureInsideParent(boxLength, stream.Length - metadataStart);
        ReadOnlySpan<byte> fullBoxHeader = ReadPrefix(stream, boxLength, scratch, 4, "track metadata");
        if (BinaryPrimitives.ReadUInt32BigEndian(fullBoxHeader) != 0)
        {
            throw new InvalidImageContentException("The track metadata box has unsupported version or flags.");
        }

        BoxReference handler = default;
        BoxReference itemInformation = default;
        BoxReference itemLocations = default;
        BoxReference itemData = default;
        bool firstChild = true;
        while (stream.Position < metadataEnd)
        {
            long childLength = HeifBoxReader.ReadHeader(stream, metadataEnd, scratch, out Heif4CharCode childType);
            long childStart = stream.Position;
            if (firstChild && childType != Heif4CharCode.Hdlr)
            {
                throw new InvalidImageContentException("The track metadata box does not begin with its picture handler.");
            }

            switch (childType)
            {
                case Heif4CharCode.Hdlr:
                    SetUnique(ref handler, childStart, childLength, childType);
                    break;
                case Heif4CharCode.Iinf:
                    SetUnique(ref itemInformation, childStart, childLength, childType);
                    break;
                case Heif4CharCode.Iloc:
                    SetUnique(ref itemLocations, childStart, childLength, childType);
                    break;
                case Heif4CharCode.Idat:
                    SetUnique(ref itemData, childStart, childLength, childType);
                    break;
            }

            firstChild = false;
            stream.Position = checked(childStart + childLength);
        }

        if (!handler.IsPresent)
        {
            throw new InvalidImageContentException("The track metadata box has no picture handler.");
        }

        stream.Position = handler.Offset;
        if (ParseHandler(stream, handler.Length, scratch) != Heif4CharCode.Pict)
        {
            throw new InvalidImageContentException("The track metadata box does not use the picture handler.");
        }

        if (!itemInformation.IsPresent && !itemLocations.IsPresent)
        {
            return new HeifSequenceMetadata(null, null);
        }

        if (!itemInformation.IsPresent || !itemLocations.IsPresent)
        {
            throw new InvalidImageContentException("The track metadata box has incomplete item declarations or locations.");
        }

        stream.Position = itemInformation.Offset;
        MetadataItemIds itemIds = this.ParseItemInformation(stream, itemInformation.Length, scratch);
        byte[]? exifData = itemIds.ExifItemId == 0
            ? null
            : this.ReadItemPayload(stream, itemLocations, itemData, itemIds.ExifItemId, scratch);

        byte[]? xmpData = itemIds.XmpItemId == 0
            ? null
            : this.ReadItemPayload(stream, itemLocations, itemData, itemIds.XmpItemId, scratch);

        return new HeifSequenceMetadata(exifData, xmpData);
    }

    /// <summary>
    /// Parses item declarations while retaining only the identifiers for Exif and unencoded XMP items.
    /// </summary>
    /// <param name="stream">The stream positioned at the item-information full-box header.</param>
    /// <param name="boxLength">The validated item-information payload length.</param>
    /// <param name="scratch">The caller-owned reusable parser scratch.</param>
    /// <returns>The recognized metadata item identifiers.</returns>
    private MetadataItemIds ParseItemInformation(Stream stream, long boxLength, Span<byte> scratch)
    {
        long itemInformationStart = stream.Position;
        long itemInformationEnd = checked(itemInformationStart + boxLength);
        ReadOnlySpan<byte> prefix = ReadPrefix(stream, boxLength, scratch, 4, "track item information");
        byte version = prefix[0];
        int headerLength = version switch
        {
            0 => 6,
            1 => 8,
            _ => throw new InvalidImageContentException($"The track item-information box has unsupported version {version}.")
        };

        prefix = ReadPrefixFromStart(stream, boxLength, scratch, headerLength, "track item information");
        if ((BinaryPrimitives.ReadUInt32BigEndian(prefix) & 0x00FFFFFF) != 0)
        {
            throw new InvalidImageContentException("The track item-information box has unsupported flags.");
        }

        uint entryCount = version == 0
            ? BinaryPrimitives.ReadUInt16BigEndian(prefix[4..])
            : BinaryPrimitives.ReadUInt32BigEndian(prefix[4..]);

        long entryBytes = itemInformationEnd - stream.Position;
        if (entryCount > int.MaxValue || entryCount > (ulong)(entryBytes / 8))
        {
            throw new InvalidImageContentException("The track item-information entry count exceeds its bounded payload.");
        }

        if (entryCount == 0)
        {
            if (entryBytes != 0)
            {
                throw new InvalidImageContentException("The empty track item-information box contains trailing data.");
            }

            return default;
        }

        using IMemoryOwner<uint> identifierOwner = this.allocator.Allocate<uint>((int)entryCount);
        Span<uint> identifiers = identifierOwner.GetSpan()[..(int)entryCount];
        MetadataItemIds result = default;
        for (int i = 0; i < identifiers.Length; i++)
        {
            if (stream.Position >= itemInformationEnd)
            {
                throw new InvalidImageContentException("The track item-information entry count exceeds its bounded payload.");
            }

            long entryLength = HeifBoxReader.ReadHeader(stream, itemInformationEnd, scratch, out Heif4CharCode entryType);
            if (entryType != Heif4CharCode.Infe)
            {
                throw new InvalidImageContentException($"The track item-information box contains unexpected child '{entryType}'.");
            }

            uint itemId = ParseItemInformationEntry(stream, entryLength, scratch, out Heif4CharCode itemType, out bool isXmp);
            identifiers[i] = itemId;
            if (itemType == Heif4CharCode.Exif)
            {
                if (result.ExifItemId != 0)
                {
                    throw new InvalidImageContentException("The image-sequence track declares more than one Exif metadata item.");
                }

                result.ExifItemId = itemId;
            }
            else if (isXmp)
            {
                if (result.XmpItemId != 0)
                {
                    throw new InvalidImageContentException("The image-sequence track declares more than one XMP metadata item.");
                }

                result.XmpItemId = itemId;
            }
        }

        if (stream.Position != itemInformationEnd)
        {
            throw new InvalidImageContentException("The track item-information entry count does not consume its bounded payload.");
        }

        identifiers.Sort();
        if (identifiers[0] == 0)
        {
            throw new InvalidImageContentException("The track item-information box declares item identifier zero.");
        }

        for (int i = 1; i < identifiers.Length; i++)
        {
            if (identifiers[i] == identifiers[i - 1])
            {
                throw new InvalidImageContentException($"The track item-information box contains duplicate item ID {identifiers[i]}.");
            }
        }

        return result;
    }

    /// <summary>
    /// Parses one item-information entry without materializing its name or MIME strings.
    /// </summary>
    /// <param name="stream">The stream positioned at the item-information-entry full-box header.</param>
    /// <param name="boxLength">The validated item-information-entry payload length.</param>
    /// <param name="scratch">The caller-owned reusable parser scratch.</param>
    /// <param name="itemType">Receives the explicit item type for version two or three entries.</param>
    /// <param name="isXmp">Receives whether the entry declares an unencoded XMP MIME item.</param>
    /// <returns>The positive item identifier.</returns>
    private static uint ParseItemInformationEntry(
        Stream stream,
        long boxLength,
        Span<byte> scratch,
        out Heif4CharCode itemType,
        out bool isXmp)
    {
        long entryStart = stream.Position;
        long entryEnd = checked(entryStart + boxLength);
        ReadOnlySpan<byte> prefix = ReadPrefix(stream, boxLength, scratch, 4, "track item-information entry");
        byte version = prefix[0];
        int fixedLength = version switch
        {
            0 or 1 => 8,
            2 => 12,
            3 => 14,
            _ => throw new InvalidImageContentException($"The track item-information entry has unsupported version {version}.")
        };

        prefix = ReadPrefixFromStart(stream, boxLength, scratch, fixedLength, "track item-information entry");
        uint flags = BinaryPrimitives.ReadUInt32BigEndian(prefix) & 0x00FFFFFF;
        if ((flags & ~1U) != 0)
        {
            throw new InvalidImageContentException("The track item-information entry has unsupported flags.");
        }

        int itemIdOffset = 4;
        uint itemId = version == 3
            ? BinaryPrimitives.ReadUInt32BigEndian(prefix[itemIdOffset..])
            : BinaryPrimitives.ReadUInt16BigEndian(prefix[itemIdOffset..]);

        int protectionOffset = version == 3 ? 8 : 6;
        if (BinaryPrimitives.ReadUInt16BigEndian(prefix[protectionOffset..]) != 0)
        {
            throw new InvalidImageContentException($"Track metadata item {itemId} uses unsupported item protection.");
        }

        itemType = version >= 2
            ? (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(prefix[(protectionOffset + 2)..])
            : default;

        isXmp = false;
        if (version < 2 || itemType is not Heif4CharCode.Exif and not Heif4CharCode.Mime)
        {
            stream.Position = entryEnd;
            return itemId;
        }

        HeifBoxPayloadReader reader = new(stream, entryEnd - stream.Position, scratch, "track item-information entry");
        reader.SkipNullTerminatedString();
        if (itemType == Heif4CharCode.Mime)
        {
            isXmp = reader.ReadNullTerminatedStringEquals(XmpContentType);
            if (reader.Remaining > 0)
            {
                // ImageSharp cannot decode an encoded MIME payload. Retain XMP only when the optional encoding is empty.
                isXmp &= reader.ReadNullTerminatedStringEquals(ReadOnlySpan<byte>.Empty);
            }
        }

        if (!reader.IsComplete)
        {
            throw new InvalidImageContentException($"Track metadata item {itemId} contains unexpected trailing data.");
        }

        return itemId;
    }

    /// <summary>
    /// Resolves and reads one recognized metadata item while pooling only its transient extent descriptors.
    /// </summary>
    /// <param name="stream">The seekable HEIF stream.</param>
    /// <param name="itemLocations">The item-location payload range.</param>
    /// <param name="itemData">The optional item-data payload range.</param>
    /// <param name="itemId">The recognized metadata item identifier.</param>
    /// <param name="scratch">The caller-owned reusable parser scratch.</param>
    /// <returns>The exact retained item payload, or <see langword="null"/> for an item without data.</returns>
    private byte[]? ReadItemPayload(
        Stream stream,
        BoxReference itemLocations,
        BoxReference itemData,
        uint itemId,
        Span<byte> scratch)
    {
        stream.Position = itemLocations.Offset;
        ItemLocationSummary summary = ParseItemLocation(
            stream,
            itemLocations.Length,
            itemData,
            itemId,
            Span<MetadataExtent>.Empty,
            scratch);

        if (summary.ExtentCount == 0 || summary.TotalLength == 0)
        {
            return null;
        }

        using IMemoryOwner<MetadataExtent> extentOwner = this.allocator.Allocate<MetadataExtent>(summary.ExtentCount);
        Span<MetadataExtent> extents = extentOwner.GetSpan()[..summary.ExtentCount];
        stream.Position = itemLocations.Offset;
        ItemLocationSummary verifiedSummary = ParseItemLocation(stream, itemLocations.Length, itemData, itemId, extents, scratch);
        if (verifiedSummary.TotalLength != summary.TotalLength)
        {
            throw new InvalidImageContentException($"Track metadata item {itemId} changed between location parser passes.");
        }

        // The payload survives parser disposal and is handed directly to ImageSharp's metadata profile model.
        byte[] data = new byte[summary.TotalLength];
        int destinationOffset = 0;
        foreach (MetadataExtent extent in extents)
        {
            stream.Position = extent.Offset;
            HeifBoxReader.ReadExactly(
                stream,
                data.AsSpan(destinationOffset, extent.Length),
                $"Track metadata item {itemId} has a truncated extent.");

            destinationOffset += extent.Length;
        }

        return data;
    }

    /// <summary>
    /// Parses one item-location box for a selected metadata item.
    /// </summary>
    /// <param name="stream">The stream positioned at the item-location full-box header.</param>
    /// <param name="boxLength">The validated item-location payload length.</param>
    /// <param name="itemData">The optional item-data payload range.</param>
    /// <param name="targetItemId">The metadata item identifier whose extents are retained.</param>
    /// <param name="targetExtents">The exact target extent span, or an empty span for the counting pass.</param>
    /// <param name="scratch">The caller-owned reusable parser scratch.</param>
    /// <returns>The target item's extent count and total payload length.</returns>
    private static ItemLocationSummary ParseItemLocation(
        Stream stream,
        long boxLength,
        BoxReference itemData,
        uint targetItemId,
        Span<MetadataExtent> targetExtents,
        Span<byte> scratch)
    {
        HeifBoxPayloadReader reader = new(stream, boxLength, scratch, "track item location");
        uint versionAndFlags = reader.ReadUInt32();
        byte version = (byte)(versionAndFlags >> 24);
        if (version > 2 || (versionAndFlags & 0x00FFFFFF) != 0)
        {
            throw new InvalidImageContentException("The track item-location box has unsupported version or flags.");
        }

        byte offsetAndLengthSizes = reader.ReadByte();
        byte baseAndIndexSizes = reader.ReadByte();
        int offsetSize = offsetAndLengthSizes >> 4;
        int lengthSize = offsetAndLengthSizes & 15;
        int baseOffsetSize = baseAndIndexSizes >> 4;
        int indexSize = version is 1 or 2 ? baseAndIndexSizes & 15 : 0;
        if (!IsSupportedFieldSize(offsetSize)
            || !IsSupportedFieldSize(lengthSize)
            || !IsSupportedFieldSize(baseOffsetSize)
            || !IsSupportedFieldSize(indexSize))
        {
            throw new InvalidImageContentException("The track item-location box uses an unsupported integer field size.");
        }

        uint itemCount = version == 2 ? reader.ReadUInt32() : reader.ReadUInt16();
        int minimumItemLength = version == 0 ? 6 : 8;
        if (itemCount > (ulong)(reader.Remaining / minimumItemLength))
        {
            throw new InvalidImageContentException("The track item-location count exceeds its bounded payload.");
        }

        bool targetFound = false;
        int targetExtentIndex = 0;
        int targetLength = 0;
        for (uint i = 0; i < itemCount; i++)
        {
            uint itemId = version == 2 ? reader.ReadUInt32() : reader.ReadUInt16();
            int constructionMethod = 0;
            if (version is 1 or 2)
            {
                ushort constructionField = reader.ReadUInt16();
                if ((constructionField & 0xFFF0) != 0)
                {
                    throw new InvalidImageContentException("The track item-location box has nonzero reserved construction bits.");
                }

                constructionMethod = constructionField & 15;
            }

            if (constructionMethod is not 0 and not 1)
            {
                throw new InvalidImageContentException($"The track item-location box uses unsupported construction method {constructionMethod}.");
            }

            if (reader.ReadUInt16() != 0)
            {
                throw new InvalidImageContentException("External track metadata data references are not supported.");
            }

            ulong baseOffset = reader.ReadVariableUInt(baseOffsetSize);
            int extentCount = reader.ReadUInt16();
            bool isTarget = itemId == targetItemId;
            if (isTarget)
            {
                if (targetFound)
                {
                    throw new InvalidImageContentException($"The track item-location box contains duplicate locations for item ID {itemId}.");
                }

                if (!targetExtents.IsEmpty && targetExtents.Length != extentCount)
                {
                    throw new InvalidImageContentException($"Track metadata item {itemId} changed between location parser passes.");
                }

                targetFound = true;
            }

            for (int j = 0; j < extentCount; j++)
            {
                if (version is 1 or 2 && indexSize > 0)
                {
                    _ = reader.ReadVariableUInt(indexSize);
                }

                ulong extentOffset = reader.ReadVariableUInt(offsetSize);
                ulong extentLength = reader.ReadVariableUInt(lengthSize);
                if (!isTarget)
                {
                    continue;
                }

                MetadataExtent extent = ResolveExtent(stream, itemData, constructionMethod, baseOffset, extentOffset, extentLength, itemId);
                if (!targetExtents.IsEmpty)
                {
                    targetExtents[targetExtentIndex] = extent;
                }

                targetExtentIndex++;
                if (extent.Length > int.MaxValue - targetLength)
                {
                    throw new InvalidImageContentException($"Track metadata item {itemId} has an unsupported combined length.");
                }

                targetLength += extent.Length;
            }
        }

        if (!reader.IsComplete)
        {
            throw new InvalidImageContentException("The track item-location box contains unexpected trailing data.");
        }

        return targetFound ? new ItemLocationSummary(targetExtentIndex, targetLength) : default;
    }

    /// <summary>
    /// Resolves one file-relative or item-data-relative extent into an absolute stream range.
    /// </summary>
    /// <param name="stream">The complete seekable HEIF stream.</param>
    /// <param name="itemData">The optional item-data payload range.</param>
    /// <param name="constructionMethod">The item-location construction method.</param>
    /// <param name="baseOffset">The item-location base offset.</param>
    /// <param name="extentOffset">The extent offset relative to the base offset.</param>
    /// <param name="extentLength">The extent length in bytes.</param>
    /// <param name="itemId">The metadata item identifier used in malformed-image diagnostics.</param>
    /// <returns>The validated absolute extent.</returns>
    private static MetadataExtent ResolveExtent(
        Stream stream,
        BoxReference itemData,
        int constructionMethod,
        ulong baseOffset,
        ulong extentOffset,
        ulong extentLength,
        uint itemId)
    {
        if (baseOffset > ulong.MaxValue - extentOffset || extentLength > int.MaxValue)
        {
            throw new InvalidImageContentException($"Track metadata item {itemId} has an unsupported extent range.");
        }

        ulong relativeOffset = baseOffset + extentOffset;
        ulong origin = 0;
        if (constructionMethod == 1)
        {
            if (!itemData.IsPresent || relativeOffset > (ulong)itemData.Length || extentLength > (ulong)itemData.Length - relativeOffset)
            {
                throw new InvalidImageContentException($"Track metadata item {itemId} has an extent outside its item-data box.");
            }

            origin = (ulong)itemData.Offset;
        }

        if (relativeOffset > ulong.MaxValue - origin)
        {
            throw new InvalidImageContentException($"Track metadata item {itemId} has an unsupported extent offset.");
        }

        ulong absoluteOffset = origin + relativeOffset;
        if (absoluteOffset > (ulong)stream.Length || extentLength > (ulong)stream.Length - absoluteOffset)
        {
            throw new InvalidImageContentException($"Track metadata item {itemId} has an extent outside the HEIF stream.");
        }

        return new MetadataExtent((long)absoluteOffset, (int)extentLength);
    }

    /// <summary>
    /// Parses one picture handler and returns its registered handler type.
    /// </summary>
    /// <param name="stream">The stream positioned at the handler full-box header.</param>
    /// <param name="boxLength">The validated handler payload length.</param>
    /// <param name="scratch">The caller-owned reusable parser scratch.</param>
    /// <returns>The registered handler type.</returns>
    private static Heif4CharCode ParseHandler(Stream stream, long boxLength, Span<byte> scratch)
    {
        ReadOnlySpan<byte> prefix = ReadPrefix(stream, boxLength, scratch, 24, "track metadata handler");
        if (BinaryPrimitives.ReadUInt32BigEndian(prefix) != 0 || BinaryPrimitives.ReadUInt32BigEndian(prefix[4..]) != 0)
        {
            throw new InvalidImageContentException("The track metadata handler has unsupported fields.");
        }

        return (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(prefix[8..]);
    }

    /// <summary>
    /// Determines whether an item-location integer width is supported by the bounded reader.
    /// </summary>
    /// <param name="size">The width in bytes.</param>
    /// <returns><see langword="true"/> for zero-width, 32-bit, or 64-bit fields.</returns>
    private static bool IsSupportedFieldSize(int size) => size is 0 or 4 or 8;

    /// <summary>
    /// Reads a fixed prefix from the stream's current position.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <param name="boxLength">The validated enclosing payload length.</param>
    /// <param name="scratch">The caller-owned reusable parser scratch.</param>
    /// <param name="length">The required prefix length.</param>
    /// <param name="name">The payload name used in malformed-image diagnostics.</param>
    /// <returns>The requested prefix within <paramref name="scratch"/>.</returns>
    private static ReadOnlySpan<byte> ReadPrefix(Stream stream, long boxLength, Span<byte> scratch, int length, string name)
    {
        if (boxLength < length)
        {
            throw new InvalidImageContentException($"The {name} payload is truncated.");
        }

        Span<byte> destination = scratch[..length];
        HeifBoxReader.ReadExactly(stream, destination, $"The {name} payload is truncated.");
        return destination;
    }

    /// <summary>
    /// Rewinds to the start of a partially read payload and reads a larger fixed prefix.
    /// </summary>
    /// <param name="stream">The source stream positioned after a four-byte prefix.</param>
    /// <param name="boxLength">The validated enclosing payload length.</param>
    /// <param name="scratch">The caller-owned reusable parser scratch.</param>
    /// <param name="length">The required prefix length.</param>
    /// <param name="name">The payload name used in malformed-image diagnostics.</param>
    /// <returns>The requested prefix within <paramref name="scratch"/>.</returns>
    private static ReadOnlySpan<byte> ReadPrefixFromStart(Stream stream, long boxLength, Span<byte> scratch, int length, string name)
    {
        stream.Position -= 4;
        return ReadPrefix(stream, boxLength, scratch, length, name);
    }

    /// <summary>
    /// Records one unique metadata child while retaining only its stream range.
    /// </summary>
    /// <param name="reference">The child reference owned by the metadata parser.</param>
    /// <param name="offset">The absolute payload offset.</param>
    /// <param name="length">The validated payload length.</param>
    /// <param name="boxType">The unique child box type.</param>
    private static void SetUnique(ref BoxReference reference, long offset, long length, Heif4CharCode boxType)
    {
        if (reference.IsPresent)
        {
            throw new InvalidImageContentException($"The track metadata box contains duplicate '{boxType}' boxes.");
        }

        reference = new BoxReference(offset, length);
    }

    /// <summary>
    /// Retains the recognized item identifiers from one track metadata box.
    /// </summary>
    private struct MetadataItemIds
    {
        /// <summary>
        /// Gets or sets the Exif item identifier, or zero when absent.
        /// </summary>
        public uint ExifItemId { get; set; }

        /// <summary>
        /// Gets or sets the XMP item identifier, or zero when absent.
        /// </summary>
        public uint XmpItemId { get; set; }
    }

    /// <summary>
    /// Retains one unique child payload range without creating a generic metadata box model.
    /// </summary>
    private readonly struct BoxReference
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BoxReference"/> struct.
        /// </summary>
        /// <param name="offset">The absolute payload offset.</param>
        /// <param name="length">The validated payload length.</param>
        public BoxReference(long offset, long length)
        {
            this.Offset = offset;
            this.Length = length;
            this.IsPresent = true;
        }

        /// <summary>
        /// Gets the absolute payload offset.
        /// </summary>
        public long Offset { get; }

        /// <summary>
        /// Gets the validated payload length.
        /// </summary>
        public long Length { get; }

        /// <summary>
        /// Gets a value indicating whether the child was present.
        /// </summary>
        public bool IsPresent { get; }
    }

    /// <summary>
    /// Describes one absolute metadata item extent.
    /// </summary>
    private readonly struct MetadataExtent
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MetadataExtent"/> struct.
        /// </summary>
        /// <param name="offset">The absolute stream offset.</param>
        /// <param name="length">The extent length in bytes.</param>
        public MetadataExtent(long offset, int length)
        {
            this.Offset = offset;
            this.Length = length;
        }

        /// <summary>
        /// Gets the absolute stream offset.
        /// </summary>
        public long Offset { get; }

        /// <summary>
        /// Gets the extent length in bytes.
        /// </summary>
        public int Length { get; }
    }

    /// <summary>
    /// Summarizes the retained extents for one metadata item.
    /// </summary>
    private readonly struct ItemLocationSummary
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ItemLocationSummary"/> struct.
        /// </summary>
        /// <param name="extentCount">The number of retained extents.</param>
        /// <param name="totalLength">The combined payload length.</param>
        public ItemLocationSummary(int extentCount, int totalLength)
        {
            this.ExtentCount = extentCount;
            this.TotalLength = totalLength;
        }

        /// <summary>
        /// Gets the number of retained extents.
        /// </summary>
        public int ExtentCount { get; }

        /// <summary>
        /// Gets the combined payload length.
        /// </summary>
        public int TotalLength { get; }
    }
}
