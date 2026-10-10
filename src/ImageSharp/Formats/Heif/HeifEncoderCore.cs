// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
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

    /// <summary>
    /// The length of a layered image index property before its three layer sizes: the box header and the flags byte.
    /// </summary>
    private const int LayeredImageIndexPropertyBoxFixedLength = BasicBoxHeaderLength + 1;
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
    /// Gets the external quantizer for each quality from 0 to 100 with the image tune. The table is a piecewise linear
    /// curve. It corrects for the different bit rate of the image tune at the same quantizer.
    /// </summary>
    private static ReadOnlySpan<byte> ImageTuneQualityToQuantizer =>
    [
        63, 63, 63, 62, 62, 62, 61, 61, 60, 60,
        59, 59, 58, 58, 57, 57, 56, 56, 55, 55,
        54, 54, 53, 53, 52, 52, 51, 51, 50, 50,
        49, 49, 48, 48, 47, 46, 46, 45, 45, 44,
        43, 43, 42, 42, 41, 40, 40, 39, 39, 38,
        37, 37, 36, 36, 35, 34, 33, 33, 32, 31,
        30, 30, 29, 28, 27, 27, 26, 25, 24, 24,
        23, 22, 21, 21, 20, 19, 18, 18, 17, 16,
        15, 15, 14, 13, 12, 12, 11, 10, 9, 9,
        8, 7, 6, 6, 5, 4, 3, 3, 2, 1,
        0
    ];

    /// <summary>
    /// Encodes the image to the stream. An image with more than one frame becomes an image sequence. An image with
    /// one frame becomes a still image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    /// <param name="image">The <see cref="Image{TPixel}"/> to encode.</param>
    /// <param name="stream">The <see cref="Stream"/> to encode the image data to.</param>
    /// <param name="cancellationToken">The token to request cancellation.</param>
    public void Encode<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Guard.NotNull(image, nameof(image));
        Guard.NotNull(stream, nameof(stream));

        if (image.Frames.Count > 1 && (image.Width > ushort.MaxValue || image.Height > ushort.MaxValue))
        {
            throw new NotSupportedException("AV1 image-sequence dimensions cannot exceed 65535 pixels.");
        }

        IReadOnlyList<HeifLayer>? layers = this.encoder.Layers;
        if (layers is not null)
        {
            // Layers are not supported for image sequences, for lossless coding, or for an image that needs a grid.
            if (image.Frames.Count > 1)
            {
                throw new NotSupportedException("A layered image must have one frame.");
            }

            if (this.encoder.Lossless)
            {
                throw new NotSupportedException("A layered image cannot be lossless.");
            }

            if (image.Width > Av1Constants.MaxFrameDimension || image.Height > Av1Constants.MaxFrameDimension)
            {
                throw new NotSupportedException($"A layered image cannot be wider or taller than {Av1Constants.MaxFrameDimension} pixels.");
            }
        }

        using ChunkedMemoryStream compressedPixels = new(this.configuration.MemoryAllocator);
        if (image.Frames.Count > 1)
        {
            Av1EncodingSettings settings = this.ResolveAv1Encoding(image, allIntra: false, layers: null, cancellationToken);
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
                    false,
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

                // The primary image item and the first track sample describe the same sync sample. The item uses the
                // extent of the sample, so the root frame is not encoded or stored twice.
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
            this.WriteMediaDataBox(compressedPixels, stream, cancellationToken);
            stream.Flush();
            return;
        }

        List<HeifItem> items = new();
        List<HeifItemLink> links = new();
        this.CompressAv1Pixels(image, compressedPixels, items, links, cancellationToken);

        long metadataBoxOffset = this.WriteFileTypeBox(stream);
        this.WriteMetadataBox(items, links, metadataBoxOffset, 0, stream);
        this.WriteMediaDataBox(compressedPixels, stream, cancellationToken);
        stream.Flush();
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

        // The flags fill the low three bytes of the big-endian word. Then the version replaces the first byte.
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
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)Heif4CharCode.Avif);
        bytesWritten += 4;
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], 0);
        bytesWritten += 4;

        // A still AVIF is also a MIAF image collection, so advertise both structural brands with the codec brand.
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)Heif4CharCode.Avif);
        bytesWritten += 4;
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)Heif4CharCode.Mif1);
        bytesWritten += 4;
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)Heif4CharCode.Miaf);
        bytesWritten += 4;

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
            // The iref box is optional. Without a link it has no content, so the encoder leaves it out.
            bytesWritten += WriteItemReferenceBox(memory, bytesWritten, links);
        }

        bytesWritten += WriteItemPropertiesBox(memory, bytesWritten, items);

        // The iloc box needs the absolute position of the mdat payload. That position depends on the final meta length.
        // Write iloc once to fix its size, calculate the mdat position, then write the same bytes again.
        int itemLocationOffset = bytesWritten;
        bytesWritten += WriteItemLocationBox(memory, bytesWritten, items, 0);

        // The mdat payload immediately follows the completed meta box and its own eight-byte header.
        long mediaDataOffset = checked(metadataBoxOffset + bytesWritten + followingBoxLength + BasicBoxHeaderLength);
        WriteItemLocationBox(memory, itemLocationOffset, items, mediaDataOffset);

        buffer = memory[..bytesWritten];
        BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)bytesWritten);
        stream.Write(buffer);
    }

    /// <summary>
    /// Gets the exact length of the metadata box that <see cref="WriteMetadataBox"/> writes.
    /// </summary>
    /// <param name="items">The declared image and metadata items.</param>
    /// <param name="links">The typed relationships between items.</param>
    /// <returns>The length of the metadata box in bytes.</returns>
    private static int GetMetadataBoxLength(List<HeifItem> items, List<HeifItemLink> links)
    {
        // The length includes every string, profile, link, property and extent. Thus the writer fills one buffer and
        // never grows or copies it.
        return checked(
            FullBoxHeaderLength
            + HandlerBoxLength
            + PrimaryItemBoxLength
            + GetItemInformationBoxLength(items)
            + (links.Count == 0 ? 0 : GetItemReferenceBoxLength(links))
            + GetItemPropertiesBoxLength(items)
            + GetItemLocationBoxLength(items));
    }

    /// <summary>
    /// Gets the exact length of the item information box, with one version 2 entry for each item.
    /// </summary>
    /// <param name="items">The items to declare.</param>
    /// <returns>The length of the item information box in bytes.</returns>
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

    /// <summary>
    /// Gets the exact length of the item reference box with 16-bit item identifiers.
    /// </summary>
    /// <param name="links">The relationships to write.</param>
    /// <returns>The length of the item reference box in bytes.</returns>
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
            propertyBytes += item.Av1LayeredImageIndex is Av1LayeredImageIndex layeredImageIndex
                ? GetLayeredImageIndexPropertyBoxLength(layeredImageIndex)
                : 0;
        }

        int associationSize = propertyCount > MaximumCompactPropertyIndex ? sizeof(ushort) : sizeof(byte);
        long length = ItemPropertiesBoxFixedLength
            + propertyBytes
            + (associationItemCount * PropertyAssociationEntryFixedLength)
            + (associationPropertyCount * associationSize);

        return checked((int)length);
    }

    /// <summary>
    /// Gets the exact length of the item location box, with one entry for each item and one extent for each data location.
    /// </summary>
    /// <param name="items">The items to locate.</param>
    /// <returns>The length of the item location box in bytes.</returns>
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
    /// Writes the item properties box. The property container holds the properties of each item that has no
    /// property source. The association box links each item to its properties by one-based index.
    /// </summary>
    /// <param name="memory">The preallocated metadata buffer.</param>
    /// <param name="memoryOffset">The destination offset within the metadata box.</param>
    /// <param name="items">The items whose properties are written and associated.</param>
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

            if (item.Av1LayeredImageIndex is Av1LayeredImageIndex layeredImageIndex)
            {
                bytesWritten += WriteLayeredImageIndexPropertyBox(memory, memoryOffset + bytesWritten, layeredImageIndex);
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

            // A reader can ignore the layer index and decode every layer, so the association is not essential.
            if (propertyItem.Av1LayeredImageIndex is not null)
            {
                WritePropertyAssociation(buffer, ref bytesWritten, propertyIndex++, largePropertyIndex, false);
            }
        }

        BinaryPrimitives.WriteUInt32BigEndian(buffer[ipmaLengthOffset..], (uint)(bytesWritten - ipmaLengthOffset));

        // Write the size of the enclosing iprp box.
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
        count += item.Av1LayeredImageIndex is not null ? 1 : 0;
        return count;
    }

    /// <summary>
    /// Gets the length of a layered image index property: 16-bit layer sizes, or 32-bit sizes when a size needs them.
    /// </summary>
    /// <param name="index">The layer index.</param>
    /// <returns>The complete layered-image-index-box length.</returns>
    private static int GetLayeredImageIndexPropertyBoxLength(Av1LayeredImageIndex index)
        => LayeredImageIndexPropertyBoxFixedLength + (3 * (UsesLargeLayerSizes(index) ? sizeof(uint) : sizeof(ushort)));

    /// <summary>
    /// Returns whether a layer size of the index does not fit in 16 bits. Then the a1lx box sets large_size.
    /// </summary>
    /// <param name="index">The layer index.</param>
    /// <returns><see langword="true"/> when the index stores 32-bit sizes.</returns>
    private static bool UsesLargeLayerSizes(Av1LayeredImageIndex index)
        => index.FirstLayerSize > ushort.MaxValue || index.SecondLayerSize > ushort.MaxValue || index.ThirdLayerSize > ushort.MaxValue;

    /// <summary>
    /// Writes the size of each layer but the last of a layered AV1 image item.
    /// </summary>
    /// <param name="memory">The preallocated metadata buffer.</param>
    /// <param name="memoryOffset">The destination offset within the property container.</param>
    /// <param name="index">The layer index to write.</param>
    /// <returns>The complete layered-image-index-box length.</returns>
    private static int WriteLayeredImageIndexPropertyBox(Span<byte> memory, int memoryOffset, Av1LayeredImageIndex index)
    {
        bool largeSize = UsesLargeLayerSizes(index);
        Span<byte> buffer = memory.Slice(memoryOffset, GetLayeredImageIndexPropertyBoxLength(index));
        int bytesWritten = WriteBoxHeader(buffer, Heif4CharCode.A1lx);

        // Seven reserved bits, then large_size.
        buffer[bytesWritten++] = largeSize ? (byte)1 : (byte)0;
        foreach (uint size in (ReadOnlySpan<uint>)[index.FirstLayerSize, index.SecondLayerSize, index.ThirdLayerSize])
        {
            if (largeSize)
            {
                BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], size);
                bytesWritten += sizeof(uint);
            }
            else
            {
                BinaryPrimitives.WriteUInt16BigEndian(buffer[bytesWritten..], (ushort)size);
                bytesWritten += sizeof(ushort);
            }
        }

        BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)bytesWritten);
        return bytesWritten;
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
    /// Writes a version 1 item location box with an absolute file offset for every item extent.
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

        // The first byte selects eight-byte offsets and four-byte lengths. The second byte selects no base offset and
        // no extent index, because every extent is an absolute file offset into mdat.
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
                // Each location is relative to the mdat payload. Adding the payload position gives the file offset.
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
    /// Writes all coded payloads in a media data box.
    /// </summary>
    /// <param name="data">The stream that holds the coded item and sample payloads.</param>
    /// <param name="stream">The destination stream.</param>
    /// <param name="cancellationToken">The token that stops the write between buffers.</param>
    private void WriteMediaDataBox(ChunkedMemoryStream data, Stream stream, CancellationToken cancellationToken)
    {
        Span<byte> buf = stackalloc byte[12];
        int bytesWritten = WriteBoxHeader(buf, Heif4CharCode.Mdat);
        BinaryPrimitives.WriteUInt32BigEndian(buf, checked((uint)(data.Length + bytesWritten)));
        stream.Write(buf[..bytesWritten]);

        // The method copies the payload one buffer at a time and checks for cancellation after each read. Thus a
        // canceled encode also stops during the write.
        using IMemoryOwner<byte> bufferOwner = this.configuration.MemoryAllocator.Allocate<byte>(this.configuration.StreamProcessingBufferSize);
        Span<byte> buffer = bufferOwner.Memory.Span;
        data.Position = 0;
        int read;
        while ((read = data.Read(buffer)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            stream.Write(buffer[..read]);
        }
    }

    /// <summary>
    /// Maps a public lossy quality to an AV1 quantizer index. The quality goes to the external quantizer scale first.
    /// </summary>
    /// <param name="quality">The lossy quality in the inclusive range zero through one hundred.</param>
    /// <param name="imageTune">Whether the encoding uses the image tune, which has its own quality curve.</param>
    /// <returns>The AV1 quantizer index.</returns>
    public static int GetAv1QuantizerIndex(int quality, bool imageTune)
        => Av1QuantizationLookup.GetQIndex(GetAv1Quantizer(quality, imageTune));

    /// <summary>
    /// Maps a public lossy quality to the external quantizer scale of 0 to 63. Without the image tune the map is linear
    /// and rounds to the nearest step.
    /// </summary>
    /// <param name="quality">The lossy quality in the inclusive range zero through one hundred.</param>
    /// <param name="imageTune">Whether the encoding uses the image tune, which has its own quality curve.</param>
    /// <returns>The external quantizer.</returns>
    public static int GetAv1Quantizer(int quality, bool imageTune)
    {
        int quantizer = imageTune ? ImageTuneQualityToQuantizer[quality] : (((100 - quality) * 63) + 50) / 100;

        // External quantizer 0 maps to the lossless quantizer index. Quality 100 must stay lossy, so the minimum is 1.
        return Math.Max(quantizer, 1);
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
        if (this.encoder.Layers is IReadOnlyList<HeifLayer> layers)
        {
            Av1EncodingSettings layeredSettings = this.ResolveAv1Encoding(image, allIntra: false, layers, cancellationToken);
            Av1ImageItemEncoding layeredEncoding = this.CompressAv1LayeredImageItem(
                image.Frames.RootFrame,
                stream,
                layeredSettings,
                layers,
                cancellationToken);

            this.WriteAv1ImageItems(image, stream, layeredSettings, layeredEncoding, items, links);
            return;
        }

        Av1EncodingSettings settings = this.ResolveAv1Encoding(image, allIntra: true, layers: null, cancellationToken);
        if (image.Width > Av1Constants.MaxFrameDimension || image.Height > Av1Constants.MaxFrameDimension)
        {
            this.CompressAv1GridPixels(image, stream, settings, items, links, cancellationToken);
            return;
        }

        Av1ImageItemEncoding encoding = this.CompressAv1ImageItem(
            image.Frames.RootFrame,
            stream,
            settings,
            true,
            cancellationToken);

        this.WriteAv1ImageItems(image, stream, settings, encoding, items, links);
    }

    /// <summary>
    /// Encodes a still image as independently coded AV1 cells referenced by one derived grid item.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel format.</typeparam>
    /// <param name="image">The source image. The image is wider or taller than the largest AV1 frame.</param>
    /// <param name="stream">The shared destination for consecutive item payloads.</param>
    /// <param name="settings">The resolved encoding settings.</param>
    /// <param name="items">The destination item declarations.</param>
    /// <param name="links">The destination item relationships.</param>
    /// <param name="cancellationToken">The token used to cancel payload encoding.</param>
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

        // A still image writes no alpha grid when every alpha sample of every cell is opaque.
        bool hasAlpha = settings.HasAlpha &&
            !Av1FrameEncoder.IsGridAlphaOpaque(
                this.configuration,
                image.Frames.RootFrame,
                new Size(cellWidth, cellHeight),
                encodedCellSize,
                settings.AlphaConfig);

        long cellCount = (long)columns * rows;
        long itemCount = 1 + cellCount;
        if (hasAlpha)
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
                    settings.ColorOptions);

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

        if (hasAlpha)
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
                        settings.AlphaOptions);

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
    /// <returns>The number of cells along the axis.</returns>
    private static int GetGridCellCount(int dimension, int maximumCellDimension)
        => (int)(((long)dimension + maximumCellDimension - 1) / maximumCellDimension);

    /// <summary>
    /// Gets the nominal cell size along one grid axis. On a subsampled axis the size is even, so every cell but the
    /// last starts on a chroma sample.
    /// </summary>
    /// <param name="dimension">The complete output dimension along the axis.</param>
    /// <param name="cellCount">The number of cells along the axis.</param>
    /// <param name="isSubsampled">Whether the chroma is subsampled along the axis.</param>
    /// <returns>The nominal cell size.</returns>
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
    /// Writes the payload of a grid item. The payload uses 32-bit output dimensions only when a dimension does not fit
    /// in 16 bits.
    /// </summary>
    /// <param name="stream">The shared destination for consecutive item payloads.</param>
    /// <param name="rows">The number of cell rows, from 1 to 256.</param>
    /// <param name="columns">The number of cell columns, from 1 to 256.</param>
    /// <param name="outputSize">The size of the reconstructed image.</param>
    /// <returns>The length of the payload in bytes.</returns>
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
    /// Reuses the property set of the first cell of one grid plane for the other cells of that plane.
    /// </summary>
    /// <param name="item">The cell item.</param>
    /// <param name="source">The first cell of the plane, or <see langword="null"/> before the first cell. The first call sets it.</param>
    private static void ShareGridCellProperties(HeifItem item, ref HeifItem? source)
    {
        if (source is null)
        {
            source = item;
            return;
        }

        // Every cell of one plane has the same coded extent and configuration, so all cells can share one property set.
        // Only the source rectangle differs for the cells that the output canvas clips.
        item.PropertySource = source;
    }

    /// <summary>
    /// Encodes one frame as the color and optional alpha payloads used by a primary AV1 image item. A single image
    /// writes no alpha payload when every converted alpha sample is opaque. The primary image of a sequence keeps its
    /// alpha, because later samples can be transparent.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel format.</typeparam>
    /// <param name="frame">The frame to encode.</param>
    /// <param name="stream">The shared destination for consecutive item payloads.</param>
    /// <param name="settings">The resolved encoding settings.</param>
    /// <param name="singleImage">Whether the frame is a single image rather than the primary image of a sequence.</param>
    /// <param name="cancellationToken">The token used to cancel payload encoding.</param>
    /// <returns>The payload extents of the color and alpha.</returns>
    private Av1ImageItemEncoding CompressAv1ImageItem<TPixel>(
        ImageFrame<TPixel> frame,
        ChunkedMemoryStream stream,
        Av1EncodingSettings settings,
        bool singleImage,
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
            settings.ColorOptions);

        long colorLength = stream.Length - colorOffset;
        Av1CodecConfiguration? alphaConfiguration = null;
        long alphaOffset = 0;
        long alphaLength = 0;

        if (settings.HasAlpha)
        {
            cancellationToken.ThrowIfCancellationRequested();
            alphaOffset = stream.Length;
            ObuSequenceHeader alphaHeader;
            bool alphaWritten = true;
            if (singleImage)
            {
                alphaWritten = Av1FrameEncoder.TryEncodeSingleImageAlpha(
                    this.configuration,
                    frame,
                    stream,
                    settings.AlphaConfig,
                    settings.AlphaQIndex,
                    settings.AlphaOptions,
                    out alphaHeader);
            }
            else
            {
                alphaHeader = Av1FrameEncoder.EncodeAlpha(
                    this.configuration,
                    frame,
                    stream,
                    settings.AlphaConfig,
                    settings.AlphaQIndex,
                    settings.AlphaOptions);
            }

            if (alphaWritten)
            {
                alphaLength = stream.Length - alphaOffset;
                alphaConfiguration = new Av1CodecConfiguration(alphaHeader);
            }
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
    /// Encodes the layers of a layered still image as the color and optional alpha payloads of one AV1 image item. Each
    /// layer is one frame of a sequence, coded at the quality of the layer. The media data holds layer 1 of the alpha,
    /// then layer 1 of the color, then layer 2 of each, and so on. Thus a viewer can show each
    /// layer as soon as it arrives.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel format.</typeparam>
    /// <param name="frame">The frame to encode.</param>
    /// <param name="stream">The shared destination for consecutive item payloads.</param>
    /// <param name="settings">The resolved settings of the layered encoding.</param>
    /// <param name="layers">The layers, from the first to the last.</param>
    /// <param name="cancellationToken">The token used to cancel payload encoding.</param>
    /// <returns>The payload extents of each layer.</returns>
    private Av1ImageItemEncoding CompressAv1LayeredImageItem<TPixel>(
        ImageFrame<TPixel> frame,
        ChunkedMemoryStream stream,
        Av1EncodingSettings settings,
        IReadOnlyList<HeifLayer> layers,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // The default quality is 60. A layer without its own alpha quality uses the alpha quality of the encoder.
        int quality = this.encoder.Quality ?? 60;
        int alphaQuality = this.encoder.AlphaQuality ?? quality;
        int layerCount = layers.Count;
        ChunkedMemoryStream?[] colorLayers = new ChunkedMemoryStream?[layerCount];
        ChunkedMemoryStream?[] alphaLayers = new ChunkedMemoryStream?[layerCount];
        try
        {
            ObuSequenceHeader colorHeader = this.CompressAv1Layers(
                frame,
                settings.ColorConfig,
                settings.ColorOptions,
                settings.ColorQIndex,
                layers,
                colorLayers,
                static (layer, encoderQuality) => layer.Quality ?? encoderQuality,
                quality,
                encodeAlpha: false,
                cancellationToken);

            Av1CodecConfiguration? alphaConfiguration = null;
            if (settings.HasAlpha)
            {
                ObuSequenceHeader alphaHeader = this.CompressAv1Layers(
                    frame,
                    settings.AlphaConfig,
                    settings.AlphaOptions,
                    settings.AlphaQIndex,
                    layers,
                    alphaLayers,
                    static (layer, encoderQuality) => layer.AlphaQuality ?? encoderQuality,
                    alphaQuality,
                    encodeAlpha: true,
                    cancellationToken);

                alphaConfiguration = new Av1CodecConfiguration(alphaHeader);
            }

            // Each layer of the alpha comes before the same layer of the color.
            HeifLocation[] colorLocations = new HeifLocation[layerCount];
            HeifLocation[]? alphaLocations = settings.HasAlpha ? new HeifLocation[layerCount] : null;
            for (int layer = 0; layer < layerCount; layer++)
            {
                if (alphaLocations is not null)
                {
                    alphaLocations[layer] = AppendLayer(stream, alphaLayers[layer]!);
                }

                colorLocations[layer] = AppendLayer(stream, colorLayers[layer]!);
            }

            return new Av1ImageItemEncoding(
                new Av1CodecConfiguration(colorHeader),
                colorLocations,
                alphaConfiguration,
                alphaLocations);
        }
        finally
        {
            for (int layer = 0; layer < layerCount; layer++)
            {
                colorLayers[layer]?.Dispose();
                alphaLayers[layer]?.Dispose();
            }
        }
    }

    /// <summary>
    /// Copies one coded layer to the end of the shared payload.
    /// </summary>
    /// <param name="stream">The shared payload.</param>
    /// <param name="layer">The coded bytes of the layer.</param>
    /// <returns>The extent of the layer within the payload.</returns>
    private static HeifLocation AppendLayer(ChunkedMemoryStream stream, ChunkedMemoryStream layer)
    {
        long offset = stream.Length;
        layer.WriteTo(stream);
        return new HeifLocation(HeifLocationOffsetOrigin.FileOffset, 0L, offset, stream.Length - offset);
    }

    /// <summary>
    /// Codes every layer of one plane group of a layered image, color or alpha, with one sequence encoder. Each layer
    /// is coded into its own buffer.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel format.</typeparam>
    /// <param name="frame">The frame to encode.</param>
    /// <param name="colorConfig">The color configuration of the coded planes.</param>
    /// <param name="options">The codec options of the coded planes.</param>
    /// <param name="firstQIndex">The quantizer index of the first layer, which the encoder starts from.</param>
    /// <param name="layers">The layers, from the first to the last.</param>
    /// <param name="layerBuffers">Receives the coded bytes of each layer. The caller disposes each buffer.</param>
    /// <param name="getQuality">Returns the quality of a layer, given the layer and the quality of the encoder.</param>
    /// <param name="quality">The quality of the encoder for the coded planes.</param>
    /// <param name="encodeAlpha">Whether the coded planes are the alpha of the image.</param>
    /// <param name="cancellationToken">The token used to cancel payload encoding.</param>
    /// <returns>The sequence header of the coded layers.</returns>
    private ObuSequenceHeader CompressAv1Layers<TPixel>(
        ImageFrame<TPixel> frame,
        ObuColorConfig colorConfig,
        Av1EncoderOptions options,
        int firstQIndex,
        IReadOnlyList<HeifLayer> layers,
        ChunkedMemoryStream?[] layerBuffers,
        Func<HeifLayer, int, int> getQuality,
        int quality,
        bool encodeAlpha,
        CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Only the image tune has its own curve from quality to quantizer.
        bool imageTune = options.Tuning == Av1Tuning.Iq;
        Av1RateControlMode rateControlMode = options.RateControlMode;
        using Av1FrameEncoder.SequenceEncoder encoder = encodeAlpha
            ? Av1FrameEncoder.CreateAlphaSequenceEncoder(this.configuration, frame.Width, frame.Height, colorConfig, firstQIndex, options)
            : Av1FrameEncoder.CreateColorSequenceEncoder(this.configuration, frame.Width, frame.Height, colorConfig, firstQIndex, options);

        int previousQuality = -1;
        for (int layer = 0; layer < layers.Count; layer++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ChunkedMemoryStream buffer = new(this.configuration.MemoryAllocator);
            layerBuffers[layer] = buffer;
            int layerQuality = getQuality(layers[layer], quality);
            int quantizer = GetAv1Quantizer(layerQuality, imageTune);
            (int minimumQuantizer, int maximumQuantizer) = GetQuantizerRange(quantizer, rateControlMode);
            (int scaleNumerator, int scaleDenominator) = HeifLayer.GetFraction(layers[layer].Scale);

            // The encoder gets a new configuration only when the quality of a layer differs from the layer before it.
            encoder.EncodeLayer(
                frame,
                buffer,
                Av1QuantizationLookup.GetQIndex(quantizer),
                minimumQuantizer,
                maximumQuantizer,
                scaleNumerator,
                scaleDenominator,
                layerQuality != previousQuality);

            previousQuality = layerQuality;
        }

        return encoder.SequenceHeader;
    }

    /// <summary>
    /// Declares a primary AV1 image item over existing payload extents and appends its associated metadata payloads.
    /// An item of several extents is a layered item, and gets the size of each layer but the last as a layer index.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel format.</typeparam>
    /// <param name="image">The source image, which gives the item size and metadata.</param>
    /// <param name="stream">The shared payload, which receives the metadata payloads.</param>
    /// <param name="settings">The resolved settings of the encoding.</param>
    /// <param name="encoding">The codec configurations and payload extents of the color and alpha.</param>
    /// <param name="items">The destination item declarations.</param>
    /// <param name="links">The destination item relationships.</param>
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

        colorItem.DataLocations.AddRange(encoding.ColorLocations);
        colorItem.Av1LayeredImageIndex = GetLayeredImageIndex(encoding.ColorLocations);
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

            alphaItem.DataLocations.AddRange(encoding.AlphaLocations!);
            alphaItem.Av1LayeredImageIndex = GetLayeredImageIndex(encoding.AlphaLocations!);
            alphaItem.SetExtent(image.Size);
            items.Add(alphaItem);
            HeifItemLink alphaLink = new(Heif4CharCode.Auxl, alphaItem.Id);
            alphaLink.DestinationIds.Add(colorItem.Id);
            links.Add(alphaLink);
        }

        this.WriteMetadataItems(image, stream, colorItem, items, links);
    }

    /// <summary>
    /// Returns the layer index of a layered item. The first entries hold the size of each layer but the last. The other
    /// entries of the three are zero.
    /// </summary>
    /// <param name="layers">The payload extent of each layer.</param>
    /// <returns>The layer index, or <see langword="null"/> for an item of one extent.</returns>
    private static Av1LayeredImageIndex? GetLayeredImageIndex(HeifLocation[] layers)
    {
        if (layers.Length < 2)
        {
            return null;
        }

        return new Av1LayeredImageIndex(
            checked((uint)layers[0].Length),
            layers.Length > 2 ? checked((uint)layers[1].Length) : 0,
            layers.Length > 3 ? checked((uint)layers[2].Length) : 0);
    }

    /// <summary>
    /// Appends Exif and XMP payload items associated with the primary presentation item. The method writes nothing when
    /// the encoder skips metadata.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel format.</typeparam>
    /// <param name="image">The source image that holds the metadata.</param>
    /// <param name="stream">The shared payload, which receives the metadata payloads.</param>
    /// <param name="primaryItem">The item that the metadata describes.</param>
    /// <param name="items">The destination item declarations.</param>
    /// <param name="links">The destination item relationships.</param>
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
    /// Serializes the Exif profile and finds its TIFF header. A HEIF Exif item starts with a four-byte offset to this
    /// header.
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

        // A profile can keep the optional Exif identifier before the TIFF byte-order marker, so search for the marker.
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
    /// Describes the already-written color and optional alpha extents backing one AV1 image item.
    /// </summary>
    private readonly struct Av1ImageItemEncoding
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Av1ImageItemEncoding"/> struct for an item of one frame.
        /// </summary>
        /// <param name="colorConfiguration">The codec configuration of the color payload.</param>
        /// <param name="colorOffset">The offset of the color payload within the media data.</param>
        /// <param name="colorLength">The length of the color payload.</param>
        /// <param name="alphaConfiguration">The codec configuration of the alpha payload, or <see langword="null"/> without alpha.</param>
        /// <param name="alphaOffset">The offset of the alpha payload within the media data.</param>
        /// <param name="alphaLength">The length of the alpha payload.</param>
        public Av1ImageItemEncoding(
            Av1CodecConfiguration colorConfiguration,
            long colorOffset,
            long colorLength,
            Av1CodecConfiguration? alphaConfiguration,
            long alphaOffset,
            long alphaLength)
            : this(
                colorConfiguration,
                [new HeifLocation(HeifLocationOffsetOrigin.FileOffset, 0L, colorOffset, colorLength)],
                alphaConfiguration,
                alphaConfiguration is null ? null : [new HeifLocation(HeifLocationOffsetOrigin.FileOffset, 0L, alphaOffset, alphaLength)])
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Av1ImageItemEncoding"/> struct for an item of one extent per
        /// layer.
        /// </summary>
        /// <param name="colorConfiguration">The codec configuration of the color payload.</param>
        /// <param name="colorLocations">The color payload extent of each layer.</param>
        /// <param name="alphaConfiguration">The codec configuration of the alpha payload, or <see langword="null"/> without alpha.</param>
        /// <param name="alphaLocations">The alpha payload extent of each layer, or <see langword="null"/> without alpha.</param>
        public Av1ImageItemEncoding(
            Av1CodecConfiguration colorConfiguration,
            HeifLocation[] colorLocations,
            Av1CodecConfiguration? alphaConfiguration,
            HeifLocation[]? alphaLocations)
        {
            this.ColorConfiguration = colorConfiguration;
            this.ColorLocations = colorLocations;
            this.AlphaConfiguration = alphaConfiguration;
            this.AlphaLocations = alphaLocations;
        }

        /// <summary>
        /// Gets the codec configuration of the color payload.
        /// </summary>
        public Av1CodecConfiguration ColorConfiguration { get; }

        /// <summary>
        /// Gets the color payload extents: one extent, or one per layer of a layered image.
        /// </summary>
        public HeifLocation[] ColorLocations { get; }

        /// <summary>
        /// Gets the codec configuration of the alpha payload, or <see langword="null"/> without alpha.
        /// </summary>
        public Av1CodecConfiguration? AlphaConfiguration { get; }

        /// <summary>
        /// Gets the alpha payload extents, or <see langword="null"/> without alpha.
        /// </summary>
        public HeifLocation[]? AlphaLocations { get; }
    }
}
