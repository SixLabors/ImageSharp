// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Text;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
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

        List<HeifItem> items = new();
        List<HeifItemLink> links = new();
        using ChunkedMemoryStream compressedPixels = new(this.configuration.MemoryAllocator);
        switch (this.encoder.CompressionMethod)
        {
            case HeifCompressionMethod.LegacyJpeg:
                this.CompressPixels(image, compressedPixels, cancellationToken);
                GenerateLegacyJpegItem(image, compressedPixels.Length, items);
                break;
            case HeifCompressionMethod.Av1:
                this.CompressAv1Pixels(image, compressedPixels, items, links, cancellationToken);
                break;
            default:
                throw new NotSupportedException($"HEIF compression method '{this.encoder.CompressionMethod}' is not supported.");
        }

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
    private static void GenerateLegacyJpegItem<TPixel>(Image<TPixel> image, long pixelDataLength, List<HeifItem> items)
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
        Span<byte> buffer = stackalloc byte[28];
        int bytesWritten = WriteBoxHeader(buffer, Heif4CharCode.Ftyp);
        Heif4CharCode majorBrand = this.encoder.CompressionMethod == HeifCompressionMethod.Av1
            ? Heif4CharCode.Avif
            : Heif4CharCode.Mif1;

        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)majorBrand);
        bytesWritten += 4;
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], 0);
        bytesWritten += 4;
        if (majorBrand == Heif4CharCode.Avif)
        {
            // A still AVIF is also a MIAF image collection, so advertise both structural brands with the codec brand.
            BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)Heif4CharCode.Avif);
            bytesWritten += 4;
            BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)Heif4CharCode.Mif1);
            bytesWritten += 4;
            BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)Heif4CharCode.Miaf);
            bytesWritten += 4;
        }

        BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)bytesWritten);
        stream.Write(buffer[..bytesWritten]);

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
        int capacity = 14;
        foreach (HeifItem item in items)
        {
            capacity += 21 + Encoding.UTF8.GetByteCount(item.Name ?? string.Empty);
            if (item.Type == Heif4CharCode.Mime)
            {
                capacity += 1 + Encoding.UTF8.GetByteCount(item.ContentType ?? string.Empty);
                if (item.ContentEncoding is not null)
                {
                    capacity += 1 + Encoding.UTF8.GetByteCount(item.ContentEncoding);
                }
            }
        }

        Span<byte> buffer = memory.GetSpan(memoryOffset, capacity);
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
            bytesWritten += Encoding.UTF8.GetBytes(item.Name ?? string.Empty, buffer[bytesWritten..]);
            buffer[bytesWritten++] = 0;
            if (item.Type == Heif4CharCode.Mime)
            {
                bytesWritten += Encoding.UTF8.GetBytes(item.ContentType ?? string.Empty, buffer[bytesWritten..]);
                buffer[bytesWritten++] = 0;
                if (item.ContentEncoding is not null)
                {
                    bytesWritten += Encoding.UTF8.GetBytes(item.ContentEncoding, buffer[bytesWritten..]);
                    buffer[bytesWritten++] = 0;
                }
            }

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
            if (item.Extent != default)
            {
                bytesWritten += WriteSpatialExtentPropertyBox(memory, memoryOffset + bytesWritten, item);
            }

            byte[]? channelBitDepths = item.ChannelBitDepths;
            if (channelBitDepths is not null)
            {
                bytesWritten += WritePixelInformationPropertyBox(memory, memoryOffset + bytesWritten, channelBitDepths);
            }
            else
            {
                byte? uniformChannelBitDepth = item.UniformChannelBitDepth;
                if (uniformChannelBitDepth is not null)
                {
                    bytesWritten += WritePixelInformationPropertyBox(
                        memory,
                        memoryOffset + bytesWritten,
                        item.ChannelCount,
                        uniformChannelBitDepth.Value);
                }
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

            IccProfile? iccProfile = item.IccProfile;
            if (iccProfile is not null)
            {
                bytesWritten += WriteIccColorInformationPropertyBox(memory, memoryOffset + bytesWritten, iccProfile);
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
        int associationItemCount = 0;
        int associationBoxCapacity = 16;
        foreach (HeifItem item in items)
        {
            int itemPropertyCount = GetPropertyCount(item);
            if (itemPropertyCount == 0)
            {
                continue;
            }

            propertyCount += itemPropertyCount;
            associationItemCount++;
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
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)associationItemCount);
        bytesWritten += 4;
        ushort propertyIndex = 1;
        foreach (HeifItem item in items)
        {
            int itemPropertyCount = GetPropertyCount(item);
            if (itemPropertyCount == 0)
            {
                continue;
            }

            BinaryPrimitives.WriteUInt16BigEndian(buffer[bytesWritten..], (ushort)item.Id);
            bytesWritten += 2;

            buffer[bytesWritten++] = (byte)itemPropertyCount;
            if (item.Extent != default)
            {
                WritePropertyAssociation(buffer, ref bytesWritten, propertyIndex++, largePropertyIndex, false);
            }

            if (item.ChannelBitDepths is not null || item.UniformChannelBitDepth is not null)
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

            if (item.IccProfile is not null)
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
        int count = item.Extent != default ? 1 : 0;
        count += item.ChannelBitDepths is not null || item.UniformChannelBitDepth is not null ? 1 : 0;
        count += item.Av1CodecConfiguration is not null ? 1 : 0;
        count += item.AuxiliaryType is not null ? 1 : 0;
        count += item.IccProfile is not null ? 1 : 0;
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
    /// Writes one common encoded precision for every image channel.
    /// </summary>
    /// <param name="memory">The expanding metadata buffer.</param>
    /// <param name="memoryOffset">The destination offset within the property container.</param>
    /// <param name="channelCount">The number of encoded image channels.</param>
    /// <param name="channelBitDepth">The common encoded precision.</param>
    /// <returns>The complete pixel-information-box length.</returns>
    private static int WritePixelInformationPropertyBox(
        AutoExpandingMemory<byte> memory,
        int memoryOffset,
        int channelCount,
        byte channelBitDepth)
    {
        Span<byte> buffer = memory.GetSpan(memoryOffset, 13 + channelCount);
        int bytesWritten = WriteBoxHeader(buffer, Heif4CharCode.Pixi, 0, 0);
        buffer[bytesWritten++] = (byte)channelCount;
        buffer.Slice(bytesWritten, channelCount).Fill(channelBitDepth);
        bytesWritten += channelCount;

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
    /// Writes an unrestricted ICC color profile for a color image item.
    /// </summary>
    /// <param name="memory">The expanding metadata buffer.</param>
    /// <param name="memoryOffset">The destination offset within the property container.</param>
    /// <param name="profile">The ICC profile to write.</param>
    /// <returns>The complete color-information-box length.</returns>
    private static int WriteIccColorInformationPropertyBox(
        AutoExpandingMemory<byte> memory,
        int memoryOffset,
        IccProfile profile)
    {
        ReadOnlyMemory<byte> profileData = profile.GetDataForWriting();
        Span<byte> buffer = memory.GetSpan(memoryOffset, 12 + profileData.Length);
        int bytesWritten = WriteBoxHeader(buffer, Heif4CharCode.Colr);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)Heif4CharCode.Prof);
        bytesWritten += 4;
        profileData.Span.CopyTo(buffer[bytesWritten..]);
        bytesWritten += profileData.Length;

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
    /// Maps the public lossy quality scale through libaom's external quantizer scale to its internal quantizer index.
    /// </summary>
    /// <param name="quality">The lossy quality in the inclusive range zero through one hundred.</param>
    /// <returns>The AV1 quantizer index.</returns>
    public static int GetAv1QuantizerIndex(int quality)
    {
        int scaledQuality = (100 - quality) * 63;
        int quantizer = (scaledQuality + 50) / 100;

        // External quantizer zero maps to the codec's lossless qindex. Keep quality 100 lossy as its public contract requires.
        quantizer = Math.Max(quantizer, 1);
        return quantizer < 62 ? quantizer * 4 : quantizer == 62 ? 249 : 255;
    }

    /// <summary>
    /// Encodes the source root frame into AV1 color and optional auxiliary-alpha item payloads.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel format.</typeparam>
    /// <param name="image">The source image.</param>
    /// <param name="stream">The shared destination for consecutive item payloads.</param>
    /// <param name="items">The destination item declarations.</param>
    /// <param name="links">The destination item relationships.</param>
    /// <param name="cancellationToken">The token used to cancel payload encoding.</param>
    private void CompressAv1Pixels<TPixel>(
        Image<TPixel> image,
        ChunkedMemoryStream stream,
        List<HeifItem> items,
        List<HeifItemLink> links,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (this.encoder.Lossless)
        {
            throw new NotSupportedException("Lossless AV1 encoding is not implemented.");
        }

        if (image.Frames.Count != 1)
        {
            throw new NotSupportedException("AV1 image-sequence encoding is not implemented.");
        }

        HeifMetadata metadata = image.Metadata.GetHeifMetadata();
        HeifBitDepth bitDepth = this.encoder.BitDepth ?? metadata.BitDepth;
        Av1BitDepth av1BitDepth = bitDepth switch
        {
            HeifBitDepth.Bit8 => Av1BitDepth.EightBit,
            HeifBitDepth.Bit10 => Av1BitDepth.TenBit,
            HeifBitDepth.Bit12 => Av1BitDepth.TwelveBit,
            _ => throw new NotSupportedException($"HEIF bit depth '{bitDepth}' is not supported.")
        };

        HeifChromaSubsampling chromaSubsampling = this.encoder.ChromaSubsampling ??
            (metadata.IsMonochrome ? HeifChromaSubsampling.Monochrome : HeifChromaSubsampling.Yuv420);

        (bool isMonochrome, bool subsamplingX, bool subsamplingY) = chromaSubsampling switch
        {
            HeifChromaSubsampling.Monochrome => (true, true, true),
            HeifChromaSubsampling.Yuv420 => (false, true, true),
            HeifChromaSubsampling.Yuv422 => (false, true, false),
            HeifChromaSubsampling.Yuv444 => (false, false, false),
            _ => throw new NotSupportedException($"HEIF chroma sampling '{chromaSubsampling}' is not supported.")
        };

        CicpProfile? sourceColorProfile = image.Metadata.CicpProfile;
        CicpProfile colorProfile;
        if (sourceColorProfile is null)
        {
            colorProfile = new CicpProfile(2, 2, 6, false);
        }
        else
        {
            bool identityMatrix = sourceColorProfile.MatrixCoefficients == CicpMatrixCoefficients.Identity;
            bool legalIdentityMatrix = !isMonochrome
                && chromaSubsampling == HeifChromaSubsampling.Yuv444
                && sourceColorProfile.ColorPrimaries == CicpColorPrimaries.ItuRBt709_6
                && sourceColorProfile.TransferCharacteristics == CicpTransferCharacteristics.Iec61966_2_1;

            if (sourceColorProfile.MatrixCoefficients == CicpMatrixCoefficients.Unspecified
                || (identityMatrix && !legalIdentityMatrix))
            {
                // The converter uses BT.601 for unspecified or incompatible identity signaling, so record that actual matrix.
                colorProfile = new CicpProfile(
                    (byte)sourceColorProfile.ColorPrimaries,
                    (byte)sourceColorProfile.TransferCharacteristics,
                    (byte)CicpMatrixCoefficients.ItuRBt601_7_525,
                    sourceColorProfile.FullRange);
            }
            else if (identityMatrix && !sourceColorProfile.FullRange)
            {
                colorProfile = new CicpProfile(
                    (byte)sourceColorProfile.ColorPrimaries,
                    (byte)sourceColorProfile.TransferCharacteristics,
                    (byte)sourceColorProfile.MatrixCoefficients,
                    true);
            }
            else
            {
                colorProfile = sourceColorProfile;
            }
        }

        ObuColorConfig colorConfig = new()
        {
            IsColorDescriptionPresent = true,
            IsMonochrome = isMonochrome,
            ColorPrimaries = (ObuColorPrimaries)colorProfile.ColorPrimaries,
            TransferCharacteristics = (ObuTransferCharacteristics)colorProfile.TransferCharacteristics,
            MatrixCoefficients = (ObuMatrixCoefficients)colorProfile.MatrixCoefficients,
            ColorRange = colorProfile.FullRange,
            SubSamplingX = subsamplingX,
            SubSamplingY = subsamplingY,
            ChromaSamplePosition = ObuChromoSamplePosition.Unknown,
            BitDepth = av1BitDepth
        };

        int quality = this.encoder.Quality ?? 75;
        int qIndex = GetAv1QuantizerIndex(quality);
        cancellationToken.ThrowIfCancellationRequested();
        ObuSequenceHeader colorHeader = Av1FrameEncoder.Encode(
            this.configuration,
            image.Frames.RootFrame,
            stream,
            colorConfig,
            qIndex,
            this.encoder.Effort);

        long colorLength = stream.Length;
        byte channelBitDepth = (byte)bitDepth;
        HeifItem colorItem = new(Heif4CharCode.Av01, 1)
        {
            ChannelCount = isMonochrome ? 1 : 3,
            UniformChannelBitDepth = channelBitDepth,
            BitsPerPixel = channelBitDepth * (isMonochrome ? 1 : 3),
            Av1CodecConfiguration = new Av1CodecConfiguration(colorHeader),
            IccProfile = this.encoder.SkipMetadata ? null : image.Metadata.IccProfile,
            CicpProfile = colorProfile
        };

        colorItem.DataLocations.Add(new HeifLocation(HeifLocationOffsetOrigin.FileOffset, 0L, 0L, colorLength));
        colorItem.SetExtent(image.Size);
        items.Add(colorItem);

        bool hasAlpha = TPixel.GetPixelTypeInfo().AlphaRepresentation != PixelAlphaRepresentation.None;
        if (hasAlpha)
        {
            ObuColorConfig alphaConfig = new()
            {
                IsMonochrome = true,
                ColorRange = true,
                SubSamplingX = true,
                SubSamplingY = true,
                BitDepth = av1BitDepth
            };

            int alphaQuality = this.encoder.AlphaQuality ?? quality;
            int alphaQIndex = GetAv1QuantizerIndex(alphaQuality);
            cancellationToken.ThrowIfCancellationRequested();
            long alphaOffset = stream.Length;
            ObuSequenceHeader alphaHeader = Av1FrameEncoder.EncodeAlpha(
                this.configuration,
                image.Frames.RootFrame,
                stream,
                alphaConfig,
                alphaQIndex,
                this.encoder.Effort);

            long alphaLength = stream.Length - alphaOffset;
            HeifItem alphaItem = new(Heif4CharCode.Av01, 2)
            {
                ChannelCount = 1,
                UniformChannelBitDepth = channelBitDepth,
                BitsPerPixel = channelBitDepth,
                Av1CodecConfiguration = new Av1CodecConfiguration(alphaHeader),
                AuxiliaryType = HeifConstants.AlphaAuxiliaryType
            };

            alphaItem.DataLocations.Add(new HeifLocation(HeifLocationOffsetOrigin.FileOffset, 0L, alphaOffset, alphaLength));
            alphaItem.SetExtent(image.Size);
            items.Add(alphaItem);
            HeifItemLink alphaLink = new(Heif4CharCode.Auxl, alphaItem.Id);
            alphaLink.DestinationIds.Add(colorItem.Id);
            links.Add(alphaLink);
        }

        if (this.encoder.SkipMetadata)
        {
            return;
        }

        byte[]? exifData = image.Metadata.ExifProfile?.ToByteArray();
        if (exifData is not null && exifData.Length > 0)
        {
            int tiffHeaderOffset = -1;

            // The HEIF Exif prefix identifies the first TIFF byte-order marker, which can follow an optional Exif
            // identifier in profiles supplied directly by callers.
            for (int i = 0; i <= exifData.Length - 4; i++)
            {
                bool isBigEndianTiff = exifData[i] == (byte)'M'
                    && exifData[i + 1] == (byte)'M'
                    && exifData[i + 2] == 0
                    && exifData[i + 3] == 42;

                bool isLittleEndianTiff = exifData[i] == (byte)'I'
                    && exifData[i + 1] == (byte)'I'
                    && exifData[i + 2] == 42
                    && exifData[i + 3] == 0;

                if (isBigEndianTiff || isLittleEndianTiff)
                {
                    tiffHeaderOffset = i;
                    break;
                }
            }

            if (tiffHeaderOffset < 0)
            {
                throw new ImageFormatException("The Exif profile does not contain a TIFF header.");
            }

            long exifOffset = stream.Length;
            Span<byte> offsetBuffer = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(offsetBuffer, (uint)tiffHeaderOffset);
            stream.Write(offsetBuffer);
            stream.Write(exifData);

            HeifItem exifItem = new(Heif4CharCode.Exif, (uint)items.Count + 1)
            {
                Name = "Exif"
            };

            exifItem.DataLocations.Add(
                new HeifLocation(
                    HeifLocationOffsetOrigin.FileOffset,
                    0L,
                    exifOffset,
                    4L + exifData.Length));

            items.Add(exifItem);
            HeifItemLink exifLink = new(Heif4CharCode.Cdsc, exifItem.Id);
            exifLink.DestinationIds.Add(colorItem.Id);
            links.Add(exifLink);
        }

        byte[]? xmpData = image.Metadata.XmpProfile?.Data;
        if (xmpData is not null && xmpData.Length > 0)
        {
            long xmpOffset = stream.Length;
            stream.Write(xmpData);
            HeifItem xmpItem = new(Heif4CharCode.Mime, (uint)items.Count + 1)
            {
                Name = "XMP",
                ContentType = "application/rdf+xml"
            };

            xmpItem.DataLocations.Add(
                new HeifLocation(
                    HeifLocationOffsetOrigin.FileOffset,
                    0L,
                    xmpOffset,
                    xmpData.Length));

            items.Add(xmpItem);
            HeifItemLink xmpLink = new(Heif4CharCode.Cdsc, xmpItem.Id);
            xmpLink.DestinationIds.Add(colorItem.Id);
            links.Add(xmpLink);
        }
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
            ColorType = colorType,
            SkipMetadata = this.encoder.SkipMetadata
        };

        // ImageEncoder is a synchronous contract. Wait for the cancellable JPEG operation so HEIF encoding
        // cannot return while its pooled item payload is still being produced.
        image.SaveAsJpegAsync(stream, encoder, cancellationToken).GetAwaiter().GetResult();
    }
}
