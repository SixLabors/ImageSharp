// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Heif.Av1;
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

        Size presentationExtent = GetPresentationExtent(item);
        return new ImageInfo(new(presentationExtent.Width, presentationExtent.Height), this.metadata);
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
                    uint width = BinaryPrimitives.ReadUInt32BigEndian(boxBuffer[4..]);
                    uint height = BinaryPrimitives.ReadUInt32BigEndian(boxBuffer[8..]);
                    if (width is 0 or > int.MaxValue || height is 0 or > int.MaxValue)
                    {
                        throw new InvalidImageContentException("The image spatial extents property has invalid dimensions.");
                    }

                    properties.Add(new KeyValuePair<Heif4CharCode, object>(Heif4CharCode.Ispe, new Size((int)width, (int)height)));
                    break;
                case Heif4CharCode.Pasp:
                    EnsureBufferRemaining(boxBuffer, 0, 8, "pixel aspect ratio");
                    uint horizontalSpacing = BinaryPrimitives.ReadUInt32BigEndian(boxBuffer);
                    uint verticalSpacing = BinaryPrimitives.ReadUInt32BigEndian(boxBuffer[4..]);
                    if (horizontalSpacing == 0 || verticalSpacing == 0)
                    {
                        throw new InvalidImageContentException("The pixel aspect ratio property has zero spacing.");
                    }

                    properties.Add(
                        new KeyValuePair<Heif4CharCode, object>(
                            Heif4CharCode.Pasp,
                            new HeifPixelAspectRatio(horizontalSpacing, verticalSpacing)));

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

                    byte[] channelBitDepths = boxBuffer.Slice(offset, channelCount).ToArray();
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
                        EnsureBufferRemaining(boxBuffer, 4, 1, "ICC color information");
                        byte[] iccData = boxBuffer[4..].ToArray();
                        IccProfile? iccProfile = null;
                        this.ExecuteAncillarySegmentAction(() =>
                        {
                            IccProfile candidate = new(iccData);
                            if (!candidate.CheckIsValid())
                            {
                                throw new InvalidIccProfileException("Invalid HEIF ICC profile.");
                            }

                            iccProfile = candidate;
                        });

                        // A malformed ancillary profile can be ignored by policy while the physical property still
                        // occupies its ipco index and remains understood for essential-association handling.
                        colorInformation = iccProfile ?? new object();
                    }
                    else if (profileType == Heif4CharCode.Nclx)
                    {
                        EnsureBufferRemaining(boxBuffer, 4, 7, "CICP color information");
                        ushort colorPrimaries = BinaryPrimitives.ReadUInt16BigEndian(boxBuffer[4..]);
                        ushort transferCharacteristics = BinaryPrimitives.ReadUInt16BigEndian(boxBuffer[6..]);
                        ushort matrixCoefficients = BinaryPrimitives.ReadUInt16BigEndian(boxBuffer[8..]);
                        byte rangeAndReserved = boxBuffer[10];
                        if ((rangeAndReserved & 0x7F) != 0)
                        {
                            throw new InvalidImageContentException("The HEIF CICP color property has nonzero reserved bits.");
                        }

                        // The box fields are 16-bit so future registrations remain representable. ImageSharp's CICP
                        // profile exposes the currently registered byte-sized H.273 values and maps others to unspecified.
                        byte colorPrimariesValue = colorPrimaries <= byte.MaxValue
                            ? (byte)colorPrimaries
                            : (byte)CicpColorPrimaries.Unspecified;

                        byte transferCharacteristicsValue = transferCharacteristics <= byte.MaxValue
                            ? (byte)transferCharacteristics
                            : (byte)CicpTransferCharacteristics.Unspecified;

                        byte matrixCoefficientsValue = matrixCoefficients <= byte.MaxValue
                            ? (byte)matrixCoefficients
                            : (byte)CicpMatrixCoefficients.Unspecified;

                        colorInformation = new CicpProfile(
                            colorPrimariesValue,
                            transferCharacteristicsValue,
                            matrixCoefficientsValue,
                            (rangeAndReserved & 0x80) != 0);
                    }

                    properties.Add(new KeyValuePair<Heif4CharCode, object>(Heif4CharCode.Colr, colorInformation));

                    break;
                case Heif4CharCode.Clli:
                    EnsureBufferRemaining(boxBuffer, 0, 4, "content light level information");
                    if (boxBuffer.Length != 4)
                    {
                        throw new InvalidImageContentException("The content light level property has an invalid length.");
                    }

                    properties.Add(
                        new KeyValuePair<Heif4CharCode, object>(
                            Heif4CharCode.Clli,
                            new HeifContentLightLevel(
                                BinaryPrimitives.ReadUInt16BigEndian(boxBuffer),
                                BinaryPrimitives.ReadUInt16BigEndian(boxBuffer[2..]))));

                    break;
                case Heif4CharCode.Mdcv:
                    EnsureBufferRemaining(boxBuffer, 0, 24, "mastering display color volume");
                    if (boxBuffer.Length != 24)
                    {
                        throw new InvalidImageContentException("The mastering display color-volume property has an invalid length.");
                    }

                    const float chromaticityScale = 1F / 50000F;
                    const double luminanceScale = 1D / 10000D;

                    // The registered mastering-display payload inherits the G, B, R primary order used by its
                    // mastering-display source syntax. Reorder it into ImageSharp's existing RGB coordinate type.
                    CieXyChromaticityCoordinates greenPrimary = new(
                        BinaryPrimitives.ReadUInt16BigEndian(boxBuffer) * chromaticityScale,
                        BinaryPrimitives.ReadUInt16BigEndian(boxBuffer[2..]) * chromaticityScale);

                    CieXyChromaticityCoordinates bluePrimary = new(
                        BinaryPrimitives.ReadUInt16BigEndian(boxBuffer[4..]) * chromaticityScale,
                        BinaryPrimitives.ReadUInt16BigEndian(boxBuffer[6..]) * chromaticityScale);

                    CieXyChromaticityCoordinates redPrimary = new(
                        BinaryPrimitives.ReadUInt16BigEndian(boxBuffer[8..]) * chromaticityScale,
                        BinaryPrimitives.ReadUInt16BigEndian(boxBuffer[10..]) * chromaticityScale);

                    properties.Add(
                        new KeyValuePair<Heif4CharCode, object>(
                            Heif4CharCode.Mdcv,
                            new HeifMasteringDisplayColorVolume(
                                new RgbPrimariesChromaticityCoordinates(redPrimary, greenPrimary, bluePrimary),
                                new CieXyChromaticityCoordinates(
                                    BinaryPrimitives.ReadUInt16BigEndian(boxBuffer[12..]) * chromaticityScale,
                                    BinaryPrimitives.ReadUInt16BigEndian(boxBuffer[14..]) * chromaticityScale),
                                BinaryPrimitives.ReadUInt32BigEndian(boxBuffer[16..]) * luminanceScale,
                                BinaryPrimitives.ReadUInt32BigEndian(boxBuffer[20..]) * luminanceScale)));

                    break;
                case Heif4CharCode.Cclv:
                    EnsureBufferRemaining(boxBuffer, 0, 1, "content color volume");
                    byte contentColorVolumeFlags = boxBuffer[0];
                    if ((contentColorVolumeFlags & 0xC3) != 0)
                    {
                        throw new InvalidImageContentException("The content color-volume property has nonzero reserved flags.");
                    }

                    bool contentPrimariesPresent = (contentColorVolumeFlags & 0x20) != 0;
                    bool minimumLuminancePresent = (contentColorVolumeFlags & 0x10) != 0;
                    bool maximumLuminancePresent = (contentColorVolumeFlags & 0x08) != 0;
                    bool averageLuminancePresent = (contentColorVolumeFlags & 0x04) != 0;
                    if (!contentPrimariesPresent
                        && !minimumLuminancePresent
                        && !maximumLuminancePresent
                        && !averageLuminancePresent)
                    {
                        throw new InvalidImageContentException("The content color-volume property does not describe any values.");
                    }

                    int expectedContentColorVolumeLength = 1
                        + (contentPrimariesPresent ? 24 : 0)
                        + (minimumLuminancePresent ? 4 : 0)
                        + (maximumLuminancePresent ? 4 : 0)
                        + (averageLuminancePresent ? 4 : 0);

                    if (boxBuffer.Length != expectedContentColorVolumeLength)
                    {
                        throw new InvalidImageContentException("The content color-volume property has an invalid length.");
                    }

                    int contentColorVolumeOffset = 1;
                    RgbPrimariesChromaticityCoordinates? contentPrimaries = null;
                    if (contentPrimariesPresent)
                    {
                        int greenPrimaryX = BinaryPrimitives.ReadInt32BigEndian(boxBuffer[contentColorVolumeOffset..]);
                        int greenPrimaryY = BinaryPrimitives.ReadInt32BigEndian(boxBuffer[(contentColorVolumeOffset + 4)..]);
                        int bluePrimaryX = BinaryPrimitives.ReadInt32BigEndian(boxBuffer[(contentColorVolumeOffset + 8)..]);
                        int bluePrimaryY = BinaryPrimitives.ReadInt32BigEndian(boxBuffer[(contentColorVolumeOffset + 12)..]);
                        int redPrimaryX = BinaryPrimitives.ReadInt32BigEndian(boxBuffer[(contentColorVolumeOffset + 16)..]);
                        int redPrimaryY = BinaryPrimitives.ReadInt32BigEndian(boxBuffer[(contentColorVolumeOffset + 20)..]);

                        const int maximumContentChromaticityValue = 5_000_000;
                        if (greenPrimaryX < -maximumContentChromaticityValue
                            || greenPrimaryX > maximumContentChromaticityValue
                            || greenPrimaryY < -maximumContentChromaticityValue
                            || greenPrimaryY > maximumContentChromaticityValue
                            || bluePrimaryX < -maximumContentChromaticityValue
                            || bluePrimaryX > maximumContentChromaticityValue
                            || bluePrimaryY < -maximumContentChromaticityValue
                            || bluePrimaryY > maximumContentChromaticityValue
                            || redPrimaryX < -maximumContentChromaticityValue
                            || redPrimaryX > maximumContentChromaticityValue
                            || redPrimaryY < -maximumContentChromaticityValue
                            || redPrimaryY > maximumContentChromaticityValue)
                        {
                            throw new InvalidImageContentException("The content color-volume property has an out-of-range primary coordinate.");
                        }

                        const float contentChromaticityScale = 1F / 50000F;

                        // Content-color-volume syntax stores signed coordinates in G, B, R order. Reorder the
                        // optional primaries into ImageSharp's existing RGB coordinate representation.
                        contentPrimaries = new RgbPrimariesChromaticityCoordinates(
                            new CieXyChromaticityCoordinates(
                                redPrimaryX * contentChromaticityScale,
                                redPrimaryY * contentChromaticityScale),
                            new CieXyChromaticityCoordinates(
                                greenPrimaryX * contentChromaticityScale,
                                greenPrimaryY * contentChromaticityScale),
                            new CieXyChromaticityCoordinates(
                                bluePrimaryX * contentChromaticityScale,
                                bluePrimaryY * contentChromaticityScale));

                        contentColorVolumeOffset += 24;
                    }

                    uint? minimumLuminanceValue = null;
                    if (minimumLuminancePresent)
                    {
                        minimumLuminanceValue = BinaryPrimitives.ReadUInt32BigEndian(boxBuffer[contentColorVolumeOffset..]);
                        contentColorVolumeOffset += 4;
                    }

                    uint? maximumLuminanceValue = null;
                    if (maximumLuminancePresent)
                    {
                        maximumLuminanceValue = BinaryPrimitives.ReadUInt32BigEndian(boxBuffer[contentColorVolumeOffset..]);
                        contentColorVolumeOffset += 4;
                    }

                    uint? averageLuminanceValue = null;
                    if (averageLuminancePresent)
                    {
                        averageLuminanceValue = BinaryPrimitives.ReadUInt32BigEndian(boxBuffer[contentColorVolumeOffset..]);
                    }

                    if (minimumLuminanceValue is not null
                        && averageLuminanceValue is not null
                        && minimumLuminanceValue.Value > averageLuminanceValue.Value)
                    {
                        throw new InvalidImageContentException("The content color-volume minimum luminance exceeds its average luminance.");
                    }

                    if (averageLuminanceValue is not null
                        && maximumLuminanceValue is not null
                        && averageLuminanceValue.Value > maximumLuminanceValue.Value)
                    {
                        throw new InvalidImageContentException("The content color-volume average luminance exceeds its maximum luminance.");
                    }

                    if (minimumLuminanceValue is not null
                        && maximumLuminanceValue is not null
                        && minimumLuminanceValue.Value > maximumLuminanceValue.Value)
                    {
                        throw new InvalidImageContentException("The content color-volume minimum luminance exceeds its maximum luminance.");
                    }

                    const double contentLuminanceScale = 1D / 10000000D;

                    // These values are normalized according to the signaled transfer characteristics. Preserve
                    // that unitless meaning instead of presenting them as physical display luminance.
                    double? minimumContentLuminance = minimumLuminanceValue is not null
                        ? minimumLuminanceValue.Value * contentLuminanceScale
                        : null;

                    double? maximumContentLuminance = maximumLuminanceValue is not null
                        ? maximumLuminanceValue.Value * contentLuminanceScale
                        : null;

                    double? averageContentLuminance = averageLuminanceValue is not null
                        ? averageLuminanceValue.Value * contentLuminanceScale
                        : null;

                    properties.Add(
                        new KeyValuePair<Heif4CharCode, object>(
                            Heif4CharCode.Cclv,
                            new HeifContentColorVolume(
                                contentPrimaries,
                                minimumContentLuminance,
                                maximumContentLuminance,
                                averageContentLuminance)));

                    break;
                case Heif4CharCode.Amve:
                    EnsureBufferRemaining(boxBuffer, 0, 8, "ambient viewing environment");
                    if (boxBuffer.Length != 8)
                    {
                        throw new InvalidImageContentException("The ambient viewing-environment property has an invalid length.");
                    }

                    uint ambientIlluminanceValue = BinaryPrimitives.ReadUInt32BigEndian(boxBuffer);
                    ushort ambientLightX = BinaryPrimitives.ReadUInt16BigEndian(boxBuffer[4..]);
                    ushort ambientLightY = BinaryPrimitives.ReadUInt16BigEndian(boxBuffer[6..]);
                    if (ambientIlluminanceValue == 0)
                    {
                        throw new InvalidImageContentException("The ambient viewing-environment property has zero illuminance.");
                    }

                    if (ambientLightX > 50000 || ambientLightY > 50000)
                    {
                        throw new InvalidImageContentException("The ambient viewing-environment property has an out-of-range chromaticity coordinate.");
                    }

                    const double ambientIlluminanceScale = 1D / 10000D;
                    const float ambientChromaticityScale = 1F / 50000F;

                    // The item property inherits H.274's fixed-point units: 0.0001 lux for illuminance and
                    // 0.00002 for each normalized CIE chromaticity coordinate.
                    properties.Add(
                        new KeyValuePair<Heif4CharCode, object>(
                            Heif4CharCode.Amve,
                            new HeifAmbientViewingEnvironment(
                                ambientIlluminanceValue * ambientIlluminanceScale,
                                new CieXyChromaticityCoordinates(
                                    ambientLightX * ambientChromaticityScale,
                                    ambientLightY * ambientChromaticityScale))));

                    break;
                case Heif4CharCode.Reve:
                    EnsureBufferRemaining(boxBuffer, 0, 20, "reference viewing environment");
                    if (boxBuffer.Length != 20)
                    {
                        throw new InvalidImageContentException("The reference viewing-environment property has an invalid length.");
                    }

                    if (BinaryPrimitives.ReadUInt32BigEndian(boxBuffer) != 0)
                    {
                        throw new InvalidImageContentException("The reference viewing-environment property has an unsupported version or flags.");
                    }

                    ushort surroundLightX = BinaryPrimitives.ReadUInt16BigEndian(boxBuffer[8..]);
                    ushort surroundLightY = BinaryPrimitives.ReadUInt16BigEndian(boxBuffer[10..]);
                    ushort peripheryLightX = BinaryPrimitives.ReadUInt16BigEndian(boxBuffer[16..]);
                    ushort peripheryLightY = BinaryPrimitives.ReadUInt16BigEndian(boxBuffer[18..]);
                    if (surroundLightX > 10000
                        || surroundLightY > 10000
                        || peripheryLightX > 10000
                        || peripheryLightY > 10000)
                    {
                        throw new InvalidImageContentException("The reference viewing-environment property has an out-of-range chromaticity coordinate.");
                    }

                    const double viewingEnvironmentLuminanceScale = 1D / 10000D;
                    const float referenceChromaticityScale = 1F / 10000F;

                    // The full-box header is followed by the display surround and then the wider periphery.
                    // Both field groups use 0.0001 increments, but luminance is physical cd/m2 while the CIE
                    // coordinates are normalized. Keep the regions distinct because they affect different areas.
                    properties.Add(
                        new KeyValuePair<Heif4CharCode, object>(
                            Heif4CharCode.Reve,
                            new HeifReferenceViewingEnvironment(
                                BinaryPrimitives.ReadUInt32BigEndian(boxBuffer[4..]) * viewingEnvironmentLuminanceScale,
                                new CieXyChromaticityCoordinates(
                                    surroundLightX * referenceChromaticityScale,
                                    surroundLightY * referenceChromaticityScale),
                                BinaryPrimitives.ReadUInt32BigEndian(boxBuffer[12..]) * viewingEnvironmentLuminanceScale,
                                new CieXyChromaticityCoordinates(
                                    peripheryLightX * referenceChromaticityScale,
                                    peripheryLightY * referenceChromaticityScale))));

                    break;
                case Heif4CharCode.Ndwt:
                    EnsureBufferRemaining(boxBuffer, 0, 8, "nominal diffuse white");
                    if (boxBuffer.Length != 8)
                    {
                        throw new InvalidImageContentException("The nominal diffuse-white property has an invalid length.");
                    }

                    if (BinaryPrimitives.ReadUInt32BigEndian(boxBuffer) != 0)
                    {
                        throw new InvalidImageContentException("The nominal diffuse-white property has an unsupported version or flags.");
                    }

                    uint diffuseWhiteLuminanceValue = BinaryPrimitives.ReadUInt32BigEndian(boxBuffer[4..]);
                    const double diffuseWhiteLuminanceScale = 1D / 10000D;

                    // A zero coded value requests the standard-defined default; it does not describe a black
                    // diffuse white. Preserve that distinction separately from an absent item property.
                    double? diffuseWhiteLuminance = diffuseWhiteLuminanceValue == 0
                        ? null
                        : diffuseWhiteLuminanceValue * diffuseWhiteLuminanceScale;

                    properties.Add(
                        new KeyValuePair<Heif4CharCode, object>(
                            Heif4CharCode.Ndwt,
                            new HeifNominalDiffuseWhite(diffuseWhiteLuminance)));

                    break;
                case Heif4CharCode.Av1C:
                    EnsureBufferRemaining(boxBuffer, 0, 4, "AV1 codec configuration");
                    properties.Add(
                        new KeyValuePair<Heif4CharCode, object>(
                            Heif4CharCode.Av1C,
                            new Av1CodecConfiguration(boxBuffer)));

                    break;
                case Heif4CharCode.Clap:
                    EnsureBufferRemaining(boxBuffer, 0, 32, "clean aperture");
                    HeifCleanAperture cleanAperture = new(
                        unchecked((int)BinaryPrimitives.ReadUInt32BigEndian(boxBuffer)),
                        unchecked((int)BinaryPrimitives.ReadUInt32BigEndian(boxBuffer[4..])),
                        unchecked((int)BinaryPrimitives.ReadUInt32BigEndian(boxBuffer[8..])),
                        unchecked((int)BinaryPrimitives.ReadUInt32BigEndian(boxBuffer[12..])),
                        unchecked((int)BinaryPrimitives.ReadUInt32BigEndian(boxBuffer[16..])),
                        unchecked((int)BinaryPrimitives.ReadUInt32BigEndian(boxBuffer[20..])),
                        unchecked((int)BinaryPrimitives.ReadUInt32BigEndian(boxBuffer[24..])),
                        unchecked((int)BinaryPrimitives.ReadUInt32BigEndian(boxBuffer[28..])));

                    properties.Add(new KeyValuePair<Heif4CharCode, object>(Heif4CharCode.Clap, cleanAperture));
                    break;
                case Heif4CharCode.Irot:
                    EnsureBufferRemaining(boxBuffer, 0, 1, "image rotation");
                    if ((boxBuffer[0] & 0xFC) != 0)
                    {
                        throw new InvalidImageContentException("The image rotation property has nonzero reserved bits.");
                    }

                    properties.Add(new KeyValuePair<Heif4CharCode, object>(Heif4CharCode.Irot, (byte)(boxBuffer[0] & 3)));
                    break;
                case Heif4CharCode.Imir:
                    EnsureBufferRemaining(boxBuffer, 0, 1, "image mirror");
                    if ((boxBuffer[0] & 0xFE) != 0)
                    {
                        throw new InvalidImageContentException("The image mirror property has nonzero reserved bits.");
                    }

                    properties.Add(new KeyValuePair<Heif4CharCode, object>(Heif4CharCode.Imir, (byte)(boxBuffer[0] & 1)));
                    break;
                case Heif4CharCode.Altt:
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

                if (!essential && prop.Key is Heif4CharCode.Clap or Heif4CharCode.Irot or Heif4CharCode.Imir)
                {
                    throw new InvalidImageContentException($"Item {itemId} associates nonessential transformative property '{PrettyPrint(prop.Key)}'.");
                }

                switch (prop.Key)
                {
                    case Heif4CharCode.Ispe:
                        item.SetExtent((Size)prop.Value);
                        break;
                    case Heif4CharCode.Pasp:
                        if (item.PixelAspectRatio is not null)
                        {
                            throw new InvalidImageContentException($"Item {itemId} associates more than one pixel aspect ratio property.");
                        }

                        item.PixelAspectRatio = (HeifPixelAspectRatio)prop.Value;
                        break;
                    case Heif4CharCode.Pixi:
                        if (item.ChannelBitDepths is not null)
                        {
                            throw new InvalidImageContentException($"Item {itemId} associates more than one pixel information property.");
                        }

                        byte[] channelBitDepths = (byte[])prop.Value;
                        int bitsPerPixel = 0;
                        for (int channel = 0; channel < channelBitDepths.Length; channel++)
                        {
                            bitsPerPixel += channelBitDepths[channel];
                        }

                        item.ChannelCount = channelBitDepths.Length;
                        item.ChannelBitDepths = channelBitDepths;
                        item.BitsPerPixel = bitsPerPixel;
                        break;
                    case Heif4CharCode.Av1C:
                        if (item.Type != Heif4CharCode.Av01)
                        {
                            throw new InvalidImageContentException($"Item {itemId} associates an AV1 codec configuration with non-AV1 item type '{PrettyPrint(item.Type)}'.");
                        }

                        if (item.Av1CodecConfiguration is not null)
                        {
                            throw new InvalidImageContentException($"Item {itemId} associates more than one AV1 codec configuration property.");
                        }

                        item.Av1CodecConfiguration = (Av1CodecConfiguration)prop.Value;
                        break;
                    case Heif4CharCode.AuxC:
                        if (item.AuxiliaryType is not null)
                        {
                            throw new InvalidImageContentException($"Item {itemId} associates more than one auxiliary type property.");
                        }

                        item.AuxiliaryType = (string)prop.Value;
                        break;
                    case Heif4CharCode.Colr:
                        if (prop.Value is IccProfile iccProfile)
                        {
                            if (item.IccProfile is not null)
                            {
                                throw new InvalidImageContentException($"Item {itemId} associates more than one ICC color property.");
                            }

                            item.IccProfile = iccProfile;
                        }
                        else if (prop.Value is CicpProfile cicpProfile)
                        {
                            if (item.CicpProfile is not null)
                            {
                                throw new InvalidImageContentException($"Item {itemId} associates more than one CICP color property.");
                            }

                            item.CicpProfile = cicpProfile;
                        }

                        break;
                    case Heif4CharCode.Clli:
                        if (item.ContentLightLevel is not null)
                        {
                            throw new InvalidImageContentException($"Item {itemId} associates more than one content light level property.");
                        }

                        item.ContentLightLevel = (HeifContentLightLevel)prop.Value;
                        break;
                    case Heif4CharCode.Mdcv:
                        if (item.MasteringDisplayColorVolume is not null)
                        {
                            throw new InvalidImageContentException($"Item {itemId} associates more than one mastering display color-volume property.");
                        }

                        item.MasteringDisplayColorVolume = (HeifMasteringDisplayColorVolume)prop.Value;
                        break;
                    case Heif4CharCode.Cclv:
                        if (item.ContentColorVolume is not null)
                        {
                            throw new InvalidImageContentException($"Item {itemId} associates more than one content color-volume property.");
                        }

                        item.ContentColorVolume = (HeifContentColorVolume)prop.Value;
                        break;
                    case Heif4CharCode.Amve:
                        if (item.AmbientViewingEnvironment is not null)
                        {
                            throw new InvalidImageContentException($"Item {itemId} associates more than one ambient viewing-environment property.");
                        }

                        item.AmbientViewingEnvironment = (HeifAmbientViewingEnvironment)prop.Value;
                        break;
                    case Heif4CharCode.Reve:
                        if (item.ReferenceViewingEnvironment is not null)
                        {
                            throw new InvalidImageContentException($"Item {itemId} associates more than one reference viewing-environment property.");
                        }

                        item.ReferenceViewingEnvironment = (HeifReferenceViewingEnvironment)prop.Value;
                        break;
                    case Heif4CharCode.Ndwt:
                        if (item.NominalDiffuseWhite is not null)
                        {
                            throw new InvalidImageContentException($"Item {itemId} associates more than one nominal diffuse-white property.");
                        }

                        item.NominalDiffuseWhite = (HeifNominalDiffuseWhite)prop.Value;
                        break;
                    case Heif4CharCode.Clap:
                        if (item.CleanAperture is not null)
                        {
                            throw new InvalidImageContentException($"Item {itemId} associates more than one clean aperture property.");
                        }

                        item.CleanAperture = (HeifCleanAperture)prop.Value;
                        break;
                    case Heif4CharCode.Irot:
                        if (item.RotationAngle is not null)
                        {
                            throw new InvalidImageContentException($"Item {itemId} associates more than one image rotation property.");
                        }

                        item.RotationAngle = (byte)prop.Value;
                        break;
                    case Heif4CharCode.Imir:
                        if (item.MirrorAxis is not null)
                        {
                            throw new InvalidImageContentException($"Item {itemId} associates more than one image mirror property.");
                        }

                        item.MirrorAxis = (byte)prop.Value;
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

        IHeifItemDecoder<TPixel>? itemDecoder = this.GetItemDecoder<TPixel>(rootItem, buffers);

        HeifItem itemToDecode = rootItem;
        if (itemDecoder is null)
        {
            // Unable to decode the primary image, decode the thumbnail instead.
            HeifItem? thumbnailItem = this.FindDecodableThumbnail<TPixel>(rootItem);
            if (thumbnailItem is not null)
            {
                itemDecoder = HeifCompressionFactory.GetDecoder<TPixel>(thumbnailItem.Type);
                itemToDecode = thumbnailItem;
            }
        }

        if (itemDecoder is null)
        {
            throw new ImageFormatException("No decodable item found inside this HEIF container.");
        }

        Image<TPixel> image = this.DecodeImageItem(itemToDecode, itemDecoder, buffers);
        try
        {
            using Image<L16>? alphaImage = this.DecodeAlphaPlane(itemToDecode, buffers, out bool alphaPremultiplied);
            if (alphaImage is not null)
            {
                this.ApplyAlpha(image, alphaImage, alphaPremultiplied);
            }

            if (!this.Options.SkipMetadata)
            {
                this.ApplyItemColorMetadata(image.Metadata, itemToDecode);
                this.ApplyItemHdrMetadata(image.Metadata, itemToDecode);
                this.ApplyAssociatedMetadata(image.Metadata, rootItem, buffers);
                _ = this.TryConvertIccProfile(image);
            }

            // MIAF defines crop, rotation, and mirror as presentation operations in that order. Applying the
            // implemented transforms after alpha composition keeps the auxiliary plane in the same coordinate space.
            ApplyPresentationTransforms(image, itemToDecode);

            if (!this.Options.SkipMetadata)
            {
                this.ApplyItemPixelAspectRatioMetadata(image.Metadata, itemToDecode);
            }

            // The decoder determines the compression of the pixels that were actually returned, including grid tiles
            // and a thumbnail fallback when the primary image compression is not available.
            HeifMetadata meta = image.Metadata.GetHeifMetadata();
            meta.CompressionMethod = itemDecoder.CompressionMethod;
            meta.HasAlpha = alphaImage is not null;
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

        // ImageMetadata expresses pixel width:height as vertical-density:horizontal-density. A quarter-turn exchanges
        // the displayed pixel axes, so it also exchanges which spacing value supplies each density.
        bool swapsAxes = imageItem.RotationAngle is 1 or 3;
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

            byte[] itemData = itemMemory.GetSpan().ToArray();
            if (metadataItem.Type == Heif4CharCode.Exif)
            {
                this.ExecuteAncillarySegmentAction(() =>
                {
                    if (itemData.Length < 8)
                    {
                        throw new InvalidImageContentException("The HEIF Exif item is truncated.");
                    }

                    uint declaredTiffHeaderOffset = BinaryPrimitives.ReadUInt32BigEndian(itemData);
                    Span<byte> exifData = itemData.AsSpan(4);
                    int actualTiffHeaderOffset = -1;

                    // Annex A stores the offset to the first TIFF byte-order marker. Match libavif by finding the
                    // first valid TIFF signature and requiring the declared offset to identify that same header.
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

                    metadata.ExifProfile = new ExifProfile(exifData[actualTiffHeaderOffset..].ToArray());
                });
            }
            else if (metadataItem.Type == Heif4CharCode.Mime &&
                string.Equals(metadataItem.ContentType, "application/rdf+xml", StringComparison.Ordinal))
            {
                this.ExecuteAncillarySegmentAction(() => metadata.XmpProfile = new XmpProfile(itemData));
            }
        }
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
            ? new GridHeifItemDecoder<TPixel>(this.configuration, this.items, this.itemLinks, buffers)
            : HeifCompressionFactory.GetDecoder<TPixel>(item.Type);

    /// <summary>
    /// Decodes one image item from its assembled payload.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel format.</typeparam>
    /// <param name="item">The image item to decode.</param>
    /// <param name="decoder">The decoder selected for the item.</param>
    /// <param name="buffers">The assembled item payloads.</param>
    /// <returns>The decoded image.</returns>
    private Image<TPixel> DecodeImageItem<TPixel>(
        HeifItem item,
        IHeifItemDecoder<TPixel> decoder,
        DisposableDictionary<uint, IMemoryOwner<byte>> buffers)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (!buffers.TryGetValue(item.Id, out IMemoryOwner<byte>? itemMemory))
        {
            throw new InvalidImageContentException($"Item {item.Id} has no data extents.");
        }

        Image<TPixel> image = decoder.DecodeItemData(
            this.configuration,
            item,
            itemMemory.GetSpan(),
            item.CicpProfile);

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
    /// Applies the clean-aperture, rotation, and mirror properties associated with an image item.
    /// </summary>
    /// <typeparam name="TPixel">The image pixel format.</typeparam>
    /// <param name="image">The decoded image item.</param>
    /// <param name="item">The item carrying the presentation properties.</param>
    private static void ApplyPresentationTransforms<TPixel>(Image<TPixel> image, HeifItem item)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (item.CleanAperture is not null)
        {
            Rectangle cropRectangle = item.CleanAperture.Value.ToRectangle(image.Size);
            if (cropRectangle != image.Bounds)
            {
                image.Mutate(context => context.Crop(cropRectangle));
            }
        }

        if (item.RotationAngle is not null)
        {
            // HEIF angles count quarter turns counter-clockwise, while ImageSharp's optimized rotate modes are clockwise.
            RotateMode rotation = item.RotationAngle.Value switch
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

        if (item.MirrorAxis is not null)
        {
            // Axis zero reflects top-to-bottom around the horizontal axis; axis one reflects left-to-right.
            FlipMode flip = item.MirrorAxis.Value == 0 ? FlipMode.Vertical : FlipMode.Horizontal;
            image.Mutate(context => context.Flip(flip));
        }
    }

    /// <summary>
    /// Decodes the direct or per-grid-tile alpha auxiliary plane associated with a color image item.
    /// </summary>
    /// <param name="colorItem">The color image item whose alpha plane is requested.</param>
    /// <param name="buffers">The assembled item payloads.</param>
    /// <param name="premultiplied">Indicates whether the color samples are premultiplied by the decoded alpha.</param>
    /// <returns>The normalized 16-bit alpha plane, or <see langword="null"/> when the item has no alpha auxiliary.</returns>
    private Image<L16>? DecodeAlphaPlane(
        HeifItem colorItem,
        DisposableDictionary<uint, IMemoryOwner<byte>> buffers,
        out bool premultiplied)
    {
        premultiplied = false;
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

            IHeifItemDecoder<L16>? decoder = this.GetItemDecoder<L16>(alphaItem, buffers);
            if (decoder is null)
            {
                throw new ImageFormatException($"The alpha auxiliary item uses unsupported item type '{alphaItem.Type}'.");
            }

            premultiplied = this.itemLinks.Any(
                link => link.Type == Heif4CharCode.Prem
                    && link.SourceId == colorItem.Id
                    && link.DestinationIds.Contains(alphaItem.Id));

            return this.DecodeImageItem(alphaItem, decoder, buffers);
        }

        if (colorItem.Type != Heif4CharCode.Grid)
        {
            return null;
        }

        List<uint>? alphaTileIds = this.FindGridAlphaTiles(colorItem);
        if (alphaTileIds is null)
        {
            return null;
        }

        if (!buffers.TryGetValue(colorItem.Id, out IMemoryOwner<byte>? gridMemory))
        {
            throw new InvalidImageContentException($"Item {colorItem.Id} has no data extents.");
        }

        // The color grid descriptor defines the same row/column layout and output canvas for per-tile alpha
        // auxiliaries. Supplying their IDs lets the existing grid compositor preserve that normative ordering.
        GridHeifItemDecoder<L16> gridDecoder = new(
            this.configuration,
            this.items,
            this.itemLinks,
            buffers,
            alphaTileIds);

        return gridDecoder.DecodeItemData(this.configuration, colorItem, gridMemory.GetSpan(), null);
    }

    /// <summary>
    /// Composes a normalized alpha plane into a decoded color image.
    /// </summary>
    /// <typeparam name="TPixel">The decoded color pixel format.</typeparam>
    /// <param name="image">The decoded color image.</param>
    /// <param name="alphaImage">The normalized 16-bit alpha plane.</param>
    /// <param name="premultiplied">Whether the stored color values must be converted to unassociated alpha.</param>
    private void ApplyAlpha<TPixel>(Image<TPixel> image, Image<L16> alphaImage, bool premultiplied)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (alphaImage.Width != image.Width || alphaImage.Height != image.Height)
        {
            // HEIF permits auxiliary alpha dimensions to differ from the master image. libavif uses a box filter
            // for this plane scaling, which maps directly to ImageSharp's existing resampler.
            alphaImage.Mutate(context => context.Resize(image.Width, image.Height, KnownResamplers.Box));
        }

        using IMemoryOwner<Rgba64> rowOwner = this.configuration.MemoryAllocator.Allocate<Rgba64>(image.Width);
        Span<Rgba64> rgbaRow = rowOwner.GetSpan()[..image.Width];
        PixelOperations<TPixel> pixelOperations = PixelOperations<TPixel>.Instance;
        ImageFrame<TPixel> colorFrame = image.Frames.RootFrame;
        ImageFrame<L16> alphaFrame = alphaImage.Frames.RootFrame;
        for (int y = 0; y < image.Height; y++)
        {
            Span<TPixel> colorRow = colorFrame.PixelBuffer.DangerousGetRowSpan(y);
            Span<L16> alphaRow = alphaFrame.PixelBuffer.DangerousGetRowSpan(y);
            pixelOperations.ToRgba64(this.configuration, colorRow, rgbaRow);
            for (int x = 0; x < image.Width; x++)
            {
                Rgba64 pixel = rgbaRow[x];
                pixel.A = alphaRow[x].PackedValue;
                if (premultiplied)
                {
                    // libavif defines transparent premultiplied samples as transparent black. For nonzero alpha,
                    // reuse the packed pixel's associated-input conversion so clamping and rounding follow ImageSharp.
                    pixel = pixel.A == 0
                        ? new Rgba64(0, 0, 0, 0)
                        : Rgba64.FromAssociatedScaledVector4(pixel.ToScaledVector4());
                }

                rgbaRow[x] = pixel;
            }

            pixelOperations.FromRgba64(this.configuration, rgbaRow, colorRow);
        }
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
