// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Parses the boxes that declare and locate HEIF items: the handler, item-information and item-location boxes.
/// </summary>
/// <remarks>
/// The still-image decoder, the image-sequence parser and the track metadata parser use these parsers. Thus each box has one set of rules.
/// </remarks>
internal static class HeifItemBoxParser
{
    /// <summary>
    /// Reads and validates the full-box header of a metadata box.
    /// </summary>
    /// <param name="stream">The stream positioned at the metadata full-box header.</param>
    /// <param name="boxLength">The metadata payload length.</param>
    /// <param name="headerBuffer">A caller-owned buffer of at least four bytes.</param>
    /// <param name="allowedFlags">The flag bits that the caller accepts.</param>
    /// <param name="name">The metadata box name that the error messages use.</param>
    /// <exception cref="InvalidImageContentException">The header is truncated, or it uses an unsupported version or flag.</exception>
    public static void ReadMetadataHeader(Stream stream, long boxLength, Span<byte> headerBuffer, uint allowedFlags, string name)
    {
        // ISO/IEC 14496-12 defines only version 0 of the metadata box. A payload shorter than the full-box header, or a stream that ends inside it, gives a
        // short span. The span reader then reports the truncation with the box name. Thus the caller supplies no separate message.
        Span<byte> fullBoxHeader = headerBuffer[..(int)Math.Min(boxLength, 4)];
        int read = stream.ReadAtLeast(fullBoxHeader, fullBoxHeader.Length, false);
        HeifSpanReader reader = new(fullBoxHeader[..read], name);
        reader.ReadFullBoxHeader(0, allowedFlags, out _);
        reader.EnsureEnd();
    }

    /// <summary>
    /// Parses a handler box and returns its four-character handler type.
    /// </summary>
    /// <param name="payload">The handler full-box payload.</param>
    /// <returns>The declared handler type.</returns>
    /// <exception cref="InvalidImageContentException">The handler box is not valid.</exception>
    public static Heif4CharCode ParseHandler(ReadOnlySpan<byte> payload)
    {
        // ISO/IEC 14496-12, section 8.4.3 defines only version 0 of the handler box. The pre_defined field, the handler type and three reserved 32-bit fields
        // come before the name. The parser does not use the name.
        HeifSpanReader reader = new(payload, "handler");
        reader.ReadFullBoxHeader(0, HeifSpanReader.AnyFlags, out _);
        reader.Skip(4);
        Heif4CharCode handlerType = (Heif4CharCode)reader.ReadUInt32();
        reader.Skip(12);
        reader.EnsureNotTruncated();
        return handlerType;
    }

    /// <summary>
    /// Parses the item-information box and its item-information entries.
    /// </summary>
    /// <param name="payload">The item-information full-box payload.</param>
    /// <param name="items">The declared items, indexed by item identifier. The method adds each declared item.</param>
    /// <exception cref="InvalidImageContentException">The box is not valid.</exception>
    public static void ParseItemInfo(ReadOnlySpan<byte> payload, Dictionary<uint, HeifItem> items)
    {
        // ISO/IEC 14496-12, section 8.11.6: version 0 has a 16-bit entry count. Version 1 has a 32-bit entry count. Each entry is an item-information-entry
        // box. This box contains at least a box header and a full-box header.
        const int minimumEntryLength = 12;

        HeifSpanReader reader = new(payload, "item info");
        byte version = reader.ReadFullBoxHeader(1, HeifSpanReader.AnyFlags, out _);
        uint entryCount = reader.ReadEntryCount(version == 0 ? 2 : 4, minimumEntryLength);
        for (uint i = 0; i < entryCount; i++)
        {
            ReadOnlySpan<byte> remaining = reader.RemainingData;
            int headerLength = HeifBoxReader.ParseHeader(remaining, out long entryLength, out Heif4CharCode entryType);
            if (entryType != Heif4CharCode.Infe)
            {
                throw new InvalidImageContentException($"The item info box contains unexpected child '{entryType}'.");
            }

            ParseItemInfoEntry(remaining.Slice(headerLength, (int)entryLength), items);
            reader.Skip(headerLength + (int)entryLength);
        }

        reader.EnsureEnd();
    }

