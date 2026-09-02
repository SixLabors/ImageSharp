// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Text;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Image encoder for writing an image to a stream as a HEIF image.
/// </summary>
internal sealed class HeifEncoderCore
{
    /// <summary>
    /// The global configuration.
    /// </summary>
    private readonly Configuration configuration;

    /// <summary>
    /// The encoder with options.
    /// </summary>
    private readonly HeifEncoder encoder;

    /// <summary>
    /// Initializes a new instance of the <see cref="HeifEncoderCore"/> class.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="encoder">The encoder with options.</param>
    public HeifEncoderCore(Configuration configuration, HeifEncoder encoder)
    {
        this.configuration = configuration;
        this.encoder = encoder;
    }

    /// <summary>
    /// Encodes the image to the specified stream from the <see cref="ImageFrame{TPixel}"/>.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="image">The <see cref="ImageFrame{TPixel}"/> to encode from.</param>
    /// <param name="stream">The <see cref="Stream"/> to encode the image data to.</param>
    /// <param name="cancellationToken">The token to request cancellation.</param>
    public void Encode<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Guard.NotNull(image, nameof(image));
        Guard.NotNull(stream, nameof(stream));

        using ChunkedMemoryStream compressedPixels = new(this.configuration.MemoryAllocator);
        switch (this.encoder.CompressionMethod)
        {
            case HeifCompressionMethod.LegacyJpeg:
                this.CompressPixels(image, compressedPixels, cancellationToken);
                break;
            case HeifCompressionMethod.Av1:
                throw new NotSupportedException("AV1 encoding is not implemented.");
            default:
                throw new NotSupportedException($"HEIF compression method '{this.encoder.CompressionMethod}' is not supported.");
        }

        List<HeifItem> items = new();
        List<HeifItemLink> links = new();
        GenerateItems(image, compressedPixels.Length, items);

