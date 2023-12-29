// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heic;

/// <summary>
/// Performs the PBM decoding operation.
/// </summary>
internal sealed class HeicDecoderCore : IImageDecoderInternals
{
    /// <summary>
    /// The general configuration.
    /// </summary>
    private readonly Configuration configuration;

    /// <summary>
    /// The <see cref="ImageMetadata"/> decoded by this decoder instance.
    /// </summary>
    private readonly ImageMetadata metadata;

    private uint primaryItem;

    private readonly List<HeicItem> items;

    private readonly List<HeicItemLink> itemLinks;

    private readonly byte[] buffer;

    /// <summary>
    /// Initializes a new instance of the <see cref="HeicDecoderCore" /> class.
    /// </summary>
    /// <param name="options">The decoder options.</param>
    public HeicDecoderCore(DecoderOptions options)
    {
        this.Options = options;
        this.configuration = options.Configuration;
        this.metadata = new ImageMetadata();
        this.items = new List<HeicItem>();
        this.itemLinks = new List<HeicItemLink>();
        this.buffer = new byte[80];
    }

    /// <inheritdoc/>
    public DecoderOptions Options { get; }

    /// <inheritdoc/>
    public Size Dimensions { get; }

    /// <inheritdoc/>
    public Image<TPixel> Decode<TPixel>(BufferedReadStream stream, CancellationToken cancellationToken)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        if (!this.CheckFileTypeBox(stream))
        {
            throw new ImageFormatException("Not an HEIC image.");
        }

        Image<TPixel>? image = null;
        while (stream.Position < stream.Length)
        {
            long boxLength = this.ReadBoxHeader(stream, out Heic4CharCode boxType);
            EnsureBoxBoundary(boxLength, stream);
            switch (boxType)
            {
                case Heic4CharCode.meta:
                    this.ParseMetadata(stream, boxLength);
                    break;
                case Heic4CharCode.mdat:
                    image = this.ParseMediaData<TPixel>(stream, boxLength);
                    break;
                case Heic4CharCode.free:
                    SkipBox(stream, boxLength);
                    break;
                case 0U:
                    // Some files have trailing zeros, skiping to EOF.
                    stream.Skip((int)(stream.Length - stream.Position));
                    break;
                default:
                    throw new ImageFormatException($"Unknown box type of '{PrettyPrint(boxType)}'");
            }
        }

        HeicItem? item = this.FindItemById(this.primaryItem);
        if (item == null)
        {
            throw new ImageFormatException("No primary item found");
        }

        if (image == null)
        {
            throw new NotImplementedException("No JPEG image decoded");
        }