    /// <summary>
    /// Parses the construction method, base offset, and ordered extents for every declared item.
    /// </summary>
    /// <param name="payload">The item-location full-box payload.</param>
    /// <param name="items">The declared items, indexed by item identifier. The method adds the extents of each item.</param>
    /// <exception cref="InvalidImageContentException">The box is not valid.</exception>
    public static void ParseItemLocation(ReadOnlySpan<byte> payload, Dictionary<uint, HeifItem> items)
    {
        HeifSpanReader reader = new(payload, "item location");
        byte version = reader.ReadFullBoxHeader(2, HeifSpanReader.AnyFlags, out _);

        // ISO/IEC 14496-12, section 8.11.3: two bytes contain the four-bit integer widths of the extent offset, the extent length and the base offset. In
        // versions 1 and 2, these bytes also contain the width of the extent index. A zero width gives an implicit zero value.
        byte offsetAndLengthSizes = reader.ReadUInt8();
        byte baseOffsetAndIndexSizes = reader.ReadUInt8();
        int offsetSize = offsetAndLengthSizes >> 4;
        int lengthSize = offsetAndLengthSizes & 0x0F;
        int baseOffsetSize = baseOffsetAndIndexSizes >> 4;
        int indexSize = version is 1 or 2 ? baseOffsetAndIndexSizes & 0x0F : 0;
        if (!IsSupportedFieldSize(offsetSize) || !IsSupportedFieldSize(lengthSize) || !IsSupportedFieldSize(baseOffsetSize) || !IsSupportedFieldSize(indexSize))
        {
            throw new InvalidImageContentException("The item location box uses an invalid integer field size.");
        }

        // Each extent contains the optional index field, the offset field and the length field. A zero total size is legal, because a zero width gives an
        // implicit zero value. Such an extent uses no bytes in the box. Each item record contains the identifier, the construction method from version 1, the
        // data reference index, the base offset and the extent count.
        int extentSize = indexSize + offsetSize + lengthSize;
        bool largeItemIds = version == 2;
        int itemRecordSize = (largeItemIds ? 4 : 2) + (version is 1 or 2 ? 2 : 0) + 2 + baseOffsetSize + 2;
        uint itemCount = reader.ReadEntryCount(largeItemIds ? 4 : 2, itemRecordSize);
        HashSet<uint> locatedItemIds = [];
        for (uint i = 0; i < itemCount; i++)
        {
            // The extents of earlier items can use the bytes that the count validation reserved for this record. A truncated read gives identifier zero. Thus
            // EnsureNotTruncated reports the truncation before the item lookup.
            uint itemId = reader.ReadU16Or32(largeItemIds);
            reader.EnsureNotTruncated();
            HeifItem item = items.GetValueOrDefault(itemId)
                ?? throw new InvalidImageContentException($"The item location box references unknown item ID {itemId}.");
            if (!locatedItemIds.Add(itemId))
            {
                throw new InvalidImageContentException($"The item location box contains duplicate locations for item ID {itemId}.");
            }

            HeifLocationOffsetOrigin constructionMethod = HeifLocationOffsetOrigin.FileOffset;
            if (version is 1 or 2)
            {
                ushort constructionField = reader.ReadUInt16();
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

            if (reader.ReadUInt16() != 0)
            {
                throw new InvalidImageContentException("External item data references are not supported.");
            }

            ulong baseOffset = reader.ReadUInt(baseOffsetSize);
            uint extentCount = reader.ReadUInt16();

            // The parser validates the declared extent count before it creates a location. Each extent must fit in the bytes that remain in the box. All
            // zero-width extents give the same implicit zero offset and length. Thus more than one zero-width extent is malformed. Without this validation, a
            // few bytes can create 65,535 locations for each item.
            if (extentSize == 0 ? extentCount > 1 : (ulong)extentCount * (uint)extentSize > (ulong)reader.Remaining)
            {
                throw new InvalidImageContentException($"The item location box declares more extents for item ID {itemId} than it contains.");
            }

            for (uint j = 0; j < extentCount; j++)
            {
                // Extent indices select the extents of a referenced item only for construction method 2. Methods 0 and 1 also contain the field when its width
                // is not zero. The parser skips the field so that it reads the next fields at the correct position.
                reader.Skip(indexSize);
                ulong extentOffset = reader.ReadUInt(offsetSize);
                ulong extentLength = reader.ReadUInt(lengthSize);

                // Stream offsets and stream lengths are signed 64-bit values. Thus a larger field value gives no valid stream range.
                if (baseOffset > long.MaxValue || extentOffset > long.MaxValue || extentLength > long.MaxValue)
                {
                    throw new InvalidImageContentException("An item location offset exceeds the supported stream range.");
                }

                item.DataLocations.Add(new HeifLocation(constructionMethod, (long)baseOffset, (long)extentOffset, (long)extentLength));
            }
        }

        reader.EnsureEnd();
    }

    /// <summary>
    /// Parses one versioned item-information entry.
    /// </summary>
    /// <param name="payload">The item-information-entry full-box payload.</param>
    /// <param name="items">The declared items, indexed by item identifier. The method adds the item.</param>
    private static void ParseItemInfoEntry(ReadOnlySpan<byte> payload, Dictionary<uint, HeifItem> items)
    {
        // ISO/IEC 14496-12, section 8.11.6: versions 0 and 1 have a 16-bit identifier and no item type. Version 2 adds the item type. Version 3 increases the
        // identifier to 32 bits.
        HeifSpanReader reader = new(payload, "item info entry");
        byte version = reader.ReadFullBoxHeader(3, HeifSpanReader.AnyFlags, out _);
        HeifItem item;
        ushort protectionIndex;
        if (version < 2)
        {
            uint itemId = reader.ReadUInt16();
            protectionIndex = reader.ReadUInt16();
            item = new HeifItem(Heif4CharCode.Infe, itemId)
            {
                Name = reader.ReadCString(),
                ContentType = reader.ReadCString()
            };

            if (reader.Remaining > 0)
            {
                item.ContentEncoding = reader.ReadCString();
            }

            if (version == 1 && reader.Remaining > 0)
            {
                // The decoder does not use version 1 extension payloads. The parser skips the 32-bit extension type and the extension, and reads no byte
                // outside this entry.
                reader.Skip(4);
                reader.Skip(reader.Remaining);
            }
        }
        else
        {
            uint itemId = reader.ReadU16Or32(version == 3);
            protectionIndex = reader.ReadUInt16();
            Heif4CharCode itemType = (Heif4CharCode)reader.ReadUInt32();
            item = new HeifItem(itemType, itemId)
            {
                Name = reader.ReadCString()
            };

            if (itemType == Heif4CharCode.Mime)
            {
                item.ContentType = reader.ReadCString();
                if (reader.Remaining > 0)
                {
                    item.ContentEncoding = reader.ReadCString();
                }
            }
            else if (itemType == Heif4CharCode.Uri)
            {
                // The decoder does not use the URI type. The parser reads it to validate its terminator.
                reader.ReadCString();
            }
        }

        reader.EnsureEnd();

        // The decoders use item identifier zero for an absent item, for example in a file without a primary-item box. A declared item with identifier zero then
        // has the same identifier as an absent item.
        if (item.Id == 0)
        {
            throw new InvalidImageContentException("The item info box declares item identifier zero.");
        }

        if (protectionIndex != 0)
        {
            throw new InvalidImageContentException($"Item {item.Id} uses unsupported item protection.");
        }

        if (!items.TryAdd(item.Id, item))
        {
            throw new InvalidImageContentException($"The item info box contains duplicate item ID {item.Id}.");
        }
    }

    /// <summary>
    /// Determines whether the reader primitives can read an item-location integer of this width.
    /// </summary>
    /// <param name="size">The width in bytes from an item-location size nibble.</param>
    /// <returns><see langword="true"/> for the registered zero, 32-bit, and 64-bit widths.</returns>
    private static bool IsSupportedFieldSize(int size) => size is 0 or 4 or 8;
}
