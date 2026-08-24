// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
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
    /// Defines the dependency order in which recognized metadata children are interpreted.
    /// </summary>
    private static readonly Heif4CharCode[] MetadataParseOrder =
    [
        Heif4CharCode.Hdlr,
        Heif4CharCode.Iinf,
        Heif4CharCode.Pitm,
        Heif4CharCode.Iref,
        Heif4CharCode.Iloc,
        Heif4CharCode.Iprp,
        Heif4CharCode.Idat
    ];

    /// <summary>
    /// The general configuration.
    /// </summary>
    private readonly Configuration configuration;

    /// <summary>
    /// The <see cref="ImageMetadata"/> decoded by this decoder instance.
    /// </summary>
    private readonly ImageMetadata metadata;

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
    /// The codec configuration associated with the current AV1 item.
    /// </summary>
    private Av1CodecConfiguration av1CodecConfiguration;

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
        this.metadata = new ImageMetadata();
        this.items = [];
        this.itemLinks = [];
    }

    /// <inheritdoc/>
    protected override Image<TPixel> Decode<TPixel>(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        if (!this.CheckFileTypeBox(stream))
        {
            throw new ImageFormatException("Not an HEIF image.");
        }

        this.items.Clear();
        this.itemLinks.Clear();
        this.itemDataOffset = -1;
        this.itemDataLength = 0;

        // Item locations are absolute file offsets or idat-relative offsets, so payload bytes need not be adjacent to
        // the metadata box. Complete the top-level scan before resolving and decoding the primary item.
        while (stream.Position < stream.Length)
        {
            long boxLength = this.ReadBoxHeader(stream, stream.Length, out Heif4CharCode boxType, true);
            switch (boxType)
            {
                case Heif4CharCode.Meta:
                    this.ParseMetadata(stream, boxLength);
                    break;
                case Heif4CharCode.Mdat:
                case Heif4CharCode.Free:
                    SkipBox(stream, boxLength);
                    break;
                case 0U:
                    // Some files have trailing zeros, skiping to EOF.
                    SkipBox(stream, stream.Length - stream.Position);
                    break;
                default:
                    SkipBox(stream, boxLength);
                    break;
            }
        }

        return this.DecodePrimaryItem<TPixel>(stream);
    }

    /// <inheritdoc/>
    protected override ImageInfo Identify(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        if (!this.CheckFileTypeBox(stream))
        {
            throw new ImageFormatException("Not an HEIF image.");
        }

        this.items.Clear();
        this.itemLinks.Clear();
        this.itemDataOffset = -1;
        this.itemDataLength = 0;

        // Identification reads only the container model. Payload boxes remain skipped because dimensions and format
        // metadata come from item declarations and associated properties rather than reconstructed pixels.
        while (stream.Position < stream.Length)
        {
            long boxLength = this.ReadBoxHeader(stream, stream.Length, out Heif4CharCode boxType, true);
            switch (boxType)
            {
                case Heif4CharCode.Meta:
                    this.ParseMetadata(stream, boxLength);
                    break;
                default:
                    // Silently skip all other box types.
                    SkipBox(stream, boxLength);
                    break;
            }
        }

        HeifItem? item = this.FindItemById(this.primaryItem);
        if (item is null)
        {
            throw new ImageFormatException("No primary item found");
        }

        this.UpdateMetadata(this.metadata, item);

        return new ImageInfo(new(item.Extent.Width, item.Extent.Height), this.metadata);
    }

    /// <summary>
    /// Reads and validates the leading file-type box against the still-image brands supported by this decoder.
    /// </summary>
    /// <param name="stream">The container stream positioned at its first top-level box.</param>
    /// <returns><see langword="true"/> when the complete file-type payload advertises a supported still-image brand.</returns>
    private bool CheckFileTypeBox(BufferedReadStream stream)
    {
        long boxLength = this.ReadBoxHeader(stream, stream.Length, out Heif4CharCode boxType, true);
        if (boxType != Heif4CharCode.Ftyp)
        {
            return false;
        }

        if (boxLength < 8 || boxLength > int.MaxValue || (boxLength & 3) != 0)
        {
            return false;
        }

        using IMemoryOwner<byte> boxMemory = this.ReadIntoBuffer(stream, boxLength);
        Span<byte> boxBuffer = boxMemory.GetSpan();
        return HeifConstants.IsSupportedFileType(boxBuffer);
    }

    /// <summary>
    /// Updates identification metadata from the primary item or its decodable thumbnail fallback.
    /// </summary>
    /// <param name="metadata">The destination image metadata.</param>
    /// <param name="item">The primary item whose visible representation is being identified.</param>
    private void UpdateMetadata(ImageMetadata metadata, HeifItem item)
    {
        HeifItem metadataItem = item;
        if (item.Type == Heif4CharCode.Grid)
        {
            // A grid is a derived image rather than a compression method. Its dimg references identify the coded
            // tile items whose decoder determines the compression reported for the primary presentation.
            HeifItemLink? derivedImageReference = this.itemLinks.FirstOrDefault(
                link => link.Type == Heif4CharCode.Dimg && link.SourceId == item.Id);

            if (derivedImageReference is not null)
            {
                HeifItem? tileItem = derivedImageReference.DestinationIds
                    .Select(this.FindItemById)
                    .FirstOrDefault(candidate => candidate is not null && HeifCompressionFactory.GetDecoder<Rgba32>(candidate.Type) is not null);

                if (tileItem is not null)
                {
                    metadataItem = tileItem;
                }
            }
        }
        else if (HeifCompressionFactory.GetDecoder<Rgba32>(item.Type) is null)
        {
            // A thumbnail reference points from the thumbnail item to the master image. Restrict fallback metadata
            // to a thumbnail of this primary item rather than allowing an unrelated thumbnail to relabel it.
            HeifItemLink? thumbnailReference = this.itemLinks.FirstOrDefault(
                link => link.Type == Heif4CharCode.Thmb && link.DestinationIds.Contains(item.Id));

            if (thumbnailReference is not null)
            {
                HeifItem? thumbnailItem = this.FindItemById(thumbnailReference.SourceId);
                if (thumbnailItem is not null && HeifCompressionFactory.GetDecoder<Rgba32>(thumbnailItem.Type) is not null)
                {
                    metadataItem = thumbnailItem;
                }
            }
        }

        HeifMetadata meta = metadata.GetHeifMetadata();
        HeifCompressionMethod compressionMethod = HeifCompressionMethod.Hevc;
        if (metadataItem.Type == Heif4CharCode.Av01)
        {
            compressionMethod = HeifCompressionMethod.Av1;
        }
        else if (metadataItem.Type == Heif4CharCode.Jpeg)
        {
            compressionMethod = HeifCompressionMethod.LegacyJpeg;
        }

        meta.CompressionMethod = compressionMethod;
    }

    /// <summary>
    /// Reads an ISO BMFF box header and resolves its validated payload length.
    /// </summary>
    /// <param name="stream">The stream positioned at the box size field.</param>
    /// <param name="parentEndPosition">The absolute end position of the containing box or file.</param>
    /// <param name="boxType">Receives the box four-character code.</param>
    /// <param name="topLevel">Indicates whether a size-zero box may extend to the end of the file.</param>
    /// <returns>The number of payload bytes following the complete variable-length header.</returns>
    private long ReadBoxHeader(BufferedReadStream stream, long parentEndPosition, out Heif4CharCode boxType, bool topLevel = false)
    {
        if (parentEndPosition - stream.Position < 8)
        {
            throw new InvalidImageContentException("Not enough data to read the box header.");
        }

        Span<byte> buf = stackalloc byte[8];
        int bytesRead = stream.Read(buf);
        if (bytesRead != 8)
        {
            throw new InvalidImageContentException("Not enough data to read the box header.");
        }

        ulong boxSize = BinaryPrimitives.ReadUInt32BigEndian(buf);
        int headerSize = 8;
        boxType = (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(buf[4..]);

        if (boxSize == 1)
        {
            // A 32-bit size value of one replaces the size field with the following unsigned 64-bit largesize value.
            if (parentEndPosition - stream.Position < 8)
            {
                throw new InvalidImageContentException("Not enough data to read the extended box size.");
            }

            bytesRead = stream.Read(buf);
            if (bytesRead != 8)
            {
                throw new InvalidImageContentException("Not enough data to read the extended box size.");
            }

            boxSize = BinaryPrimitives.ReadUInt64BigEndian(buf);
            headerSize += 8;
        }

        if (boxType == Heif4CharCode.Uuid)
        {
            if (parentEndPosition - stream.Position < 16)
            {
                throw new InvalidImageContentException("Not enough data to read the UUID box user type.");
            }

            // The UUID user type is part of the variable-sized box header, even though this decoder skips its value.
            SkipBox(stream, 16);
            headerSize += 16;
        }

        if (boxSize == 0)
        {
            // ISO BMFF permits a size-zero box only at file level, where it consumes the rest of the file.
            if (!topLevel)
            {
                throw new InvalidImageContentException("A nested box cannot extend to the end of the file.");
            }

            return parentEndPosition - stream.Position;
        }

        if (boxSize < (ulong)headerSize)
        {
            throw new InvalidImageContentException("Box size is smaller than its header.");
        }

        ulong contentLength = boxSize - (ulong)headerSize;
        if (contentLength > (ulong)(parentEndPosition - stream.Position))
        {
            throw new InvalidImageContentException("Box size extends beyond its parent boundary.");
        }

        return (long)contentLength;
    }

    /// <summary>
    /// Parses an ISO BMFF child-box header from a bounded parent payload.
    /// </summary>
    /// <param name="buffer">The remaining bytes in the parent payload, beginning at the child size field.</param>
    /// <param name="length">Receives the validated child payload length.</param>
    /// <param name="boxType">Receives the child box four-character code.</param>
    /// <returns>The number of bytes occupied by the complete child header.</returns>
    private static int ParseBoxHeader(Span<byte> buffer, out long length, out Heif4CharCode boxType)
    {
        if (buffer.Length < 8)
        {
            throw new InvalidImageContentException("Not enough data to read the box header.");
        }

        ulong boxSize = BinaryPrimitives.ReadUInt32BigEndian(buffer);
        int bytesRead = 8;
        boxType = (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(buffer[4..]);
        if (boxSize == 1)
        {
            if (buffer.Length < 16)
            {
                throw new InvalidImageContentException("Not enough data to read the extended box size.");
            }

            boxSize = BinaryPrimitives.ReadUInt64BigEndian(buffer[bytesRead..]);
            bytesRead += 8;
        }

        if (boxType == Heif4CharCode.Uuid)
        {
            if (buffer.Length - bytesRead < 16)
            {
                throw new InvalidImageContentException("Not enough data to read the UUID box user type.");
            }

            bytesRead += 16;
        }

        if (boxSize == 0)
        {
            throw new InvalidImageContentException("A nested box cannot extend to the end of the file.");
        }

        if (boxSize < (ulong)bytesRead)
        {
            throw new InvalidImageContentException("Box size is smaller than its header.");
        }

        ulong contentLength = boxSize - (ulong)bytesRead;
        if (contentLength > (ulong)(buffer.Length - bytesRead))
        {
            throw new InvalidImageContentException("Box size extends beyond its parent boundary.");
        }

        length = (long)contentLength;
        return bytesRead;
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
            long length = this.ReadBoxHeader(stream, endPosition, out Heif4CharCode boxType);
            if (Array.IndexOf(MetadataParseOrder, boxType) >= 0)
            {
                // Association and location boxes can precede the item declarations they reference.
                if (!boxes.TryAdd(boxType, (stream.Position, length)))
                {
                    throw new InvalidImageContentException($"The metadata box contains duplicate '{PrettyPrint(boxType)}' boxes.");
                }
            }

            SkipBox(stream, length);
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
        using IMemoryOwner<byte> boxMemory = this.ReadIntoBuffer(stream, boxLength);
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
        using IMemoryOwner<byte> boxMemory = this.ReadIntoBuffer(stream, boxLength);
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
        int headerLength = ParseBoxHeader(buffer, out long boxLength, out Heif4CharCode boxType);
        if (boxType != Heif4CharCode.Infe)
        {
            throw new InvalidImageContentException($"The item info box contains unexpected child '{PrettyPrint(boxType)}'.");
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
        using IMemoryOwner<byte> boxMemory = this.ReadIntoBuffer(stream, boxLength);
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
            int referenceHeaderLength = ParseBoxHeader(boxBuffer[bytesRead..], out long referenceLength, out Heif4CharCode linkType);
            int referenceEnd = checked(bytesRead + referenceHeaderLength + (int)referenceLength);
            Span<byte> referenceBuffer = boxBuffer[..referenceEnd];
            bytesRead += referenceHeaderLength;
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
                throw new InvalidImageContentException($"The '{PrettyPrint(linkType)}' item reference length does not match its entry count.");
            }

            this.itemLinks.Add(link);
        }
    }

    /// <summary>
    /// Reads the identifier of the presentation's primary item.
    /// </summary>
    /// <param name="stream">The stream positioned at the primary-item full-box payload.</param>
    /// <param name="boxLength">The bounded primary-item payload length.</param>
    private void ParsePrimaryItem(BufferedReadStream stream, long boxLength)
    {
        using IMemoryOwner<byte> boxMemory = this.ReadIntoBuffer(stream, boxLength);
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
            long containerLength = this.ReadBoxHeader(stream, endBoxPosition, out Heif4CharCode containerType);
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
            SkipBox(stream, containerLength);
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
            long itemLength = this.ReadBoxHeader(stream, endPosition, out Heif4CharCode itemType);
            using IMemoryOwner<byte> boxMemory = this.ReadIntoBuffer(stream, itemLength);
            Span<byte> boxBuffer = boxMemory.GetSpan();
            switch (itemType)
            {
                case Heif4CharCode.Ispe:
                    EnsureBufferRemaining(boxBuffer, 0, 12, "image spatial extents");

                    // The full-box header precedes the unsigned display width and height.
                    int width = (int)BinaryPrimitives.ReadUInt32BigEndian(boxBuffer[4..]);
                    int height = (int)BinaryPrimitives.ReadUInt32BigEndian(boxBuffer[8..]);
                    properties.Add(new KeyValuePair<Heif4CharCode, object>(Heif4CharCode.Ispe, new Size(width, height)));
                    break;
                case Heif4CharCode.Pasp:
                    EnsureBufferRemaining(boxBuffer, 0, 8, "pixel aspect ratio");
                    int horizontalSpacing = (int)BinaryPrimitives.ReadUInt32BigEndian(boxBuffer);
                    int verticalSpacing = (int)BinaryPrimitives.ReadUInt32BigEndian(boxBuffer[4..]);
                    properties.Add(new KeyValuePair<Heif4CharCode, object>(Heif4CharCode.Pasp, new Size(horizontalSpacing, verticalSpacing)));
                    break;
                case Heif4CharCode.Pixi:
                    EnsureBufferRemaining(boxBuffer, 0, 5, "pixel information");

                    // The full-box header precedes one bit-depth byte for each channel.
                    int channelCount = boxBuffer[4];
                    int offset = 5;
                    EnsureBufferRemaining(boxBuffer, offset, channelCount, "pixel information");
                    int bitsPerPixel = 0;
                    for (int i = 0; i < channelCount; i++)
                    {
                        bitsPerPixel += boxBuffer[offset + i];
                    }

                    properties.Add(new KeyValuePair<Heif4CharCode, object>(Heif4CharCode.Pixi, new int[] { channelCount, bitsPerPixel }));

                    break;
                case Heif4CharCode.Colr:
                    EnsureBufferRemaining(boxBuffer, 0, 4, "color information");
                    Heif4CharCode profileType = (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(boxBuffer);
                    if (profileType is Heif4CharCode.RICC or Heif4CharCode.Prof)
                    {
                        byte[] iccData = new byte[(int)itemLength - 4];
                        boxBuffer[4..].CopyTo(iccData);
                        this.metadata.IccProfile = new IccProfile(iccData);
                    }

                    // Property indices refer to every box in ipco, including properties handled directly while parsing.
                    properties.Add(new KeyValuePair<Heif4CharCode, object>(Heif4CharCode.Colr, new object()));

                    break;
                case Heif4CharCode.Av1C:
                    EnsureBufferRemaining(boxBuffer, 0, 4, "AV1 codec configuration");
                    this.av1CodecConfiguration = new(boxBuffer);
                    properties.Add(new KeyValuePair<Heif4CharCode, object>(Heif4CharCode.Av1C, new object()));
                    break;
                case Heif4CharCode.Altt:
                case Heif4CharCode.Imir:
                case Heif4CharCode.Irot:
                case Heif4CharCode.Iscl:
                case Heif4CharCode.HvcC:
                case Heif4CharCode.Rloc:
                case Heif4CharCode.Udes:
                    // These registered image properties are not arbitrary unknown boxes. Preserve their indices so
                    // container identification remains available while their owning image stage handles the value.
                    properties.Add(new KeyValuePair<Heif4CharCode, object>(itemType, new object()));
                    break;
                default:
                    // Unknown properties still occupy an ipco index and become an error only when marked essential.
                    properties.Add(new KeyValuePair<Heif4CharCode, object>(itemType, UnknownProperty));
                    break;
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
        using IMemoryOwner<byte> boxMemory = this.ReadIntoBuffer(stream, boxLength);
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
                    throw new InvalidImageContentException($"Item {itemId} references property index {propertyIndex + 1}, but only {properties.Count} properties exist.");
                }

                KeyValuePair<Heif4CharCode, object> prop = properties[(int)propertyIndex];
                if (essential && ReferenceEquals(prop.Value, UnknownProperty))
                {
                    throw new InvalidImageContentException($"Item {itemId} associates unknown essential property '{PrettyPrint(prop.Key)}'.");
                }

                switch (prop.Key)
                {
                    case Heif4CharCode.Ispe:
                        item.SetExtent((Size)prop.Value);
                        break;
                    case Heif4CharCode.Pasp:
                        item.PixelAspectRatio = (Size)prop.Value;
                        break;
                    case Heif4CharCode.Pixi:
                        int[] values = (int[])prop.Value;
                        item.ChannelCount = values[0];
                        item.BitsPerPixel = values[1];
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
        using IMemoryOwner<byte> boxMemory = this.ReadIntoBuffer(stream, boxLength);
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
    /// <returns>The image reconstructed from the selected item.</returns>
    private Image<TPixel> DecodePrimaryItem<TPixel>(BufferedReadStream stream)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using DisposableDictionary<uint, IMemoryOwner<byte>> buffers = new(this.items.Count);
        foreach (HeifItem item in this.items)
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
            IMemoryOwner<byte> extentMemory = this.configuration.MemoryAllocator.Allocate<byte>(bufferLength);
            buffers.Add(item.Id, extentMemory);
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

                EnsureBoxInsideParent(loc.Length, sourceBytesRemaining);
                stream.Position = sourceOffset;
                int extentLength = (int)loc.Length;
                int bytesRead = stream.Read(itemBuffer.Slice(writeOffset, extentLength));
                if (bytesRead != extentLength)
                {
                    throw new InvalidImageContentException($"Item {item.Id} extent is truncated.");
                }

                writeOffset += extentLength;
            }
        }

        HeifItem? rootItem = this.FindItemById(this.primaryItem);
        if (rootItem is null)
        {
            throw new ImageFormatException("No primary HEIF item defined.");
        }

        IHeifItemDecoder<TPixel>? itemDecoder = rootItem.Type == Heif4CharCode.Grid
            ? new GridHeifItemDecoder<TPixel>(this.configuration, this.items, this.itemLinks, buffers)
            : HeifCompressionFactory.GetDecoder<TPixel>(rootItem.Type);
        HeifItem itemToDecode = rootItem;
        if (itemDecoder is null)
        {
            // Unable to decode the primary image, decode the thumbnail instead.
            HeifItemLink? thumbLink = this.itemLinks.FirstOrDefault(
                link => link.Type == Heif4CharCode.Thmb && link.DestinationIds.Contains(rootItem.Id));

            if (thumbLink is not null)
            {
                HeifItem? thumbItem = this.FindItemById(thumbLink.SourceId);
                if (thumbItem is not null)
                {
                    itemDecoder = HeifCompressionFactory.GetDecoder<TPixel>(thumbItem.Type);
                    if (itemDecoder is not null)
                    {
                        itemToDecode = thumbItem;
                    }
                }
            }
        }

        if (itemDecoder is null)
        {
            throw new ImageFormatException("No decodable item found inside this HEIF container.");
        }

        if (!buffers.TryGetValue(itemToDecode.Id, out IMemoryOwner<byte>? itemMemory))
        {
            throw new InvalidImageContentException($"Item {itemToDecode.Id} has no data extents.");
        }

        Image<TPixel> image = itemDecoder.DecodeItemData(this.configuration, itemToDecode, itemMemory.GetSpan());

        // The decoder determines the compression of the pixels that were actually returned, including grid tiles
        // and a thumbnail fallback when the primary image compression is not available.
        HeifMetadata meta = image.Metadata.GetHeifMetadata();
        meta.CompressionMethod = itemDecoder.CompressionMethod;
        return image;
    }

    /// <summary>
    /// Validates that a fixed-width field remains within a buffered box payload.
    /// </summary>
    /// <param name="buffer">The bounded box payload.</param>
    /// <param name="offset">The zero-based field offset.</param>
    /// <param name="count">The field width in bytes.</param>
    /// <param name="boxName">The diagnostic name used for malformed input errors.</param>
    private static void EnsureBufferRemaining(Span<byte> buffer, int offset, int count, string boxName)
    {
        if ((uint)offset > (uint)buffer.Length || (uint)count > (uint)(buffer.Length - offset))
        {
            throw new InvalidImageContentException($"The {boxName} box is truncated.");
        }
    }

    /// <summary>
    /// Advances over a box payload without narrowing its 64-bit length.
    /// </summary>
    /// <param name="stream">The seekable container stream.</param>
    /// <param name="boxLength">The validated payload length.</param>
    private static void SkipBox(Stream stream, long boxLength)
        => stream.Seek(boxLength, SeekOrigin.Current);

    /// <summary>
    /// Reads a complete bounded box payload into allocator-owned memory.
    /// </summary>
    /// <param name="stream">The stream positioned at the payload start.</param>
    /// <param name="length">The validated payload length.</param>
    /// <returns>An owner containing exactly the requested payload bytes.</returns>
    private IMemoryOwner<byte> ReadIntoBuffer(Stream stream, long length)
    {
        if ((ulong)length > int.MaxValue)
        {
            throw new InvalidImageContentException("Box content is too large to buffer.");
        }

        int bufferLength = (int)length;
        IMemoryOwner<byte> buffer = this.configuration.MemoryAllocator.Allocate<byte>(bufferLength);
        int bytesRead = stream.Read(buffer.GetSpan());
        if (bytesRead != bufferLength)
        {
            throw new InvalidImageContentException("Stream length is not sufficient for box content.");
        }

        return buffer;
    }

    /// <summary>
    /// Validates a box payload length against the bytes remaining in the file.
    /// </summary>
    /// <param name="boxLength">The declared box payload length.</param>
    /// <param name="stream">The stream positioned at the payload start.</param>
    private static void EnsureBoxBoundary(long boxLength, Stream stream)
        => EnsureBoxInsideParent(boxLength, stream.Length - stream.Position);

    /// <summary>
    /// Validates a child payload length against its remaining parent payload.
    /// </summary>
    /// <param name="boxLength">The declared child payload length.</param>
    /// <param name="parentLength">The number of bytes remaining in the parent.</param>
    private static void EnsureBoxInsideParent(long boxLength, long parentLength)
    {
        if (boxLength < 0 || parentLength < 0 || boxLength > parentLength)
        {
            throw new InvalidImageContentException("Box size extends beyond its parent boundary.");
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

    /// <summary>
    /// Formats a known enum name or an unknown four-character code for diagnostics.
    /// </summary>
    /// <param name="code">The box, property, brand, or item code.</param>
    /// <returns>A readable enum name or four-character ASCII value.</returns>
    private static string PrettyPrint(Heif4CharCode code)
    {
        string? pretty = Enum.GetName(code);
        if (string.IsNullOrEmpty(pretty))
        {
            Span<byte> bytes = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(bytes, (uint)code);
            pretty = Encoding.ASCII.GetString(bytes);
        }

        return pretty;
    }
}