        // Write out the generated header and pixels.
        long metadataBoxOffset = this.WriteFileTypeBox(stream);
        this.WriteMetadataBox(items, links, metadataBoxOffset, stream);
        this.WriteMediaDataBox(compressedPixels, stream);
        stream.Flush();
    }

    /// <summary>
    /// Builds the item declarations and relationships for the encoded image payload.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel format.</typeparam>
    /// <param name="image">The source image.</param>
    /// <param name="pixelDataLength">The encoded primary-item payload length.</param>
    /// <param name="items">The destination item collection.</param>
    private static void GenerateItems<TPixel>(Image<TPixel> image, long pixelDataLength, List<HeifItem> items)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        HeifItem primaryItem = new(Heif4CharCode.Jpeg, 1u);
        primaryItem.DataLocations.Add(new HeifLocation(HeifLocationOffsetOrigin.FileOffset, 0L, 0L, pixelDataLength));
        primaryItem.BitsPerPixel = 24;
        primaryItem.ChannelCount = 3;
        primaryItem.SetExtent(image.Size);
        items.Add(primaryItem);

        // No item relationship is emitted until the writer has a distinct derived image,
        // thumbnail, auxiliary image, or metadata item to reference.
    }

    /// <summary>
    /// Writes an eight-byte ISO BMFF basic box header with a placeholder size.
    /// </summary>
    /// <param name="buffer">The destination beginning at the box size field.</param>
    /// <param name="type">The box four-character code.</param>
    /// <returns>The number of header bytes written.</returns>
    private static int WriteBoxHeader(Span<byte> buffer, Heif4CharCode type)
    {
        int bytesWritten = 0;
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], 8U);
        bytesWritten += 4;
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)type);
        bytesWritten += 4;

        return bytesWritten;
    }

    /// <summary>
    /// Writes a 12-byte ISO BMFF full-box header with a placeholder size.
    /// </summary>
    /// <param name="buffer">The destination beginning at the box size field.</param>
    /// <param name="type">The box four-character code.</param>
    /// <param name="version">The full-box syntax version.</param>
    /// <param name="flags">The 24-bit full-box flags value.</param>
    /// <returns>The number of header bytes written.</returns>
    private static int WriteBoxHeader(Span<byte> buffer, Heif4CharCode type, byte version, uint flags)
    {
        int bytesWritten = 0;
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], 12);
        bytesWritten += 4;
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)type);
        bytesWritten += 4;

        // Writing the 24-bit flags as a big-endian 32-bit value establishes the three flag bytes, after which the
        // version overwrites the leading byte to form the full-box version-and-flags word.
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], flags);
        buffer[bytesWritten] = version;
        bytesWritten += 4;

        return bytesWritten;
    }

    /// <summary>
    /// Writes the major brand, minor version, and compatible brands for the current HEIF output.
    /// </summary>
    /// <param name="stream">The destination stream.</param>
    /// <returns>The number of bytes written.</returns>
    private int WriteFileTypeBox(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[16];
        int bytesWritten = WriteBoxHeader(buffer, Heif4CharCode.Ftyp);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)Heif4CharCode.Mif1);
        bytesWritten += 4;
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], 0);
        bytesWritten += 4;

        BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)bytesWritten);
        stream.Write(buffer);

        return bytesWritten;
    }

    /// <summary>
    /// Writes the metadata box containing item declarations, relationships, properties, and file locations.
    /// </summary>
    /// <param name="items">The declared image and metadata items.</param>
    /// <param name="links">The typed relationships between items.</param>
    /// <param name="metadataBoxOffset">The metadata box offset from the start of the encoded file.</param>
    /// <param name="stream">The destination stream positioned after the file-type box.</param>
    private void WriteMetadataBox(List<HeifItem> items, List<HeifItemLink> links, long metadataBoxOffset, Stream stream)
    {
        using AutoExpandingMemory<byte> memory = new(this.configuration, 0x1000);
        Span<byte> buffer = memory.GetSpan(12);
        int bytesWritten = WriteBoxHeader(buffer, Heif4CharCode.Meta, 0, 0);
        bytesWritten += WriteHandlerBox(memory, bytesWritten);
        bytesWritten += WritePrimaryItemBox(memory, bytesWritten);
        bytesWritten += WriteItemInfoBox(memory, bytesWritten, items);
        if (links.Count > 0)
        {
            // iref is optional and has no meaning without at least one typed item relationship.
            bytesWritten += WriteItemReferenceBox(memory, bytesWritten, items, links);
        }

        bytesWritten += WriteItemPropertiesBox(memory, bytesWritten, items);

        // iloc needs the absolute mdat payload position, but that position depends on the final meta length. Emit it
        // once to establish the stable box size, calculate the following mdat position, then patch the same bytes.
        int itemLocationOffset = bytesWritten;
        bytesWritten += WriteItemLocationBox(memory, bytesWritten, items, 0);

        // The mdat payload immediately follows the completed meta box and its own eight-byte header.
        long mediaDataOffset = checked(metadataBoxOffset + bytesWritten + 8);
        WriteItemLocationBox(memory, itemLocationOffset, items, mediaDataOffset);

        buffer = memory.GetSpan(bytesWritten);
        BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)bytesWritten);
        stream.Write(buffer);
    }

    /// <summary>
    /// Writes the picture metadata handler box.
    /// </summary>
    /// <param name="memory">The expanding metadata buffer.</param>
    /// <param name="memoryOffset">The destination offset within the metadata box.</param>
    /// <returns>The complete handler-box length.</returns>
    private static int WriteHandlerBox(AutoExpandingMemory<byte> memory, int memoryOffset)
    {
        Span<byte> buffer = memory.GetSpan(memoryOffset, 33);
        int bytesWritten = WriteBoxHeader(buffer, Heif4CharCode.Hdlr, 0, 0);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], 0);
        bytesWritten += 4;
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)Heif4CharCode.Pict);
        bytesWritten += 4;
        for (int i = 0; i < 13; i++)
        {
            buffer[bytesWritten++] = 0;
        }

        BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)bytesWritten);
        return bytesWritten;
    }

    /// <summary>
    /// Writes the identifier of the primary presentation item.
    /// </summary>
    /// <param name="memory">The expanding metadata buffer.</param>
    /// <param name="memoryOffset">The destination offset within the metadata box.</param>
    /// <returns>The complete primary-item-box length.</returns>
    private static int WritePrimaryItemBox(AutoExpandingMemory<byte> memory, int memoryOffset)
    {
        Span<byte> buffer = memory.GetSpan(memoryOffset, 14);
        int bytesWritten = WriteBoxHeader(buffer, Heif4CharCode.Pitm, 0, 0);
        BinaryPrimitives.WriteUInt16BigEndian(buffer[bytesWritten..], 1);
        bytesWritten += 2;

        BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)bytesWritten);
        return bytesWritten;
    }

    /// <summary>
    /// Writes the item-information box and one version-two entry for each item.
    /// </summary>
    /// <param name="memory">The expanding metadata buffer.</param>
    /// <param name="memoryOffset">The destination offset within the metadata box.</param>
    /// <param name="items">The items to declare.</param>
    /// <returns>The complete item-information-box length.</returns>
    private static int WriteItemInfoBox(AutoExpandingMemory<byte> memory, int memoryOffset, List<HeifItem> items)
    {
        Span<byte> buffer = memory.GetSpan(memoryOffset, 14 + (items.Count * 21));
        int bytesWritten = WriteBoxHeader(buffer, Heif4CharCode.Iinf, 0, 0);
        BinaryPrimitives.WriteUInt16BigEndian(buffer[bytesWritten..], (ushort)items.Count);
        bytesWritten += 2;
        foreach (HeifItem item in items)
        {
            int itemLengthOffset = bytesWritten;
            bytesWritten += WriteBoxHeader(buffer[bytesWritten..], Heif4CharCode.Infe, 2, 0);
            BinaryPrimitives.WriteUInt16BigEndian(buffer[bytesWritten..], (ushort)item.Id);
            bytesWritten += 2;
            BinaryPrimitives.WriteUInt16BigEndian(buffer[bytesWritten..], 0);
            bytesWritten += 2;
            BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)item.Type);
            bytesWritten += 4;
            buffer[bytesWritten++] = 0;

            BinaryPrimitives.WriteUInt32BigEndian(buffer[itemLengthOffset..], (uint)(bytesWritten - itemLengthOffset));
        }

        BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)bytesWritten);
        return bytesWritten;
    }

    /// <summary>
    /// Writes typed item-reference child boxes using 16-bit item identifiers.
    /// </summary>
    /// <param name="memory">The expanding metadata buffer.</param>
    /// <param name="memoryOffset">The destination offset within the metadata box.</param>
    /// <param name="items">The declared items used to size the destination.</param>
    /// <param name="links">The relationships to write.</param>
    /// <returns>The complete item-reference-box length.</returns>
    private static int WriteItemReferenceBox(AutoExpandingMemory<byte> memory, int memoryOffset, List<HeifItem> items, List<HeifItemLink> links)
    {
        Span<byte> buffer = memory.GetSpan(memoryOffset, 12 + (links.Count * (12 + (items.Count * 2))));
        int bytesWritten = WriteBoxHeader(buffer, Heif4CharCode.Iref, 0, 0);
        foreach (HeifItemLink link in links)
        {
            int itemLengthOffset = bytesWritten;
            bytesWritten += WriteBoxHeader(buffer[bytesWritten..], link.Type);
            BinaryPrimitives.WriteUInt16BigEndian(buffer[bytesWritten..], (ushort)link.SourceId);
            bytesWritten += 2;
            BinaryPrimitives.WriteUInt16BigEndian(buffer[bytesWritten..], (ushort)link.DestinationIds.Count);
            bytesWritten += 2;
            foreach (uint destId in link.DestinationIds)
            {
                BinaryPrimitives.WriteUInt16BigEndian(buffer[bytesWritten..], (ushort)destId);
                bytesWritten += 2;
            }

            BinaryPrimitives.WriteUInt32BigEndian(buffer[itemLengthOffset..], (uint)(bytesWritten - itemLengthOffset));
        }

        BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)bytesWritten);
        return bytesWritten;
    }

    /// <summary>
    /// Writes spatial-extent properties and their one-based item associations.
    /// </summary>
    /// <param name="memory">The expanding metadata buffer.</param>
    /// <param name="memoryOffset">The destination offset within the metadata box.</param>
    /// <param name="items">The items whose dimensions are written and associated.</param>
    /// <returns>The complete item-properties-box length.</returns>
    public static int WriteItemPropertiesBox(AutoExpandingMemory<byte> memory, int memoryOffset, List<HeifItem> items)
    {
        Span<byte> buffer = memory.GetSpan(memoryOffset, 20);
        int bytesWritten = WriteBoxHeader(buffer, Heif4CharCode.Iprp);

        // ipco order defines the one-based property indices written later in ipma.
        int ipcoLengthOffset = bytesWritten;
        bytesWritten += WriteBoxHeader(buffer[bytesWritten..], Heif4CharCode.Ipco);
        foreach (HeifItem item in items)
        {
            bytesWritten += WriteSpatialExtentPropertyBox(memory, memoryOffset + bytesWritten, item);

            byte[]? channelBitDepths = item.ChannelBitDepths;
            if (channelBitDepths is not null)
            {
                bytesWritten += WritePixelInformationPropertyBox(memory, memoryOffset + bytesWritten, channelBitDepths);
            }

            Av1CodecConfiguration? codecConfiguration = item.Av1CodecConfiguration;
            if (codecConfiguration is not null)
            {
                bytesWritten += WriteAv1CodecConfigurationPropertyBox(memory, memoryOffset + bytesWritten, codecConfiguration);
            }

            string? auxiliaryType = item.AuxiliaryType;
            if (auxiliaryType is not null)
            {
                bytesWritten += WriteAuxiliaryTypePropertyBox(memory, memoryOffset + bytesWritten, auxiliaryType);
            }

            CicpProfile? cicpProfile = item.CicpProfile;
            if (cicpProfile is not null)
            {
                bytesWritten += WriteColorInformationPropertyBox(memory, memoryOffset + bytesWritten, cicpProfile);
            }
        }

        buffer = memory.GetSpan(memoryOffset, bytesWritten);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[ipcoLengthOffset..], (uint)(bytesWritten - ipcoLengthOffset));
        int propertyCount = 0;
        int associationBoxCapacity = 16;
        foreach (HeifItem item in items)
        {
            int itemPropertyCount = GetPropertyCount(item);
            propertyCount += itemPropertyCount;
            associationBoxCapacity += 3 + itemPropertyCount;
        }

        bool largePropertyIndex = propertyCount > 0x7F;
        if (largePropertyIndex)
        {
            associationBoxCapacity += propertyCount;
        }

        buffer = memory.GetSpan(memoryOffset, bytesWritten + associationBoxCapacity);

        // ipma uses a 15-bit index only when the property table cannot fit in the compact seven-bit form.
        int ipmaLengthOffset = bytesWritten;
        bytesWritten += WriteBoxHeader(buffer[bytesWritten..], Heif4CharCode.Ipma, 0, largePropertyIndex ? 1U : 0U);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)items.Count);
        bytesWritten += 4;
        ushort propertyIndex = 1;
        foreach (HeifItem item in items)
        {
            BinaryPrimitives.WriteUInt16BigEndian(buffer[bytesWritten..], (ushort)item.Id);
            bytesWritten += 2;

            int itemPropertyCount = GetPropertyCount(item);
            buffer[bytesWritten++] = (byte)itemPropertyCount;
            WritePropertyAssociation(buffer, ref bytesWritten, propertyIndex++, largePropertyIndex, false);

            if (item.ChannelBitDepths is not null)
            {
                WritePropertyAssociation(buffer, ref bytesWritten, propertyIndex++, largePropertyIndex, false);
            }

            if (item.Av1CodecConfiguration is not null)
            {
                WritePropertyAssociation(buffer, ref bytesWritten, propertyIndex++, largePropertyIndex, true);
            }

            if (item.AuxiliaryType is not null)
            {
                WritePropertyAssociation(buffer, ref bytesWritten, propertyIndex++, largePropertyIndex, false);
            }

            if (item.CicpProfile is not null)
            {
                WritePropertyAssociation(buffer, ref bytesWritten, propertyIndex++, largePropertyIndex, false);
            }
        }

        BinaryPrimitives.WriteUInt32BigEndian(buffer[ipmaLengthOffset..], (uint)(bytesWritten - ipmaLengthOffset));

        // Update size of enclosing 'iprp' box.
        BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)bytesWritten);
        return bytesWritten;
    }

    /// <summary>
    /// Gets the number of properties emitted for an item.
    /// </summary>
    /// <param name="item">The item whose populated properties are counted.</param>
    /// <returns>The number of emitted properties.</returns>
    private static int GetPropertyCount(HeifItem item)
    {
        int count = 1;
        count += item.ChannelBitDepths is not null ? 1 : 0;
        count += item.Av1CodecConfiguration is not null ? 1 : 0;
        count += item.AuxiliaryType is not null ? 1 : 0;
        count += item.CicpProfile is not null ? 1 : 0;
        return count;
    }

    /// <summary>
    /// Writes one compact or extended property association.
    /// </summary>
    /// <param name="buffer">The item-property-association destination.</param>
    /// <param name="offset">The current destination offset, advanced past the association.</param>
    /// <param name="propertyIndex">The one-based property index.</param>
    /// <param name="largePropertyIndex">Whether the association uses a 15-bit property index.</param>
    /// <param name="essential">Whether decoding the item requires understanding this property.</param>
    private static void WritePropertyAssociation(
        Span<byte> buffer,
        ref int offset,
        ushort propertyIndex,
        bool largePropertyIndex,
        bool essential)
    {
        if (largePropertyIndex)
        {
            ushort association = essential ? (ushort)(propertyIndex | 0x8000) : propertyIndex;
            BinaryPrimitives.WriteUInt16BigEndian(buffer[offset..], association);
            offset += 2;
        }
        else
        {
            buffer[offset++] = essential ? (byte)(propertyIndex | 0x80) : (byte)propertyIndex;
        }
    }

    /// <summary>
    /// Writes the encoded precision of each image channel.
    /// </summary>
    /// <param name="memory">The expanding metadata buffer.</param>
    /// <param name="memoryOffset">The destination offset within the property container.</param>
    /// <param name="channelBitDepths">The encoded precision of each channel.</param>
    /// <returns>The complete pixel-information-box length.</returns>
    private static int WritePixelInformationPropertyBox(
        AutoExpandingMemory<byte> memory,
        int memoryOffset,
        ReadOnlySpan<byte> channelBitDepths)
    {
        Span<byte> buffer = memory.GetSpan(memoryOffset, 13 + channelBitDepths.Length);
        int bytesWritten = WriteBoxHeader(buffer, Heif4CharCode.Pixi, 0, 0);
        buffer[bytesWritten++] = (byte)channelBitDepths.Length;
        channelBitDepths.CopyTo(buffer[bytesWritten..]);
        bytesWritten += channelBitDepths.Length;

        BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)bytesWritten);
        return bytesWritten;
    }

    /// <summary>
    /// Writes an AV1 codec-configuration property.
    /// </summary>
    /// <param name="memory">The expanding metadata buffer.</param>
    /// <param name="memoryOffset">The destination offset within the property container.</param>
    /// <param name="configuration">The fixed image configuration.</param>
    /// <returns>The complete AV1 codec-configuration-box length.</returns>
    private static int WriteAv1CodecConfigurationPropertyBox(
        AutoExpandingMemory<byte> memory,
        int memoryOffset,
        Av1CodecConfiguration configuration)
    {
        Span<byte> buffer = memory.GetSpan(memoryOffset, 8 + Av1CodecConfiguration.FixedHeaderSize);
        int bytesWritten = WriteBoxHeader(buffer, Heif4CharCode.Av1C);
        configuration.WriteFixedHeader(buffer.Slice(bytesWritten, Av1CodecConfiguration.FixedHeaderSize));
        bytesWritten += Av1CodecConfiguration.FixedHeaderSize;

        BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)bytesWritten);
        return bytesWritten;
    }

    /// <summary>
    /// Writes the registered type of an auxiliary image item.
    /// </summary>
    /// <param name="memory">The expanding metadata buffer.</param>
    /// <param name="memoryOffset">The destination offset within the property container.</param>
    /// <param name="auxiliaryType">The null-terminated registered auxiliary type.</param>
    /// <returns>The complete auxiliary-type-box length.</returns>
    private static int WriteAuxiliaryTypePropertyBox(
        AutoExpandingMemory<byte> memory,
        int memoryOffset,
        string auxiliaryType)
    {
        int auxiliaryTypeLength = Encoding.UTF8.GetByteCount(auxiliaryType);
        Span<byte> buffer = memory.GetSpan(memoryOffset, 13 + auxiliaryTypeLength);
        int bytesWritten = WriteBoxHeader(buffer, Heif4CharCode.AuxC, 0, 0);
        bytesWritten += Encoding.UTF8.GetBytes(auxiliaryType, buffer[bytesWritten..]);
        buffer[bytesWritten++] = 0;

        BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)bytesWritten);
        return bytesWritten;
    }

    /// <summary>
    /// Writes an H.273 color description for a color image item.
    /// </summary>
    /// <param name="memory">The expanding metadata buffer.</param>
    /// <param name="memoryOffset">The destination offset within the property container.</param>
    /// <param name="profile">The color description to write.</param>
    /// <returns>The complete color-information-box length.</returns>
    private static int WriteColorInformationPropertyBox(
        AutoExpandingMemory<byte> memory,
        int memoryOffset,
        CicpProfile profile)
    {
        Span<byte> buffer = memory.GetSpan(memoryOffset, 19);
        int bytesWritten = WriteBoxHeader(buffer, Heif4CharCode.Colr);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)Heif4CharCode.Nclx);
        bytesWritten += 4;
        BinaryPrimitives.WriteUInt16BigEndian(buffer[bytesWritten..], (ushort)profile.ColorPrimaries);
        bytesWritten += 2;
        BinaryPrimitives.WriteUInt16BigEndian(buffer[bytesWritten..], (ushort)profile.TransferCharacteristics);
        bytesWritten += 2;
        BinaryPrimitives.WriteUInt16BigEndian(buffer[bytesWritten..], (ushort)profile.MatrixCoefficients);
        bytesWritten += 2;
        buffer[bytesWritten++] = profile.FullRange ? (byte)0x80 : (byte)0;

        BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)bytesWritten);
        return bytesWritten;
    }

    /// <summary>
    /// Writes an item's display width and height as an image-spatial-extents property.
    /// </summary>
    /// <param name="memory">The expanding metadata buffer.</param>
    /// <param name="memoryOffset">The destination offset within the property container.</param>
    /// <param name="item">The item whose extent is written.</param>
    /// <returns>The complete image-spatial-extents-box length.</returns>
    private static int WriteSpatialExtentPropertyBox(AutoExpandingMemory<byte> memory, int memoryOffset, HeifItem item)
    {
        Span<byte> buffer = memory.GetSpan(memoryOffset, 20);
        int bytesWritten = WriteBoxHeader(buffer, Heif4CharCode.Ispe, 0, 0);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)item.Extent.Width);
        bytesWritten += 4;
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)item.Extent.Height);
        bytesWritten += 4;

        BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)bytesWritten);
        return bytesWritten;
    }

    /// <summary>
    /// Writes version-one file-relative locations for every ordered item extent.
    /// </summary>
    /// <param name="memory">The expanding metadata buffer.</param>
    /// <param name="memoryOffset">The destination offset within the metadata box.</param>
    /// <param name="items">The items and relative payload extents to locate.</param>
    /// <param name="mediaDataOffset">The absolute stream offset of the media-data payload.</param>
    /// <returns>The complete item-location-box length.</returns>
    private static int WriteItemLocationBox(AutoExpandingMemory<byte> memory, int memoryOffset, List<HeifItem> items, long mediaDataOffset)
    {
        int extentCount = items.Sum(item => item.DataLocations.Count);
        Span<byte> buffer = memory.GetSpan(memoryOffset, 16 + (items.Count * 8) + (extentCount * 12));
        int bytesWritten = WriteBoxHeader(buffer, Heif4CharCode.Iloc, 1, 0);

        // The high and low nibbles select eight-byte offsets and four-byte lengths. Base offsets and extent indices
        // are omitted, because every generated extent is written as one absolute file offset into mdat.
        buffer[bytesWritten++] = 0x84;
        buffer[bytesWritten++] = 0;
        BinaryPrimitives.WriteUInt16BigEndian(buffer[bytesWritten..], (ushort)items.Count);
        bytesWritten += 2;
        foreach (HeifItem item in items)
        {
            BinaryPrimitives.WriteUInt16BigEndian(buffer[bytesWritten..], (ushort)item.Id);
            bytesWritten += 2;

            // Version 1 stores twelve reserved bits followed by the four-bit construction method.
            BinaryPrimitives.WriteUInt16BigEndian(buffer[bytesWritten..], (ushort)HeifLocationOffsetOrigin.FileOffset);
            bytesWritten += 2;
            BinaryPrimitives.WriteUInt16BigEndian(buffer[bytesWritten..], 0);
            bytesWritten += 2;
            BinaryPrimitives.WriteUInt16BigEndian(buffer[bytesWritten..], (ushort)item.DataLocations.Count);
            bytesWritten += 2;
            foreach (HeifLocation loc in item.DataLocations)
            {
                // Generated locations are relative to the mdat payload until the enclosing meta size is known.
                long absoluteOffset = checked(mediaDataOffset + loc.BaseOffset + loc.Offset);
                BinaryPrimitives.WriteUInt64BigEndian(buffer[bytesWritten..], (ulong)absoluteOffset);
                bytesWritten += 8;
                BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)loc.Length);
                bytesWritten += 4;
            }
        }

        BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)bytesWritten);
        return bytesWritten;
    }

    /// <summary>
    /// Writes the encoded primary-item bytes in a media-data box.
    /// </summary>
    /// <param name="data">The encoded item payload stream.</param>
    /// <param name="stream">The destination stream.</param>
    private void WriteMediaDataBox(ChunkedMemoryStream data, Stream stream)
    {
        Span<byte> buf = stackalloc byte[12];
        int bytesWritten = WriteBoxHeader(buf, Heif4CharCode.Mdat);
        BinaryPrimitives.WriteUInt32BigEndian(buf, checked((uint)(data.Length + bytesWritten)));
        stream.Write(buf[..bytesWritten]);

        data.WriteTo(stream);
    }

    /// <summary>
    /// Encodes the source pixels as the current legacy JPEG item payload.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel format.</typeparam>
    /// <param name="image">The source image.</param>
    /// <param name="stream">The destination for the encoded JPEG item bytes.</param>
    /// <param name="cancellationToken">The token used to cancel payload encoding.</param>
    private void CompressPixels<TPixel>(
        Image<TPixel> image,
        ChunkedMemoryStream stream,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (this.encoder.Lossless)
        {
            throw new NotSupportedException("Legacy JPEG image items do not support lossless encoding.");
        }

        if (this.encoder.BitDepth is not null and not HeifBitDepth.Bit8)
        {
            throw new NotSupportedException("Legacy JPEG image items support only 8-bit component encoding.");
        }

        JpegColorType colorType = this.encoder.ChromaSubsampling switch
        {
            null or HeifChromaSubsampling.Yuv420 => JpegColorType.YCbCrRatio420,
            HeifChromaSubsampling.Yuv422 => JpegColorType.YCbCrRatio422,
            HeifChromaSubsampling.Yuv444 => JpegColorType.YCbCrRatio444,
            HeifChromaSubsampling.Monochrome => JpegColorType.Luminance,
            _ => throw new NotSupportedException($"HEIF chroma sampling '{this.encoder.ChromaSubsampling}' is not supported.")
        };

        JpegEncoder encoder = new()
        {
            // The HEIF quality scale includes zero while the JPEG payload encoder starts at one.
            // Map the lowest HEIF setting to the lowest representable JPEG setting.
            Quality = this.encoder.Quality == 0 ? 1 : this.encoder.Quality,
            ColorType = colorType
        };

        // ImageEncoder is a synchronous contract. Wait for the cancellable JPEG operation so HEIF encoding
        // cannot return while its pooled item payload is still being produced.
        image.SaveAsJpegAsync(stream, encoder, cancellationToken).GetAwaiter().GetResult();
    }
}
