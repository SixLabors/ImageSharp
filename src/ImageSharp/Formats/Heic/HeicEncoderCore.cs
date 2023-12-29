// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Buffers.Binary;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heic;

/// <summary>
/// Image encoder for writing an image to a stream as a HEIC image.
/// </summary>
internal sealed class HeicEncoderCore : IImageEncoderInternals
{
    /// <summary>
    /// The global configuration.
    /// </summary>
    private Configuration configuration;

    /// <summary>
    /// The encoder with options.
    /// </summary>
    private readonly HeicEncoder encoder;

    /// <summary>
    /// Initializes a new instance of the <see cref="HeicEncoderCore"/> class.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="encoder">The encoder with options.</param>
    public HeicEncoderCore(Configuration configuration, HeicEncoder encoder)
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
    public async void Encode<TPixel>(Image<TPixel> image, Stream stream, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Guard.NotNull(image, nameof(image));
        Guard.NotNull(stream, nameof(stream));

        byte[] pixels = await CompressPixels(image, cancellationToken);
        List<HeicItem> items = new();
        List<HeicItemLink> links = new();
        GenerateItems(image, pixels, items, links);

        // Write out the generated header and pixels.
        this.WriteFileTypeBox(stream);
        this.WriteMetadataBox(items, links, stream);
        this.WriteMediaDataBox(pixels, stream);
        stream.Flush();
    }

    private static void GenerateItems<TPixel>(Image<TPixel> image, byte[] pixels, List<HeicItem> items, List<HeicItemLink> links)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        HeicItem primaryItem = new(Heic4CharCode.jpeg, 1u);
        primaryItem.DataLocations.Add(new HeicLocation(0L, pixels.LongLength));
        primaryItem.BitsPerPixel = 24;
        primaryItem.ChannelCount = 3;
        primaryItem.SetExtent(image.Size);
        items.Add(primaryItem);

