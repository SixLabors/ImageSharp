// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Image encoder for writing an image to a stream as a HEIF image.
/// </summary>
internal sealed partial class HeifEncoderCore
{
    // ISO BMFF box lengths include their size and type fields. Full boxes also include version and flags.
    private const int BasicBoxHeaderLength = 8;
    private const int FullBoxHeaderLength = 12;
    private const int HandlerBoxLength = 33;
    private const int PrimaryItemBoxLength = 14;
    private const int ItemInformationBoxFixedLength = 14;
    private const int ItemInformationEntryFixedLength = 21;
    private const int ItemReferenceBoxFixedLength = 12;
    private const int ItemReferenceEntryFixedLength = 12;
    private const int ItemPropertiesBoxFixedLength = 32;
    private const int PropertyAssociationEntryFixedLength = 3;
    private const int ItemLocationBoxFixedLength = 16;
    private const int ItemLocationEntryFixedLength = 8;
    private const int ItemExtentLength = 12;
    private const int SpatialExtentPropertyBoxLength = 20;
    private const int PixelInformationPropertyBoxFixedLength = 13;
    private const int Av1CodecConfigurationPropertyBoxLength = BasicBoxHeaderLength + Av1CodecConfiguration.FixedHeaderSize;
    private const int AuxiliaryTypePropertyBoxFixedLength = 13;
    private const int IccColorInformationPropertyBoxFixedLength = 12;
    private const int CicpColorInformationPropertyBoxLength = 19;
    private const int MaximumCompactPropertyIndex = 0x7F;
    private const ushort EssentialPropertyFlag = 0x8000;
    private const byte CompactEssentialPropertyFlag = 0x80;
    private const uint HiddenImageItemFlag = 1;

    /// <summary>
    /// The version defined for the AVIF grid item payload.
    /// </summary>
    private const byte GridDescriptorVersion = 0;

    /// <summary>
    /// The largest row or column count representable by a grid descriptor.
    /// </summary>
    private const int MaximumGridAxisCellCount = byte.MaxValue + 1;

    /// <summary>
    /// The minimum width and height permitted for the first cell of an AVIF grid.
    /// </summary>
    private const int MinimumGridCellDimension = 64;

    /// <summary>
    /// The grid descriptor length when output dimensions use 32-bit fields.
    /// </summary>
    private const int LongGridDescriptorLength = 12;

    /// <summary>
    /// Selects 32-bit output dimensions in a grid descriptor.
    /// </summary>
    private const byte LargeGridDimensionsFlag = 1;

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

        switch (this.encoder.CompressionMethod)
        {
            case HeifCompressionMethod.LegacyJpeg:
                break;
            case HeifCompressionMethod.Av1:
                if (image.Frames.Count > 1)
                {
                    if (image.Width > ushort.MaxValue || image.Height > ushort.MaxValue)
                    {
                        throw new NotSupportedException("AV1 image-sequence dimensions cannot exceed 65535 pixels.");
                    }
                }

                break;
            default:
                throw new NotSupportedException($"HEIF compression method '{this.encoder.CompressionMethod}' is not supported.");
        }

        using ChunkedMemoryStream compressedPixels = new(this.configuration.MemoryAllocator);
        if (this.encoder.CompressionMethod == HeifCompressionMethod.Av1 && image.Frames.Count > 1)
        {
            Av1EncodingSettings settings = this.ResolveAv1Encoding(image);
            bool animateRootFrame = this.encoder.AnimateRootFrame
                ?? image.Metadata.GetHeifMetadata().AnimateRootFrame;

            int firstFrameIndex = animateRootFrame ? 0 : 1;
            int sequenceFrameCount = image.Frames.Count - firstFrameIndex;
            int sampleCount = sequenceFrameCount * (settings.HasAlpha ? 2 : 1);
            using IMemoryOwner<HeifSequenceSampleInfo> samplesOwner =
                this.configuration.MemoryAllocator.Allocate<HeifSequenceSampleInfo>(sampleCount);

            List<HeifItem> sequenceItems = new();
            List<HeifItemLink> sequenceLinks = new();
            if (!animateRootFrame)
            {
                Av1ImageItemEncoding primaryImage = this.CompressAv1ImageItem(
                    image.Frames.RootFrame,
                    compressedPixels,
                    settings,
                    cancellationToken);

                this.WriteAv1ImageItems(
                    image,
                    compressedPixels,
                    settings,
                    primaryImage,
                    sequenceItems,
                    sequenceLinks);
            }

            Memory<HeifSequenceSampleInfo> samples = samplesOwner.Memory[..sampleCount];
            HeifSequenceEncoding sequence = this.CompressAv1Sequence(
                image,
                compressedPixels,
                settings,
                samples,
                firstFrameIndex,
                cancellationToken);

            if (animateRootFrame)
            {
                HeifSequenceSampleInfo colorSample = sequence.ColorTrack.Samples[0];
                HeifSequenceTrackEncoding? alphaTrack = sequence.AlphaTrack;
                Av1ImageItemEncoding primaryImage = new(
                    sequence.ColorTrack.Configuration,
                    colorSample.Offset,
                    colorSample.Length,
                    alphaTrack?.Configuration,
                    alphaTrack?.Samples[0].Offset ?? 0,
                    alphaTrack?.Samples[0].Length ?? 0);

                // The primary image item and the first track sample describe the same sync sample. Sharing its
                // extent matches libavif and avoids encoding or storing the root frame twice.
                this.WriteAv1ImageItems(
                    image,
                    compressedPixels,
                    settings,
                    primaryImage,
                    sequenceItems,
                    sequenceLinks);
            }

            int fileTypeLength = this.WriteSequenceFileTypeBox(stream);
            int metadataLength = GetMetadataBoxLength(sequenceItems, sequenceLinks);
            int movieLength = GetSequenceMovieBoxLength(sequence);
            this.WriteMetadataBox(sequenceItems, sequenceLinks, fileTypeLength, movieLength, stream);
            this.WriteSequenceMovieBox(sequence, fileTypeLength + metadataLength, stream);
            this.WriteMediaDataBox(compressedPixels, stream);
            stream.Flush();
            return;
        }