        return image;
    }

    /// <inheritdoc/>
    public ImageInfo Identify(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        this.CheckFileTypeBox(stream);

        while (stream.Position < stream.Length)
        {
            long boxLength = this.ReadBoxHeader(stream, out Heic4CharCode boxType);
            EnsureBoxBoundary(boxLength, stream);
            switch (boxType)
            {
                case Heic4CharCode.meta:
                    this.ParseMetadata(stream, boxLength);
                    break;
                default:
                    // Silently skip all other box types.
                    SkipBox(stream, boxLength);
                    break;
            }
        }

        HeicItem? item = this.FindItemById(this.primaryItem);
        if (item == null)
        {
            throw new ImageFormatException("No primary item found");
        }

        HeicMetadata meta = this.metadata.GetHeicMetadata();
        HeicCompressionMethod compressionMethod = HeicCompressionMethod.Hevc;
        if (item.Type == Heic4CharCode.av01)
        {
            compressionMethod = HeicCompressionMethod.Av1;
        }
        else if (this.itemLinks.Any(link => link.Type == Heic4CharCode.thmb))
        {
            compressionMethod = HeicCompressionMethod.LegacyJpeg;
        }

        meta.CompressionMethod = compressionMethod;

        return new ImageInfo(new PixelTypeInfo(item.BitsPerPixel), new(item.Extent.Width, item.Extent.Height), this.metadata);
    }

    private bool CheckFileTypeBox(BufferedReadStream stream)
    {
        long boxLength = this.ReadBoxHeader(stream, out Heic4CharCode boxType);
        Span<byte> buffer = this.ReadIntoBuffer(stream, boxLength);
        uint majorBrand = BinaryPrimitives.ReadUInt32BigEndian(buffer);
        bool correctBrand = majorBrand == (uint)Heic4CharCode.heic || majorBrand == (uint)Heic4CharCode.heix;

        // TODO: Interpret minorVersion and compatible brands.
        return boxType == Heic4CharCode.ftyp && correctBrand;
    }

    private long ReadBoxHeader(BufferedReadStream stream, out Heic4CharCode boxType)
    {
        // Read 4 bytes of length of box
        Span<byte> buf = this.ReadIntoBuffer(stream, 8);
        long boxSize = BinaryPrimitives.ReadUInt32BigEndian(buf);
        long headerSize = 8;

        // Read 4 bytes of box type
        boxType = (Heic4CharCode)BinaryPrimitives.ReadUInt32BigEndian(buf[4..]);

        if (boxSize == 1)
        {
            buf = this.ReadIntoBuffer(stream, 8);
            boxSize = (long)BinaryPrimitives.ReadUInt64BigEndian(buf);
            headerSize += 8;
        }

        return boxSize - headerSize;
    }

    private static int ParseBoxHeader(Span<byte> buffer, out long length, out Heic4CharCode boxType)
    {
        long boxSize = BinaryPrimitives.ReadUInt32BigEndian(buffer);
        int bytesRead = 4;
        boxType = (Heic4CharCode)BinaryPrimitives.ReadUInt32BigEndian(buffer[bytesRead..]);
        bytesRead += 4;
        if (boxSize == 1)
        {
            boxSize = (long)BinaryPrimitives.ReadUInt64BigEndian(buffer[bytesRead..]);
            bytesRead += 8;
        }

        length = boxSize - bytesRead;
        return bytesRead;
    }

    private void ParseMetadata(BufferedReadStream stream, long boxLength)
    {
        long endPosition = stream.Position + boxLength;
        stream.Skip(4);
        while (stream.Position < endPosition)
        {
            long length = this.ReadBoxHeader(stream, out Heic4CharCode boxType);
            EnsureBoxBoundary(length, boxLength);
            switch (boxType)
            {
                case Heic4CharCode.iprp:
                    this.ParseItemProperties(stream, length);
                    break;
                case Heic4CharCode.iinf:
                    this.ParseItemInfo(stream, length);
                    break;
                case Heic4CharCode.iref:
                    this.ParseItemReference(stream, length);
                    break;
                case Heic4CharCode.pitm:
                    this.ParsePrimaryItem(stream, length);
                    break;
                case Heic4CharCode.hdlr:
                    this.ParseHandler(stream, length);
                    break;
                case Heic4CharCode.iloc:
                    this.ParseItemLocation(stream, length);
                    break;
                case Heic4CharCode.dinf:
                case Heic4CharCode.idat:
                case Heic4CharCode.grpl:
                case Heic4CharCode.ipro:
                case Heic4CharCode.uuid:
                case Heic4CharCode.ipmc:
                    // Silently skip these boxes.
                    SkipBox(stream, length);
                    break;
                default:
                    throw new ImageFormatException($"Unknown metadata box type of '{PrettyPrint(boxType)}'");
            }
        }
    }

    private void ParseHandler(BufferedReadStream stream, long boxLength)
    {
        Span<byte> buffer = this.ReadIntoBuffer(stream, boxLength);

        // Only read the handler type, to check if this is not a movie file.
        int bytesRead = 8;
        Heic4CharCode handlerType = (Heic4CharCode)BinaryPrimitives.ReadUInt32BigEndian(buffer[bytesRead..]);
        if (handlerType != Heic4CharCode.pict)
        {
            throw new ImageFormatException("Not a picture file.");
        }
    }

    private void ParseItemInfo(BufferedReadStream stream, long boxLength)
    {
        Span<byte> buffer = this.ReadIntoBuffer(stream, boxLength);
        uint entryCount;
        int bytesRead = 0;
        byte version = buffer[bytesRead];
        bytesRead += 4;
        entryCount = ReadUInt16Or32(buffer, version != 0, ref bytesRead);

        for (uint i = 0; i < entryCount; i++)
        {
            bytesRead += this.ParseItemInfoEntry(buffer[bytesRead..]);
        }
    }

    private int ParseItemInfoEntry(Span<byte> buffer)
    {
        int bytesRead = ParseBoxHeader(buffer, out long boxLength, out Heic4CharCode boxType);
        byte version = buffer[bytesRead];
        bytesRead += 4;
        HeicItem? item = null;
        if (version is 0 or 1)
        {
            uint itemId = BinaryPrimitives.ReadUInt16BigEndian(buffer[bytesRead..]);
            bytesRead += 2;
            item = new HeicItem(boxType, itemId);

            // Skip Protection Index, not sure what that means...
            bytesRead += 2;
            item.Name = ReadNullTerminatedString(buffer[bytesRead..]);
            bytesRead += item.Name.Length + 1;
            item.ContentType = ReadNullTerminatedString(buffer[bytesRead..]);
            bytesRead += item.ContentType.Length + 1;

            // Optional field.
            if (bytesRead < boxLength)
            {
                item.ContentEncoding = ReadNullTerminatedString(buffer[bytesRead..]);
                bytesRead += item.ContentEncoding.Length + 1;
            }
        }

        if (version == 1)
        {
            // Optional fields.
            if (bytesRead < boxLength)
            {
                item!.ExtensionType = BinaryPrimitives.ReadUInt32BigEndian(buffer[bytesRead..]);
                bytesRead += 4;
            }

            if (bytesRead < boxLength)
            {
                // TODO: Parse item.Extension
            }
        }

        if (version >= 2)
        {
            uint itemId = ReadUInt16Or32(buffer, version == 3, ref bytesRead);

            // Skip Protection Index, not sure what that means...
            bytesRead += 2;
            Heic4CharCode itemType = (Heic4CharCode)BinaryPrimitives.ReadUInt32BigEndian(buffer[bytesRead..]);
            bytesRead += 4;
            item = new HeicItem(itemType, itemId);
            item.Name = ReadNullTerminatedString(buffer[bytesRead..]);
            bytesRead += item.Name.Length + 1;
            if (item.Type == Heic4CharCode.mime)
            {
                item.ContentType = ReadNullTerminatedString(buffer[bytesRead..]);
                bytesRead += item.ContentType.Length + 1;

                // Optional field.
                if (bytesRead < boxLength)
                {
                    item.ContentEncoding = ReadNullTerminatedString(buffer[bytesRead..]);
                    bytesRead += item.ContentEncoding.Length + 1;
                }
            }
            else if (item.Type == Heic4CharCode.uri)
            {
                item.UriType = ReadNullTerminatedString(buffer[bytesRead..]);
                bytesRead += item.UriType.Length + 1;
            }
        }

        if (item != null)
        {
            this.items.Add(item);
        }

        return bytesRead;
    }

    private void ParseItemReference(BufferedReadStream stream, long boxLength)
    {
        Span<byte> buffer = this.ReadIntoBuffer(stream, boxLength);
        int bytesRead = 0;
        bool largeIds = buffer[bytesRead] != 0;
        bytesRead += 4;
        while (bytesRead < boxLength)
        {
            bytesRead += ParseBoxHeader(buffer[bytesRead..], out long subBoxLength, out Heic4CharCode linkType);
            uint sourceId = ReadUInt16Or32(buffer, largeIds, ref bytesRead);
            HeicItemLink link = new(linkType, sourceId);

            int count = BinaryPrimitives.ReadUInt16BigEndian(buffer[bytesRead..]);
            bytesRead += 2;
            for (uint i = 0; i < count; i++)
            {
                uint destId = ReadUInt16Or32(buffer, largeIds, ref bytesRead);
                link.DestinationIds.Add(destId);
            }

            this.itemLinks!.Add(link);
        }
    }

    private void ParsePrimaryItem(BufferedReadStream stream, long boxLength)
    {
        // BoxLength should be 6 or 8.
        Span<byte> buffer = this.ReadIntoBuffer(stream, boxLength);
        byte version = buffer[0];
        int bytesRead = 4;
        this.primaryItem = ReadUInt16Or32(buffer, version != 0, ref bytesRead);
    }

    private void ParseItemProperties(BufferedReadStream stream, long boxLength)
    {
        // Cannot use Dictionary here, Properties can have multiple instances with the same key.
        List<KeyValuePair<Heic4CharCode, object>> properties = new();
        long endBoxPosition = stream.Position + boxLength;
        while (stream.Position < endBoxPosition)
        {
            long containerLength = this.ReadBoxHeader(stream, out Heic4CharCode containerType);
            EnsureBoxBoundary(containerLength, boxLength);
            if (containerType == Heic4CharCode.ipco)
            {
                // Parse Item Property Container, which is just an array of property boxes.
                this.ParsePropertyContainer(stream, containerLength, properties);
            }
            else if (containerType == Heic4CharCode.ipma)
            {
                // Parse Item Property Association
                this.ParsePropertyAssociation(stream, containerLength, properties);
            }
            else
            {
                throw new ImageFormatException($"Unknown container type in property box of '{PrettyPrint(containerType)}'");
            }
        }
    }

    private void ParsePropertyContainer(BufferedReadStream stream, long boxLength, List<KeyValuePair<Heic4CharCode, object>> properties)
    {
        long endPosition = stream.Position + boxLength;
        while (stream.Position < endPosition)
        {
            int itemLength = (int)this.ReadBoxHeader(stream, out Heic4CharCode itemType);
            EnsureBoxBoundary(itemLength, boxLength);
            Span<byte> buffer = this.ReadIntoBuffer(stream, itemLength);
            switch (itemType)
            {
                case Heic4CharCode.ispe:
                    // Skip over version (8 bits) and flags (24 bits).
                    int width = (int)BinaryPrimitives.ReadUInt32BigEndian(buffer[4..]);
                    int height = (int)BinaryPrimitives.ReadUInt32BigEndian(buffer[8..]);
                    properties.Add(new KeyValuePair<Heic4CharCode, object>(Heic4CharCode.ispe, new Size(width, height)));
                    break;
                case Heic4CharCode.pasp:
                    int horizontalSpacing = (int)BinaryPrimitives.ReadUInt32BigEndian(buffer);
                    int verticalSpacing = (int)BinaryPrimitives.ReadUInt32BigEndian(buffer[4..]);
                    properties.Add(new KeyValuePair<Heic4CharCode, object>(Heic4CharCode.pasp, new Size(horizontalSpacing, verticalSpacing)));
                    break;
                case Heic4CharCode.pixi:
                    // Skip over version (8 bits) and flags (24 bits).
                    int channelCount = buffer[4];
                    int offset = 5;
                    int bitsPerPixel = 0;
                    for (int i = 0; i < channelCount; i++)
                    {
                        bitsPerPixel += buffer[offset + i];
                    }

                    properties.Add(new KeyValuePair<Heic4CharCode, object>(Heic4CharCode.pixi, new int[] { channelCount, bitsPerPixel }));

                    break;
                case Heic4CharCode.altt:
                case Heic4CharCode.colr:
                case Heic4CharCode.imir:
                case Heic4CharCode.irot:
                case Heic4CharCode.iscl:
                case Heic4CharCode.hvcC:
                case Heic4CharCode.av1C:
                case Heic4CharCode.rloc:
                case Heic4CharCode.udes:
                    // TODO: Implement
                    properties.Add(new KeyValuePair<Heic4CharCode, object>(itemType, new object()));
                    break;
                default:
                    throw new ImageFormatException($"Unknown item type in property box of '{PrettyPrint(itemType)}'");
            }
        }
    }

    private void ParsePropertyAssociation(BufferedReadStream stream, long boxLength, List<KeyValuePair<Heic4CharCode, object>> properties)
    {
        Span<byte> buffer = this.ReadIntoBuffer(stream, boxLength);
        byte version = buffer[0];
        byte flags = buffer[3];
        int bytesRead = 4;
        int itemId = (int)ReadUInt16Or32(buffer, version >= 1, ref bytesRead);

        int associationCount = buffer[bytesRead++];
        for (int i = 0; i < associationCount; i++)
        {
            uint propId;
            if (flags == 1)
            {
                propId = BinaryPrimitives.ReadUInt16BigEndian(buffer[bytesRead..]) & 0x4FFFU;
                bytesRead += 2;
            }
            else
            {
                propId = buffer[bytesRead++] & 0x4FU;
            }

            KeyValuePair<Heic4CharCode, object> prop = properties[(int)propId];
            switch (prop.Key)
            {
                case Heic4CharCode.ispe:
                    this.items[itemId].SetExtent((Size)prop.Value);
                    break;
                case Heic4CharCode.pasp:
                    this.items[itemId].PixelAspectRatio = (Size)prop.Value;
                    break;
                case Heic4CharCode.pixi:
                    int[] values = (int[])prop.Value;
                    this.items[itemId].ChannelCount = values[0];
                    this.items[itemId].BitsPerPixel = values[1];
                    break;
            }
        }
    }

    private void ParseItemLocation(BufferedReadStream stream, long boxLength)
    {
        Span<byte> buffer = this.ReadIntoBuffer(stream, boxLength);
        int bytesRead = 0;
        byte version = buffer[bytesRead];
        bytesRead += 4;
        byte b1 = buffer[bytesRead++];
        byte b2 = buffer[bytesRead++];
        int offsetSize = (b1 >> 4) & 0x0f;
        int lengthSize = b1 & 0x0f;
        int baseOffsetSize = (b2 >> 4) & 0x0f;
        int indexSize = 0;
        if (version is 1 or 2)
        {
            indexSize = b2 & 0x0f;
        }

        uint itemCount = ReadUInt16Or32(buffer, version == 2, ref bytesRead);
        for (uint i = 0; i < itemCount; i++)
        {
            uint itemId = ReadUInt16Or32(buffer, version == 2, ref bytesRead);
            HeicItem? item = this.FindItemById(itemId);
            if (version is 1 or 2)
            {
                bytesRead++;
                byte b3 = buffer[bytesRead++];
                int constructionMethod = b3 & 0x0f;
            }

            uint dataReferenceIndex = BinaryPrimitives.ReadUInt16BigEndian(buffer[bytesRead..]);
            bytesRead += 2;
            ulong baseOffset = ReadUIntVariable(buffer, baseOffsetSize, ref bytesRead);
            uint extentCount = BinaryPrimitives.ReadUInt16BigEndian(buffer[bytesRead..]);
            bytesRead += 2;
            for (uint j = 0; j < extentCount; j++)
            {
                if (version is 1 or 2 && indexSize > 0)
                {
                    _ = ReadUIntVariable(buffer, indexSize, ref bytesRead);
                }

                ulong extentOffset = ReadUIntVariable(buffer, offsetSize, ref bytesRead);
                ulong extentLength = ReadUIntVariable(buffer, lengthSize, ref bytesRead);
                HeicLocation loc = new HeicLocation((long)extentOffset, (long)extentLength);
                item?.DataLocations.Add(loc);
            }
        }
    }

    private static uint ReadUInt16Or32(Span<byte> buffer, bool isLarge, ref int bytesRead)
    {
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

    private static ulong ReadUIntVariable(Span<byte> buffer, int numBytes, ref int bytesRead)
    {
        ulong result = 0UL;
        if (numBytes == 8)
        {
            result = BinaryPrimitives.ReadUInt64BigEndian(buffer[bytesRead..]);
            bytesRead += 8;
        }
        else if (numBytes == 4)
        {
            result = BinaryPrimitives.ReadUInt32BigEndian(buffer[bytesRead..]);
            bytesRead += 4;
        }
        else if (numBytes == 2)
        {
            result = BinaryPrimitives.ReadUInt16BigEndian(buffer[bytesRead..]);
            bytesRead += 2;
        }
        else if (numBytes == 1)
        {
            result = buffer[bytesRead++];
        }

        return result;
    }

    private Image<TPixel> ParseMediaData<TPixel>(BufferedReadStream stream, long boxLength)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // FIXME: No HVC decoding yet, so parse only a JPEG thumbnail.
        HeicItemLink? thumbLink = this.itemLinks.FirstOrDefault(link => link.Type == Heic4CharCode.thmb);
        if (thumbLink == null)
        {
            throw new NotImplementedException("No thumbnail found");
        }

        HeicItem? thumbItem = this.FindItemById(thumbLink.SourceId);
        if (thumbItem == null || thumbItem.Type != Heic4CharCode.jpeg)
        {
            throw new NotImplementedException("No HVC decoding implemented yet");
        }

        int thumbFileOffset = (int)thumbItem.DataLocations[0].Offset;
        int thumbFileLength = (int)thumbItem.DataLocations[0].Length;
        stream.Skip((int)(thumbFileOffset - stream.Position));
        using IMemoryOwner<byte> thumbMemory = this.configuration.MemoryAllocator.Allocate<byte>(thumbFileLength);
        Span<byte> thumbSpan = thumbMemory.GetSpan();
        stream.Read(thumbSpan);

        HeicMetadata meta = this.metadata.GetHeicMetadata();
        meta.CompressionMethod = HeicCompressionMethod.LegacyJpeg;

        return Image.Load<TPixel>(thumbSpan);
    }

    private static void SkipBox(BufferedReadStream stream, long boxLength)
        => stream.Skip((int)boxLength);

    private Span<byte> ReadIntoBuffer(BufferedReadStream stream, long length)
    {
        if (length <= this.buffer.Length)
        {
            stream.Read(this.buffer, 0, (int)length);
            return this.buffer;
        }
        else
        {
            Span<byte> temp = new byte[length];
            stream.Read(temp);
            return temp;
        }
    }

    private static void EnsureBoxBoundary(long boxLength, Stream stream)
        => EnsureBoxBoundary(boxLength, stream.Length - stream.Position);

    private static void EnsureBoxBoundary(long boxLength, long parentLength)
    {
        if (boxLength > parentLength)
        {
            throw new ImageFormatException("Box size beyond boundary");
        }
    }

    private HeicItem? FindItemById(uint itemId)
        => this.items.FirstOrDefault(item => item.Id == itemId);

    private static string ReadNullTerminatedString(Span<byte> span)
    {
        Span<byte> bytes = span[..span.IndexOf((byte)0)];
        return Encoding.UTF8.GetString(bytes);
    }

    private static string PrettyPrint(Heic4CharCode code)
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