        // Create a fake thumbnail, to make our own Decoder happy.
        HeicItemLink thumbnail = new(Heic4CharCode.thmb, 1u);
        thumbnail.DestinationIds.Add(1u);
        links.Add(thumbnail);
    }

    private static int WriteBoxHeader(Span<byte> buffer, Heic4CharCode type)
    {
        int bytesWritten = 0;
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], 8U);
        bytesWritten += 4;
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)type);
        bytesWritten += 4;

        return bytesWritten;
    }

    private static int WriteBoxHeader(Span<byte> buffer, Heic4CharCode type, byte version, uint flags)
    {
        int bytesWritten = 0;
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], 12U);
        bytesWritten += 4;
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)type);
        bytesWritten += 4;

        // Layout in memory is 4 bytes, 1 version byte followed by 3 flag bytes.
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], flags);
        buffer[bytesWritten] = version;
        bytesWritten += 4;

        return bytesWritten;
    }

    private void WriteFileTypeBox(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[24];
        int bytesWritten = WriteBoxHeader(buffer, Heic4CharCode.ftyp);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)Heic4CharCode.heic);
        bytesWritten += 4;
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], 0U);
        bytesWritten += 4;
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)Heic4CharCode.mif1);
        bytesWritten += 4;
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)Heic4CharCode.heic);
        bytesWritten += 4;

        BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)bytesWritten);
        stream.Write(buffer);
    }

    private void WriteMetadataBox(List<HeicItem> items, List<HeicItemLink> links, Stream stream)
    {
        using AutoExpandingMemory<byte> memory = new(this.configuration, 0x1000);
        Span<byte> buffer = memory.GetSpan(12);
        int bytesWritten = WriteBoxHeader(buffer, Heic4CharCode.meta, 0, 0);
        bytesWritten += WriteHandlerBox(memory, bytesWritten);
        bytesWritten += WritePrimaryItemBox(memory, bytesWritten);
        bytesWritten += WriteItemInfoBox(memory, bytesWritten, items);
        bytesWritten += WriteItemReferenceBox(memory, bytesWritten, items, links);
        bytesWritten += WriteItemPropertiesBox(memory, bytesWritten, items);
        bytesWritten += WriteItemDataBox(memory, bytesWritten);
        bytesWritten += WriteItemLocationBox(memory, bytesWritten, items);

        buffer = memory.GetSpan(bytesWritten);
        BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)bytesWritten);
        stream.Write(buffer);
    }

    private static int WriteHandlerBox(AutoExpandingMemory<byte> memory, int memoryOffset)
    {
        Span<byte> buffer = memory.GetSpan(memoryOffset, 33);
        int bytesWritten = WriteBoxHeader(buffer, Heic4CharCode.hdlr, 0, 0);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], 0);
        bytesWritten += 4;
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)Heic4CharCode.pict);
        bytesWritten += 4;
        for (int i = 0; i < 13; i++)
        {
            buffer[bytesWritten++] = 0;
        }

        BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)bytesWritten);
        return bytesWritten;
    }

    private static int WritePrimaryItemBox(AutoExpandingMemory<byte> memory, int memoryOffset)
    {
        Span<byte> buffer = memory.GetSpan(memoryOffset, 14);
        int bytesWritten = WriteBoxHeader(buffer, Heic4CharCode.pitm, 0, 0);
        BinaryPrimitives.WriteUInt16BigEndian(buffer[bytesWritten..], 1);
        bytesWritten += 2;

        BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)bytesWritten);
        return bytesWritten;
    }

    private static int WriteItemInfoBox(AutoExpandingMemory<byte> memory, int memoryOffset, List<HeicItem> items)
    {
        Span<byte> buffer = memory.GetSpan(memoryOffset, 14 + (items.Count * 21));
        int bytesWritten = WriteBoxHeader(buffer, Heic4CharCode.iinf, 0, 0);
        BinaryPrimitives.WriteUInt16BigEndian(buffer[bytesWritten..], (ushort)items.Count);
        bytesWritten += 2;
        foreach (HeicItem item in items)
        {
            int itemLengthOffset = bytesWritten;
            bytesWritten += WriteBoxHeader(buffer[bytesWritten..], Heic4CharCode.infe, 2, 0);
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

    private static int WriteItemReferenceBox(AutoExpandingMemory<byte> memory, int memoryOffset, List<HeicItem> items, List<HeicItemLink> links)
    {
        Span<byte> buffer = memory.GetSpan(memoryOffset, 12 + (links.Count * (12 + (items.Count * 2))));
        int bytesWritten = WriteBoxHeader(buffer, Heic4CharCode.iref, 0, 0);
        foreach (HeicItemLink link in links)
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

    private static int WriteItemPropertiesBox(AutoExpandingMemory<byte> memory, int memoryOffset, List<HeicItem> items)
    {
        const ushort numPropPerItem = 1;
        Span<byte> buffer = memory.GetSpan(memoryOffset, 20);
        int bytesWritten = WriteBoxHeader(buffer, Heic4CharCode.iprp);

        // Write 'ipco' box
        int ipcoLengthOffset = bytesWritten;
        bytesWritten += WriteBoxHeader(buffer[bytesWritten..], Heic4CharCode.ipco);
        foreach (HeicItem item in items)
        {
            bytesWritten += WriteSpatialExtentPropertyBox(memory, memoryOffset + bytesWritten, item);
        }

        BinaryPrimitives.WriteUInt32BigEndian(buffer[ipcoLengthOffset..], (uint)(bytesWritten - ipcoLengthOffset));
        buffer = memory.GetSpan(memoryOffset, bytesWritten + 16 + (5 * items.Count * numPropPerItem));

        // Write 'ipma' box
        int ipmaLengthOffset = bytesWritten;
        bytesWritten += WriteBoxHeader(buffer[bytesWritten..], Heic4CharCode.ipma, 0, 0);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)(items.Count * numPropPerItem));
        bytesWritten += 4;
        ushort propIndex = 0;
        foreach (HeicItem item in items)
        {
            BinaryPrimitives.WriteUInt16BigEndian(buffer[bytesWritten..], (ushort)item.Id);
            bytesWritten += 2;
            buffer[bytesWritten++] = 1;
            BinaryPrimitives.WriteUInt16BigEndian(buffer[bytesWritten..], propIndex);
            bytesWritten += 2;
            propIndex += numPropPerItem;
        }

        BinaryPrimitives.WriteUInt32BigEndian(buffer[ipmaLengthOffset..], (uint)(bytesWritten - ipmaLengthOffset));

        // Update size of enclosing 'iprp' box.
        BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)bytesWritten);
        return bytesWritten;
    }

    private static int WriteSpatialExtentPropertyBox(AutoExpandingMemory<byte> memory, int memoryOffset, HeicItem item)
    {
        Span<byte> buffer = memory.GetSpan(memoryOffset, 20);
        int bytesWritten = WriteBoxHeader(buffer, Heic4CharCode.ispe, 0, 0);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)item.Extent.Width);
        bytesWritten += 4;
        BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)item.Extent.Height);
        bytesWritten += 4;

        BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)bytesWritten);
        return bytesWritten;
    }

    private static int WriteItemDataBox(AutoExpandingMemory<byte> memory, int memoryOffset)
    {
        Span<byte> buffer = memory.GetSpan(memoryOffset, 10);
        int bytesWritten = WriteBoxHeader(buffer, Heic4CharCode.idat);

        BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)bytesWritten);
        return bytesWritten;
    }

    private static int WriteItemLocationBox(AutoExpandingMemory<byte> memory, int memoryOffset, List<HeicItem> items)
    {
        Span<byte> buffer = memory.GetSpan(memoryOffset, 30 + (items.Count << 3));
        int bytesWritten = WriteBoxHeader(buffer, Heic4CharCode.iloc, 1, 0);
        buffer[bytesWritten++] = 0x44;
        buffer[bytesWritten++] = 0;
        BinaryPrimitives.WriteUInt16BigEndian(buffer[bytesWritten..], 1);
        bytesWritten += 2;
        BinaryPrimitives.WriteUInt16BigEndian(buffer[bytesWritten..], (ushort)items[0].Id);
        bytesWritten += 2;
        for (int i = 0; i < 4; i++)
        {
            buffer[bytesWritten++] = 0;
        }

        IEnumerable<HeicLocation> itemLocs = items.SelectMany(item => item.DataLocations).Where(loc => loc != null);
        BinaryPrimitives.WriteUInt16BigEndian(buffer[bytesWritten..], (ushort)itemLocs.Count());
        bytesWritten += 2;
        foreach (HeicLocation loc in itemLocs)
        {
            BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)loc.Offset);
            bytesWritten += 4;
            BinaryPrimitives.WriteUInt32BigEndian(buffer[bytesWritten..], (uint)loc.Length);
            bytesWritten += 4;
        }

        BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)bytesWritten);
        return bytesWritten;
    }

    private void WriteMediaDataBox(Span<byte> data, Stream stream)
    {
        Span<byte> buf = stackalloc byte[12];
        int bytesWritten = WriteBoxHeader(buf, Heic4CharCode.mdat);
        BinaryPrimitives.WriteUInt32BigEndian(buf, (uint)(data.Length + bytesWritten));
        stream.Write(buf[..bytesWritten]);

        stream.Write(data);
    }

    private static async Task<byte[]> CompressPixels<TPixel>(Image<TPixel> image, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using MemoryStream stream = new();
        JpegEncoder encoder = new()
        {
            ColorType = JpegEncodingColor.YCbCrRatio420
        };
        await image.SaveAsJpegAsync(stream, encoder, cancellationToken);
        return stream.ToArray();
    }
}