        List<HeifItem> items = new();
        List<HeifItemLink> links = new();
        switch (this.encoder.CompressionMethod)
        {
            case HeifCompressionMethod.LegacyJpeg:
                this.CompressPixels(image, compressedPixels, cancellationToken);
                GenerateLegacyJpegItem(image, compressedPixels.Length, items);
                break;
            case HeifCompressionMethod.Av1:
                this.CompressAv1Pixels(image, compressedPixels, items, links, cancellationToken);
                break;
        }

        // Write out the generated header and pixels.
        long metadataBoxOffset = this.WriteFileTypeBox(stream);
        this.WriteMetadataBox(items, links, metadataBoxOffset, 0, stream);
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
    /// <param name="followingBoxLength">The number of bytes between this box and the media-data box.</param>
    /// <param name="stream">The destination stream positioned after the file-type box.</param>
    private void WriteMetadataBox(
        List<HeifItem> items,
        List<HeifItemLink> links,
        long metadataBoxOffset,
        int followingBoxLength,
        Stream stream)
    {
        int metadataLength = GetMetadataBoxLength(items, links);
        using IMemoryOwner<byte> metadataOwner = this.configuration.MemoryAllocator.Allocate<byte>(metadataLength);
        Span<byte> memory = metadataOwner.Memory.Span[..metadataLength];
        Span<byte> buffer = memory[..FullBoxHeaderLength];
        int bytesWritten = WriteBoxHeader(buffer, Heif4CharCode.Meta, 0, 0);
        bytesWritten += WriteHandlerBox(memory, bytesWritten);
        bytesWritten += WritePrimaryItemBox(memory, bytesWritten);
        bytesWritten += WriteItemInfoBox(memory, bytesWritten, items);
        if (links.Count > 0)
        {
            // iref is optional and has no meaning without at least one typed item relationship.
            bytesWritten += WriteItemReferenceBox(memory, bytesWritten, links);
        }

        bytesWritten += WriteItemPropertiesBox(memory, bytesWritten, items);

        // iloc needs the absolute mdat payload position, but that position depends on the final meta length. Emit it
        // once to establish the stable box size, calculate the following mdat position, then patch the same bytes.
        int itemLocationOffset = bytesWritten;
        bytesWritten += WriteItemLocationBox(memory, bytesWritten, items, 0);

        // The mdat payload immediately follows the completed meta box and its own eight-byte header.
        long mediaDataOffset = checked(metadataBoxOffset + bytesWritten + followingBoxLength + BasicBoxHeaderLength);
        WriteItemLocationBox(memory, itemLocationOffset, items, mediaDataOffset);

        buffer = memory[..bytesWritten];
        BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)bytesWritten);
        stream.Write(buffer);
    }

    private static int GetMetadataBoxLength(List<HeifItem> items, List<HeifItemLink> links)
    {
        // All variable-length strings, profiles, relationships, properties, and extents are resolved before
        // allocating the metadata box, so writing it never needs to re-rent or copy a backing buffer.
        return checked(
            FullBoxHeaderLength
            + HandlerBoxLength
            + PrimaryItemBoxLength
            + GetItemInformationBoxLength(items)
            + (links.Count == 0 ? 0 : GetItemReferenceBoxLength(links))
            + GetItemPropertiesBoxLength(items)
            + GetItemLocationBoxLength(items));
    }

    private static int GetItemInformationBoxLength(List<HeifItem> items)
    {
        long length = ItemInformationBoxFixedLength;
        foreach (HeifItem item in items)
        {
            length += ItemInformationEntryFixedLength + Encoding.UTF8.GetByteCount(item.Name ?? string.Empty);
            if (item.Type == Heif4CharCode.Mime)
            {
                length += 1 + Encoding.UTF8.GetByteCount(item.ContentType ?? string.Empty);
                if (item.ContentEncoding is not null)
                {
                    length += 1 + Encoding.UTF8.GetByteCount(item.ContentEncoding);
                }
            }
        }

        return checked((int)length);
    }

    private static int GetItemReferenceBoxLength(List<HeifItemLink> links)
    {
        long length = ItemReferenceBoxFixedLength;
        foreach (HeifItemLink link in links)
        {
            length += ItemReferenceEntryFixedLength + ((long)link.DestinationIds.Count * sizeof(ushort));
        }

        return checked((int)length);
    }

    /// <summary>
    /// Gets the exact number of bytes required for the item-properties box.
    /// </summary>
    /// <param name="items">The items whose properties and associations are counted.</param>
    /// <returns>The complete item-properties-box length.</returns>
    public static int GetItemPropertiesBoxLength(List<HeifItem> items)
    {
        long propertyCount = 0;
        long associationItemCount = 0;
        long associationPropertyCount = 0;
        long propertyBytes = 0;
        foreach (HeifItem item in items)
        {
            HeifItem propertyItem = item.PropertySource ?? item;
            int itemPropertyCount = GetPropertyCount(propertyItem);
            associationItemCount += itemPropertyCount == 0 ? 0 : 1;
            associationPropertyCount += itemPropertyCount;

            if (item.PropertySource is not null)
            {
                continue;
            }

            propertyCount += itemPropertyCount;
            propertyBytes += item.Extent == default ? 0 : SpatialExtentPropertyBoxLength;
            if (item.ChannelBitDepths is not null)
            {
                propertyBytes += PixelInformationPropertyBoxFixedLength + item.ChannelBitDepths.Length;
            }
            else if (item.UniformChannelBitDepth is not null)
            {
                propertyBytes += PixelInformationPropertyBoxFixedLength + item.ChannelCount;
            }

            propertyBytes += item.Av1CodecConfiguration is null
                ? 0
                : Av1CodecConfigurationPropertyBoxLength;
            propertyBytes += item.AuxiliaryType is null
                ? 0
                : AuxiliaryTypePropertyBoxFixedLength + Encoding.UTF8.GetByteCount(item.AuxiliaryType);
            propertyBytes += item.IccProfile is null
                ? 0
                : IccColorInformationPropertyBoxFixedLength + item.GetIccProfileDataForWriting().Length;
            propertyBytes += item.CicpProfile is null ? 0 : CicpColorInformationPropertyBoxLength;
        }

        int associationSize = propertyCount > MaximumCompactPropertyIndex ? sizeof(ushort) : sizeof(byte);
        long length = ItemPropertiesBoxFixedLength
            + propertyBytes
            + (associationItemCount * PropertyAssociationEntryFixedLength)
            + (associationPropertyCount * associationSize);

        return checked((int)length);
    }

    private static int GetItemLocationBoxLength(List<HeifItem> items)
    {
        long extentCount = 0;
        foreach (HeifItem item in items)
        {
            extentCount += item.DataLocations.Count;
        }

        long length =
            ItemLocationBoxFixedLength
            + ((long)items.Count * ItemLocationEntryFixedLength)
            + (extentCount * ItemExtentLength);

        return checked((int)length);
    }

    /// <summary>
    /// Writes the picture metadata handler box.
    /// </summary>
    /// <param name="memory">The preallocated metadata buffer.</param>
    /// <param name="memoryOffset">The destination offset within the metadata box.</param>
    /// <returns>The complete handler-box length.</returns>
    private static int WriteHandlerBox(Span<byte> memory, int memoryOffset)
    {
        Span<byte> buffer = memory.Slice(memoryOffset, HandlerBoxLength);
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
    /// <param name="memory">The preallocated metadata buffer.</param>
    /// <param name="memoryOffset">The destination offset within the metadata box.</param>
    /// <returns>The complete primary-item-box length.</returns>
    private static int WritePrimaryItemBox(Span<byte> memory, int memoryOffset)
    {
        Span<byte> buffer = memory.Slice(memoryOffset, PrimaryItemBoxLength);
        int bytesWritten = WriteBoxHeader(buffer, Heif4CharCode.Pitm, 0, 0);
        BinaryPrimitives.WriteUInt16BigEndian(buffer[bytesWritten..], 1);
        bytesWritten += 2;

        BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)bytesWritten);
        return bytesWritten;
    }

    /// <summary>
    /// Writes the item-information box and one version-two entry for each item.
    /// </summary>
    /// <param name="memory">The preallocated metadata buffer.</param>
    /// <param name="memoryOffset">The destination offset within the metadata box.</param>
    /// <param name="items">The items to declare.</param>
    /// <returns>The complete item-information-box length.</returns>
    private static int WriteItemInfoBox(Span<byte> memory, int memoryOffset, List<HeifItem> items)
    {
        Span<byte> buffer = memory.Slice(memoryOffset, GetItemInformationBoxLength(items));
        int bytesWritten = WriteBoxHeader(buffer, Heif4CharCode.Iinf, 0, 0);
        BinaryPrimitives.WriteUInt16BigEndian(buffer[bytesWritten..], (ushort)items.Count);
        bytesWritten += 2;
        foreach (HeifItem item in items)
        {
            int itemLengthOffset = bytesWritten;
            bytesWritten += WriteBoxHeader(
                buffer[bytesWritten..],
                Heif4CharCode.Infe,
                2,
                item.IsHidden ? HiddenImageItemFlag : 0);

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
    /// <param name="memory">The preallocated metadata buffer.</param>
    /// <param name="memoryOffset">The destination offset within the metadata box.</param>
    /// <param name="links">The relationships to write.</param>
    /// <returns>The complete item-reference-box length.</returns>
    private static int WriteItemReferenceBox(Span<byte> memory, int memoryOffset, List<HeifItemLink> links)
    {
        Span<byte> buffer = memory.Slice(memoryOffset, GetItemReferenceBoxLength(links));
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
    /// <param name="memory">The preallocated metadata buffer.</param>
    /// <param name="memoryOffset">The destination offset within the metadata box.</param>
    /// <param name="items">The items whose dimensions are written and associated.</param>
    /// <returns>The complete item-properties-box length.</returns>
    public static int WriteItemPropertiesBox(Span<byte> memory, int memoryOffset, List<HeifItem> items)
    {
        Span<byte> buffer = memory.Slice(memoryOffset, GetItemPropertiesBoxLength(items));
        int bytesWritten = WriteBoxHeader(buffer, Heif4CharCode.Iprp);

        // ipco order defines the one-based property indices written later in ipma.
        int ipcoLengthOffset = bytesWritten;
        bytesWritten += WriteBoxHeader(buffer[bytesWritten..], Heif4CharCode.Ipco);
        ushort nextPropertyIndex = 1;
        foreach (HeifItem item in items)
        {
            if (item.PropertySource is not null)
            {
                continue;
            }

            item.FirstPropertyIndex = nextPropertyIndex;
            if (item.Extent != default)
            {
                bytesWritten += WriteSpatialExtentPropertyBox(memory, memoryOffset + bytesWritten, item);
                nextPropertyIndex++;
            }

            byte[]? channelBitDepths = item.ChannelBitDepths;
            if (channelBitDepths is not null)
            {
                bytesWritten += WritePixelInformationPropertyBox(memory, memoryOffset + bytesWritten, channelBitDepths);
                nextPropertyIndex++;
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

                    nextPropertyIndex++;
                }
            }

            Av1CodecConfiguration? codecConfiguration = item.Av1CodecConfiguration;
            if (codecConfiguration is not null)
            {
                bytesWritten += WriteAv1CodecConfigurationPropertyBox(memory, memoryOffset + bytesWritten, codecConfiguration);
                nextPropertyIndex++;
            }

            string? auxiliaryType = item.AuxiliaryType;
            if (auxiliaryType is not null)
            {
                bytesWritten += WriteAuxiliaryTypePropertyBox(memory, memoryOffset + bytesWritten, auxiliaryType);
                nextPropertyIndex++;
            }

            IccProfile? iccProfile = item.IccProfile;
            if (iccProfile is not null)
            {
                bytesWritten += WriteIccColorInformationPropertyBox(memory, memoryOffset + bytesWritten, item.GetIccProfileDataForWriting());
                nextPropertyIndex++;
            }

            CicpProfile? cicpProfile = item.CicpProfile;
            if (cicpProfile is not null)
            {
                bytesWritten += WriteColorInformationPropertyBox(memory, memoryOffset + bytesWritten, cicpProfile);
                nextPropertyIndex++;
            }
        }

        BinaryPrimitives.WriteUInt32BigEndian(buffer[ipcoLengthOffset..], (uint)(bytesWritten - ipcoLengthOffset));
        int propertyCount = nextPropertyIndex - 1;
        int associationItemCount = 0;
        foreach (HeifItem item in items)
        {
            HeifItem propertyItem = item.PropertySource ?? item;
            int itemPropertyCount = GetPropertyCount(propertyItem);
            if (itemPropertyCount == 0)
            {
                continue;
            }

            associationItemCount++;
        }

        bool largePropertyIndex = propertyCount > MaximumCompactPropertyIndex;

        // ipma uses a 15-bit index only when the property table cannot fit in the compact seven-bit form.
        int ipmaLengthOffset = bytesWritten;
        bytesWritten += WriteBoxHeader(buffer[bytesWritten..], Heif4CharCode.Ipma, 0, largePropertyIndex ? 1U : 0U);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)associationItemCount);
        bytesWritten += 4;
        foreach (HeifItem item in items)
        {
            HeifItem propertyItem = item.PropertySource ?? item;
            int itemPropertyCount = GetPropertyCount(propertyItem);
            if (itemPropertyCount == 0)
            {
                continue;
            }

            BinaryPrimitives.WriteUInt16BigEndian(buffer[bytesWritten..], (ushort)item.Id);
            bytesWritten += 2;

            buffer[bytesWritten++] = (byte)itemPropertyCount;
            ushort propertyIndex = propertyItem.FirstPropertyIndex;
            if (propertyItem.Extent != default)
            {
                WritePropertyAssociation(buffer, ref bytesWritten, propertyIndex++, largePropertyIndex, false);
            }

            if (propertyItem.ChannelBitDepths is not null || propertyItem.UniformChannelBitDepth is not null)
            {
                WritePropertyAssociation(buffer, ref bytesWritten, propertyIndex++, largePropertyIndex, false);
            }

            if (propertyItem.Av1CodecConfiguration is not null)
            {
                WritePropertyAssociation(buffer, ref bytesWritten, propertyIndex++, largePropertyIndex, true);
            }

            if (propertyItem.AuxiliaryType is not null)
            {
                WritePropertyAssociation(buffer, ref bytesWritten, propertyIndex++, largePropertyIndex, false);
            }

            if (propertyItem.IccProfile is not null)
            {
                WritePropertyAssociation(buffer, ref bytesWritten, propertyIndex++, largePropertyIndex, false);
            }

            if (propertyItem.CicpProfile is not null)
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
            ushort association = essential ? (ushort)(propertyIndex | EssentialPropertyFlag) : propertyIndex;
            BinaryPrimitives.WriteUInt16BigEndian(buffer[offset..], association);
            offset += 2;
        }
        else
        {
            buffer[offset++] = essential ? (byte)(propertyIndex | CompactEssentialPropertyFlag) : (byte)propertyIndex;
        }
    }

    /// <summary>
    /// Writes the encoded precision of each image channel.
    /// </summary>
    /// <param name="memory">The preallocated metadata buffer.</param>
    /// <param name="memoryOffset">The destination offset within the property container.</param>
    /// <param name="channelBitDepths">The encoded precision of each channel.</param>
    /// <returns>The complete pixel-information-box length.</returns>
    private static int WritePixelInformationPropertyBox(
        Span<byte> memory,
        int memoryOffset,
        ReadOnlySpan<byte> channelBitDepths)
    {
        Span<byte> buffer = memory.Slice(memoryOffset, PixelInformationPropertyBoxFixedLength + channelBitDepths.Length);
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
    /// <param name="memory">The preallocated metadata buffer.</param>
    /// <param name="memoryOffset">The destination offset within the property container.</param>
    /// <param name="channelCount">The number of encoded image channels.</param>
    /// <param name="channelBitDepth">The common encoded precision.</param>
    /// <returns>The complete pixel-information-box length.</returns>
    private static int WritePixelInformationPropertyBox(
        Span<byte> memory,
        int memoryOffset,
        int channelCount,
        byte channelBitDepth)
    {
        Span<byte> buffer = memory.Slice(memoryOffset, PixelInformationPropertyBoxFixedLength + channelCount);
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
    /// <param name="memory">The preallocated metadata buffer.</param>
    /// <param name="memoryOffset">The destination offset within the property container.</param>
    /// <param name="configuration">The fixed image configuration.</param>
    /// <returns>The complete AV1 codec-configuration-box length.</returns>
    private static int WriteAv1CodecConfigurationPropertyBox(
        Span<byte> memory,
        int memoryOffset,
        Av1CodecConfiguration configuration)
    {
        Span<byte> buffer = memory.Slice(memoryOffset, Av1CodecConfigurationPropertyBoxLength);
        int bytesWritten = WriteBoxHeader(buffer, Heif4CharCode.Av1C);
        configuration.WriteFixedHeader(buffer.Slice(bytesWritten, Av1CodecConfiguration.FixedHeaderSize));
        bytesWritten += Av1CodecConfiguration.FixedHeaderSize;

        BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)bytesWritten);
        return bytesWritten;
    }

    /// <summary>
    /// Writes the registered type of an auxiliary image item.
    /// </summary>
    /// <param name="memory">The preallocated metadata buffer.</param>
    /// <param name="memoryOffset">The destination offset within the property container.</param>
    /// <param name="auxiliaryType">The null-terminated registered auxiliary type.</param>
    /// <returns>The complete auxiliary-type-box length.</returns>
    private static int WriteAuxiliaryTypePropertyBox(
        Span<byte> memory,
        int memoryOffset,
        string auxiliaryType)
    {
        int auxiliaryTypeLength = Encoding.UTF8.GetByteCount(auxiliaryType);
        Span<byte> buffer = memory.Slice(memoryOffset, AuxiliaryTypePropertyBoxFixedLength + auxiliaryTypeLength);
        int bytesWritten = WriteBoxHeader(buffer, Heif4CharCode.AuxC, 0, 0);
        bytesWritten += Encoding.UTF8.GetBytes(auxiliaryType, buffer[bytesWritten..]);
        buffer[bytesWritten++] = 0;

        BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)bytesWritten);
        return bytesWritten;
    }

    /// <summary>
    /// Writes an unrestricted ICC color profile for a color image item.
    /// </summary>
    /// <param name="memory">The preallocated metadata buffer.</param>
    /// <param name="memoryOffset">The destination offset within the property container.</param>
    /// <param name="profileData">The serialized ICC profile to write.</param>
    /// <returns>The complete color-information-box length.</returns>
    private static int WriteIccColorInformationPropertyBox(
        Span<byte> memory,
        int memoryOffset,
        ReadOnlyMemory<byte> profileData)
    {
        Span<byte> buffer = memory.Slice(memoryOffset, IccColorInformationPropertyBoxFixedLength + profileData.Length);
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
    /// <param name="memory">The preallocated metadata buffer.</param>
    /// <param name="memoryOffset">The destination offset within the property container.</param>
    /// <param name="profile">The color description to write.</param>
    /// <returns>The complete color-information-box length.</returns>
    private static int WriteColorInformationPropertyBox(
        Span<byte> memory,
        int memoryOffset,
        CicpProfile profile)
    {
        Span<byte> buffer = memory.Slice(memoryOffset, CicpColorInformationPropertyBoxLength);
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
    /// <param name="memory">The preallocated metadata buffer.</param>
    /// <param name="memoryOffset">The destination offset within the property container.</param>
    /// <param name="item">The item whose extent is written.</param>
    /// <returns>The complete image-spatial-extents-box length.</returns>
    private static int WriteSpatialExtentPropertyBox(Span<byte> memory, int memoryOffset, HeifItem item)
    {
        Span<byte> buffer = memory.Slice(memoryOffset, SpatialExtentPropertyBoxLength);
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
    /// <param name="memory">The preallocated metadata buffer.</param>
    /// <param name="memoryOffset">The destination offset within the metadata box.</param>
    /// <param name="items">The items and relative payload extents to locate.</param>
    /// <param name="mediaDataOffset">The absolute stream offset of the media-data payload.</param>
    /// <returns>The complete item-location-box length.</returns>
    private static int WriteItemLocationBox(Span<byte> memory, int memoryOffset, List<HeifItem> items, long mediaDataOffset)
    {
        Span<byte> buffer = memory.Slice(memoryOffset, GetItemLocationBoxLength(items));
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
        return Av1QuantizationLookup.GetQIndex(quantizer);
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
        Av1EncodingSettings settings = this.ResolveAv1Encoding(image);
        if (image.Width > Av1Constants.MaxFrameDimension || image.Height > Av1Constants.MaxFrameDimension)
        {
            this.CompressAv1GridPixels(image, stream, settings, items, links, cancellationToken);
            return;
        }

        Av1ImageItemEncoding encoding = this.CompressAv1ImageItem(
            image.Frames.RootFrame,
            stream,
            settings,
            cancellationToken);

        this.WriteAv1ImageItems(image, stream, settings, encoding, items, links);
    }

    /// <summary>
    /// Encodes a still image as independently coded AV1 cells referenced by one derived grid item.
    /// </summary>
    private void CompressAv1GridPixels<TPixel>(
        Image<TPixel> image,
        ChunkedMemoryStream stream,
        Av1EncodingSettings settings,
        List<HeifItem> items,
        List<HeifItemLink> links,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        bool isSubsampledX = !settings.ColorConfig.IsMonochrome && settings.ColorConfig.SubSamplingX;
        bool isSubsampledY = !settings.ColorConfig.IsMonochrome && settings.ColorConfig.SubSamplingY;
        int columns = GetGridCellCount(image.Width, Av1Constants.MaxFrameDimension);
        int rows = GetGridCellCount(image.Height, Av1Constants.MaxFrameDimension);
        if (columns > MaximumGridAxisCellCount || rows > MaximumGridAxisCellCount)
        {
            throw new NotSupportedException(
                $"AVIF grids support at most {MaximumGridAxisCellCount} columns and rows.");
        }

        int cellWidth = GetGridCellSize(image.Width, columns, isSubsampledX);
        int cellHeight = GetGridCellSize(image.Height, rows, isSubsampledY);
        Size encodedCellSize = new(
            Math.Max(cellWidth, MinimumGridCellDimension),
            Math.Max(cellHeight, MinimumGridCellDimension));

        long cellCount = (long)columns * rows;
        long itemCount = 1 + cellCount;
        if (settings.HasAlpha)
        {
            itemCount += 1 + cellCount;
        }

        if (!this.encoder.SkipMetadata)
        {
            itemCount += image.Metadata.ExifProfile is null ? 0 : 1;
            itemCount += image.Metadata.XmpProfile is null ? 0 : 1;
        }

        if (itemCount > ushort.MaxValue)
        {
            throw new NotSupportedException(
                $"The encoded AVIF grid requires {itemCount} items, but this container supports at most {ushort.MaxValue}.");
        }

        byte channelBitDepth = (byte)settings.BitDepth;
        long descriptorOffset = stream.Length;
        int descriptorLength = WriteGridDescriptor(stream, rows, columns, image.Size);
        HeifItem colorGrid = new(Heif4CharCode.Grid, 1)
        {
            ChannelCount = settings.ColorConfig.IsMonochrome ? 1 : 3,
            UniformChannelBitDepth = channelBitDepth,
            BitsPerPixel = channelBitDepth * (settings.ColorConfig.IsMonochrome ? 1 : 3),
            IccProfile = this.encoder.SkipMetadata ? null : image.Metadata.IccProfile,
            CicpProfile = settings.ColorProfile
        };

        colorGrid.DataLocations.Add(
            new HeifLocation(
                HeifLocationOffsetOrigin.FileOffset,
                0L,
                descriptorOffset,
                descriptorLength));

        colorGrid.SetExtent(image.Size);
        items.Add(colorGrid);
        HeifItemLink colorGridLink = new(Heif4CharCode.Dimg, colorGrid.Id);
        links.Add(colorGridLink);
        HeifItem? colorPropertySource = null;
        ImageFrame<TPixel> rootFrame = image.Frames.RootFrame;
        for (int row = 0; row < rows; row++)
        {
            int y = row * cellHeight;
            int height = Math.Min(cellHeight, image.Height - y);
            for (int column = 0; column < columns; column++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int x = column * cellWidth;
                int width = Math.Min(cellWidth, image.Width - x);
                Rectangle sourceRectangle = new(x, y, width, height);
                long colorOffset = stream.Length;
                ObuSequenceHeader colorHeader = Av1FrameEncoder.EncodeGridCell(
                    this.configuration,
                    rootFrame,
                    sourceRectangle,
                    encodedCellSize,
                    stream,
                    settings.ColorConfig,
                    settings.ColorQIndex,
                    this.encoder.Effort);

                long colorLength = stream.Length - colorOffset;
                HeifItem colorCell = new(Heif4CharCode.Av01, (uint)items.Count + 1)
                {
                    IsHidden = true,
                    ChannelCount = colorGrid.ChannelCount,
                    UniformChannelBitDepth = channelBitDepth,
                    BitsPerPixel = colorGrid.BitsPerPixel,
                    Av1CodecConfiguration = new Av1CodecConfiguration(colorHeader),
                    IccProfile = colorGrid.IccProfile,
                    CicpProfile = settings.ColorProfile
                };

                colorCell.DataLocations.Add(
                    new HeifLocation(
                        HeifLocationOffsetOrigin.FileOffset,
                        0L,
                        colorOffset,
                        colorLength));

                colorCell.SetExtent(encodedCellSize);
                ShareGridCellProperties(colorCell, ref colorPropertySource);

                items.Add(colorCell);
                colorGridLink.DestinationIds.Add(colorCell.Id);
            }
        }

        if (settings.HasAlpha)
        {
            descriptorOffset = stream.Length;
            descriptorLength = WriteGridDescriptor(stream, rows, columns, image.Size);
            HeifItem alphaGrid = new(Heif4CharCode.Grid, (uint)items.Count + 1)
            {
                ChannelCount = 1,
                UniformChannelBitDepth = channelBitDepth,
                BitsPerPixel = channelBitDepth,
                AuxiliaryType = HeifConstants.AlphaAuxiliaryType
            };

            alphaGrid.DataLocations.Add(
                new HeifLocation(
                    HeifLocationOffsetOrigin.FileOffset,
                    0L,
                    descriptorOffset,
                    descriptorLength));

            alphaGrid.SetExtent(image.Size);
            items.Add(alphaGrid);
            HeifItemLink alphaGridLink = new(Heif4CharCode.Dimg, alphaGrid.Id);
            links.Add(alphaGridLink);
            HeifItemLink alphaLink = new(Heif4CharCode.Auxl, alphaGrid.Id);
            alphaLink.DestinationIds.Add(colorGrid.Id);
            links.Add(alphaLink);
            HeifItem? alphaPropertySource = null;
            for (int row = 0; row < rows; row++)
            {
                int y = row * cellHeight;
                int height = Math.Min(cellHeight, image.Height - y);
                for (int column = 0; column < columns; column++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int x = column * cellWidth;
                    int width = Math.Min(cellWidth, image.Width - x);
                    Rectangle sourceRectangle = new(x, y, width, height);
                    long alphaOffset = stream.Length;
                    ObuSequenceHeader alphaHeader = Av1FrameEncoder.EncodeAlphaGridCell(
                        this.configuration,
                        rootFrame,
                        sourceRectangle,
                        encodedCellSize,
                        stream,
                        settings.AlphaConfig,
                        settings.AlphaQIndex,
                        this.encoder.Effort);

                    long alphaLength = stream.Length - alphaOffset;
                    HeifItem alphaCell = new(Heif4CharCode.Av01, (uint)items.Count + 1)
                    {
                        IsHidden = true,
                        ChannelCount = 1,
                        UniformChannelBitDepth = channelBitDepth,
                        BitsPerPixel = channelBitDepth,
                        Av1CodecConfiguration = new Av1CodecConfiguration(alphaHeader),
                        AuxiliaryType = HeifConstants.AlphaAuxiliaryType
                    };

                    alphaCell.DataLocations.Add(
                        new HeifLocation(
                            HeifLocationOffsetOrigin.FileOffset,
                            0L,
                            alphaOffset,
                            alphaLength));

                    alphaCell.SetExtent(encodedCellSize);
                    ShareGridCellProperties(alphaCell, ref alphaPropertySource);

                    items.Add(alphaCell);
                    alphaGridLink.DestinationIds.Add(alphaCell.Id);
                }
            }
        }

        this.WriteMetadataItems(image, stream, colorGrid, items, links);
    }

    /// <summary>
    /// Gets the minimum number of independently coded cells needed along one grid axis.
    /// </summary>
    /// <param name="dimension">The complete output dimension along the axis.</param>
    /// <param name="maximumCellDimension">The largest permitted nominal cell dimension.</param>
    private static int GetGridCellCount(int dimension, int maximumCellDimension)
        => (int)(((long)dimension + maximumCellDimension - 1) / maximumCellDimension);

    /// <summary>
    /// Gets the nominal cell size while preserving chroma alignment for every non-edge cell.
    /// </summary>
    private static int GetGridCellSize(int dimension, int cellCount, bool isSubsampled)
    {
        int cellSize = (int)(((long)dimension + cellCount - 1) / cellCount);
        if (isSubsampled && (cellSize & 1) != 0)
        {
            cellSize++;
        }

        return cellSize;
    }

    /// <summary>
    /// Writes the fixed grid item payload and returns its exact length.
    /// </summary>
    private static int WriteGridDescriptor(Stream stream, int rows, int columns, Size outputSize)
    {
        bool usesLargeDimensions = outputSize.Width > ushort.MaxValue || outputSize.Height > ushort.MaxValue;
        Span<byte> descriptor = stackalloc byte[LongGridDescriptorLength];
        int descriptorLength = 0;
        descriptor[descriptorLength++] = GridDescriptorVersion;
        descriptor[descriptorLength++] = usesLargeDimensions ? LargeGridDimensionsFlag : (byte)0;
        descriptor[descriptorLength++] = (byte)(rows - 1);
        descriptor[descriptorLength++] = (byte)(columns - 1);
        if (usesLargeDimensions)
        {
            BinaryPrimitives.WriteUInt32BigEndian(descriptor[descriptorLength..], (uint)outputSize.Width);
            descriptorLength += sizeof(uint);
            BinaryPrimitives.WriteUInt32BigEndian(descriptor[descriptorLength..], (uint)outputSize.Height);
            descriptorLength += sizeof(uint);
        }
        else
        {
            BinaryPrimitives.WriteUInt16BigEndian(descriptor[descriptorLength..], (ushort)outputSize.Width);
            descriptorLength += sizeof(ushort);
            BinaryPrimitives.WriteUInt16BigEndian(descriptor[descriptorLength..], (ushort)outputSize.Height);
            descriptorLength += sizeof(ushort);
        }

        stream.Write(descriptor[..descriptorLength]);
        return descriptorLength;
    }

    /// <summary>
    /// Reuses the common property set emitted for the first cell in one grid plane.
    /// </summary>
    private static void ShareGridCellProperties(HeifItem item, ref HeifItem? source)
    {
        if (source is null)
        {
            source = item;
            return;
        }

        // Every cell in one plane is coded to the same extent and configuration so current AVIF readers can
        // share one property set. Only the source rectangle differs for cells clipped by the output canvas.
        item.PropertySource = source;
    }

    /// <summary>
    /// Encodes one frame as the color and optional alpha payloads used by a primary AV1 image item.
    /// </summary>
    private Av1ImageItemEncoding CompressAv1ImageItem<TPixel>(
        ImageFrame<TPixel> frame,
        ChunkedMemoryStream stream,
        Av1EncodingSettings settings,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        cancellationToken.ThrowIfCancellationRequested();
        long colorOffset = stream.Length;
        ObuSequenceHeader colorHeader = Av1FrameEncoder.Encode(
            this.configuration,
            frame,
            stream,
            settings.ColorConfig,
            settings.ColorQIndex,
            this.encoder.Effort);

        long colorLength = stream.Length - colorOffset;
        Av1CodecConfiguration? alphaConfiguration = null;
        long alphaOffset = 0;
        long alphaLength = 0;

        if (settings.HasAlpha)
        {
            cancellationToken.ThrowIfCancellationRequested();
            alphaOffset = stream.Length;
            ObuSequenceHeader alphaHeader = Av1FrameEncoder.EncodeAlpha(
                this.configuration,
                frame,
                stream,
                settings.AlphaConfig,
                settings.AlphaQIndex,
                this.encoder.Effort);

            alphaLength = stream.Length - alphaOffset;
            alphaConfiguration = new Av1CodecConfiguration(alphaHeader);
        }

        return new Av1ImageItemEncoding(
            new Av1CodecConfiguration(colorHeader),
            colorOffset,
            colorLength,
            alphaConfiguration,
            alphaOffset,
            alphaLength);
    }

    /// <summary>
    /// Declares a primary AV1 image item over existing payload extents and appends its associated metadata payloads.
    /// </summary>
    private void WriteAv1ImageItems<TPixel>(
        Image<TPixel> image,
        ChunkedMemoryStream stream,
        Av1EncodingSettings settings,
        Av1ImageItemEncoding encoding,
        List<HeifItem> items,
        List<HeifItemLink> links)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        byte channelBitDepth = (byte)settings.BitDepth;
        HeifItem colorItem = new(Heif4CharCode.Av01, 1)
        {
            ChannelCount = settings.ColorConfig.IsMonochrome ? 1 : 3,
            UniformChannelBitDepth = channelBitDepth,
            BitsPerPixel = channelBitDepth * (settings.ColorConfig.IsMonochrome ? 1 : 3),
            Av1CodecConfiguration = encoding.ColorConfiguration,
            IccProfile = this.encoder.SkipMetadata ? null : image.Metadata.IccProfile,
            CicpProfile = settings.ColorProfile
        };

        colorItem.DataLocations.Add(
            new HeifLocation(
                HeifLocationOffsetOrigin.FileOffset,
                0L,
                encoding.ColorOffset,
                encoding.ColorLength));

        colorItem.SetExtent(image.Size);
        items.Add(colorItem);

        Av1CodecConfiguration? alphaConfiguration = encoding.AlphaConfiguration;
        if (alphaConfiguration is not null)
        {
            HeifItem alphaItem = new(Heif4CharCode.Av01, 2)
            {
                ChannelCount = 1,
                UniformChannelBitDepth = channelBitDepth,
                BitsPerPixel = channelBitDepth,
                Av1CodecConfiguration = alphaConfiguration,
                AuxiliaryType = HeifConstants.AlphaAuxiliaryType
            };

            alphaItem.DataLocations.Add(
                new HeifLocation(
                    HeifLocationOffsetOrigin.FileOffset,
                    0L,
                    encoding.AlphaOffset,
                    encoding.AlphaLength));

            alphaItem.SetExtent(image.Size);
            items.Add(alphaItem);
            HeifItemLink alphaLink = new(Heif4CharCode.Auxl, alphaItem.Id);
            alphaLink.DestinationIds.Add(colorItem.Id);
            links.Add(alphaLink);
        }

        this.WriteMetadataItems(image, stream, colorItem, items, links);
    }

    /// <summary>
    /// Appends Exif and XMP payload items associated with the primary presentation item.
    /// </summary>
    private void WriteMetadataItems<TPixel>(
        Image<TPixel> image,
        ChunkedMemoryStream stream,
        HeifItem primaryItem,
        List<HeifItem> items,
        List<HeifItemLink> links)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (this.encoder.SkipMetadata)
        {
            return;
        }

        byte[]? exifData = GetExifData(image.Metadata, out uint tiffHeaderOffset);
        if (exifData is not null)
        {
            long exifOffset = stream.Length;
            Span<byte> offsetBuffer = stackalloc byte[sizeof(uint)];
            BinaryPrimitives.WriteUInt32BigEndian(offsetBuffer, tiffHeaderOffset);
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
                    sizeof(uint) + (long)exifData.Length));

            items.Add(exifItem);
            HeifItemLink exifLink = new(Heif4CharCode.Cdsc, exifItem.Id);
            exifLink.DestinationIds.Add(primaryItem.Id);
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
            xmpLink.DestinationIds.Add(primaryItem.Id);
            links.Add(xmpLink);
        }
    }

    /// <summary>
    /// Materializes the caller's Exif profile once and locates the TIFF header addressed by HEIF's four-byte prefix.
    /// </summary>
    /// <param name="metadata">The source image metadata.</param>
    /// <param name="tiffHeaderOffset">The byte offset of the TIFF header within the returned profile.</param>
    /// <returns>The serialized profile, or <see langword="null"/> when the source has no Exif payload.</returns>
    private static byte[]? GetExifData(ImageMetadata metadata, out uint tiffHeaderOffset)
    {
        byte[]? exifData = metadata.ExifProfile?.ToByteArray();
        if (exifData is null || exifData.Length == 0)
        {
            tiffHeaderOffset = 0;
            return null;
        }

        // A directly supplied profile can retain the optional Exif identifier before its TIFF byte-order marker.
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
                tiffHeaderOffset = (uint)i;
                return exifData;
            }
        }

        throw new ImageFormatException("The Exif profile does not contain a TIFF header.");
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

    /// <summary>
    /// Describes the already-written color and optional alpha extents backing one AV1 image item.
    /// </summary>
    private readonly struct Av1ImageItemEncoding
    {
        public Av1ImageItemEncoding(
            Av1CodecConfiguration colorConfiguration,
            long colorOffset,
            long colorLength,
            Av1CodecConfiguration? alphaConfiguration,
            long alphaOffset,
            long alphaLength)
        {
            this.ColorConfiguration = colorConfiguration;
            this.ColorOffset = colorOffset;
            this.ColorLength = colorLength;
            this.AlphaConfiguration = alphaConfiguration;
            this.AlphaOffset = alphaOffset;
            this.AlphaLength = alphaLength;
        }

        public Av1CodecConfiguration ColorConfiguration { get; }

        public long ColorOffset { get; }

        public long ColorLength { get; }

        public Av1CodecConfiguration? AlphaConfiguration { get; }

        public long AlphaOffset { get; }

        public long AlphaLength { get; }
    }
}
