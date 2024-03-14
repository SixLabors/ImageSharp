// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Performs the HEIF decoding operation.
/// </summary>
internal sealed class HeifDecoderCore : IImageDecoderInternals
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

    private readonly List<HeifItem> items;

    private readonly List<HeifItemLink> itemLinks;

    private readonly byte[] buffer;

    /// <summary>
    /// Initializes a new instance of the <see cref="HeifDecoderCore" /> class.
    /// </summary>
    /// <param name="options">The decoder options.</param>
    public HeifDecoderCore(DecoderOptions options)
    {
        this.Options = options;
        this.configuration = options.Configuration;
        this.metadata = new ImageMetadata();
        this.items = new List<HeifItem>();
        this.itemLinks = new List<HeifItemLink>();
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
            throw new ImageFormatException("Not an HEIF image.");
        }

        Image<TPixel>? image = null;
        while (stream.Position < stream.Length)
        {
            long boxLength = this.ReadBoxHeader(stream, out Heif4CharCode boxType);
            EnsureBoxBoundary(boxLength, stream);
            switch (boxType)
            {
                case Heif4CharCode.Meta:
                    this.ParseMetadata(stream, boxLength);
                    break;
                case Heif4CharCode.Mdat:
                    image = this.ParseMediaData<TPixel>(stream, boxLength);
                    break;
                case Heif4CharCode.Free:
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

        HeifItem? item = this.FindItemById(this.primaryItem);
        if (item == null)
        {
            throw new ImageFormatException("No primary item found");
        }

        if (image == null)
        {
            throw new NotImplementedException("No JPEG image decoded");
        }

        this.UpdateMetadata(image.Metadata, item);
        return image;
    }

    /// <inheritdoc/>
    public ImageInfo Identify(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        this.CheckFileTypeBox(stream);

        while (stream.Position < stream.Length)
        {
            long boxLength = this.ReadBoxHeader(stream, out Heif4CharCode boxType);
            EnsureBoxBoundary(boxLength, stream);
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
        if (item == null)
        {
            throw new ImageFormatException("No primary item found");
        }

        this.UpdateMetadata(this.metadata, item);

        return new ImageInfo(new PixelTypeInfo(item.BitsPerPixel), new(item.Extent.Width, item.Extent.Height), this.metadata);
    }

    private bool CheckFileTypeBox(BufferedReadStream stream)
    {
        long boxLength = this.ReadBoxHeader(stream, out Heif4CharCode boxType);
        Span<byte> buffer = this.ReadIntoBuffer(stream, boxLength);
        uint majorBrand = BinaryPrimitives.ReadUInt32BigEndian(buffer);
        bool correctBrand = majorBrand is (uint)Heif4CharCode.Heic or (uint)Heif4CharCode.Heix or (uint)Heif4CharCode.Avif;

        // TODO: Interpret minorVersion and compatible brands.
        return boxType == Heif4CharCode.Ftyp && correctBrand;
    }

    private void UpdateMetadata(ImageMetadata metadata, HeifItem item)
    {
        HeifMetadata meta = metadata.GetHeifMetadata();
        HeifCompressionMethod compressionMethod = HeifCompressionMethod.Hevc;
        if (item.Type == Heif4CharCode.Av01)
        {
            compressionMethod = HeifCompressionMethod.Av1;
        }
        else if (item.Type == Heif4CharCode.Jpeg || this.itemLinks.Any(link => link.Type == Heif4CharCode.Thmb))
        {
            compressionMethod = HeifCompressionMethod.LegacyJpeg;
        }

        meta.CompressionMethod = compressionMethod;
    }

    private long ReadBoxHeader(BufferedReadStream stream, out Heif4CharCode boxType)
    {
        // Read 4 bytes of length of box
        Span<byte> buf = this.ReadIntoBuffer(stream, 8);
        long boxSize = BinaryPrimitives.ReadUInt32BigEndian(buf);
        long headerSize = 8;

        // Read 4 bytes of box type
        boxType = (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(buf[4..]);

        if (boxSize == 1)
        {
            buf = this.ReadIntoBuffer(stream, 8);
            boxSize = (long)BinaryPrimitives.ReadUInt64BigEndian(buf);
            headerSize += 8;
        }

        return boxSize - headerSize;
    }

    private static int ParseBoxHeader(Span<byte> buffer, out long length, out Heif4CharCode boxType)
    {
        long boxSize = BinaryPrimitives.ReadUInt32BigEndian(buffer);
        int bytesRead = 4;
        boxType = (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(buffer[bytesRead..]);
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
            long length = this.ReadBoxHeader(stream, out Heif4CharCode boxType);
            EnsureBoxBoundary(length, boxLength);
            switch (boxType)
            {
                case Heif4CharCode.Iprp:
                    this.ParseItemProperties(stream, length);
                    break;
                case Heif4CharCode.Iinf:
                    this.ParseItemInfo(stream, length);
                    break;
                case Heif4CharCode.Iref:
                    this.ParseItemReference(stream, length);
                    break;
                case Heif4CharCode.Pitm:
                    this.ParsePrimaryItem(stream, length);
                    break;
                case Heif4CharCode.Hdlr:
                    this.ParseHandler(stream, length);
                    break;
                case Heif4CharCode.Iloc:
                    this.ParseItemLocation(stream, length);
                    break;
                case Heif4CharCode.Dinf:
                case Heif4CharCode.Idat:
                case Heif4CharCode.Grpl:
                case Heif4CharCode.Ipro:
                case Heif4CharCode.Uuid:
                case Heif4CharCode.Ipmc:
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
        Heif4CharCode handlerType = (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(buffer[bytesRead..]);
        if (handlerType != Heif4CharCode.Pict)
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
        int bytesRead = ParseBoxHeader(buffer, out long boxLength, out Heif4CharCode boxType);
        byte version = buffer[bytesRead];
        bytesRead += 4;
        HeifItem? item = null;
        if (version is 0 or 1)
        {
            uint itemId = BinaryPrimitives.ReadUInt16BigEndian(buffer[bytesRead..]);
            bytesRead += 2;
            item = new HeifItem(boxType, itemId);

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
            Heif4CharCode itemType = (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(buffer[bytesRead..]);
            bytesRead += 4;
            item = new HeifItem(itemType, itemId);
            item.Name = ReadNullTerminatedString(buffer[bytesRead..]);
            bytesRead += item.Name.Length + 1;
            if (item.Type == Heif4CharCode.Mime)
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
            else if (item.Type == Heif4CharCode.Uri)
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
            bytesRead += ParseBoxHeader(buffer[bytesRead..], out long subBoxLength, out Heif4CharCode linkType);
            uint sourceId = ReadUInt16Or32(buffer, largeIds, ref bytesRead);
            HeifItemLink link = new(linkType, sourceId);

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
        List<KeyValuePair<Heif4CharCode, object>> properties = new();
        long endBoxPosition = stream.Position + boxLength;
        while (stream.Position < endBoxPosition)
        {
            long containerLength = this.ReadBoxHeader(stream, out Heif4CharCode containerType);
            EnsureBoxBoundary(containerLength, boxLength);
            if (containerType == Heif4CharCode.Ipco)
            {
                // Parse Item Property Container, which is just an array of property boxes.
                this.ParsePropertyContainer(stream, containerLength, properties);
            }
            else if (containerType == Heif4CharCode.Ipma)
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

    private void ParsePropertyContainer(BufferedReadStream stream, long boxLength, List<KeyValuePair<Heif4CharCode, object>> properties)
    {
        long endPosition = stream.Position + boxLength;
        while (stream.Position < endPosition)
        {
            int itemLength = (int)this.ReadBoxHeader(stream, out Heif4CharCode itemType);
            EnsureBoxBoundary(itemLength, boxLength);
            Span<byte> buffer = this.ReadIntoBuffer(stream, itemLength);
            switch (itemType)
            {
                case Heif4CharCode.Ispe:
                    // Skip over version (8 bits) and flags (24 bits).
                    int width = (int)BinaryPrimitives.ReadUInt32BigEndian(buffer[4..]);
                    int height = (int)BinaryPrimitives.ReadUInt32BigEndian(buffer[8..]);
                    properties.Add(new KeyValuePair<Heif4CharCode, object>(Heif4CharCode.Ispe, new Size(width, height)));
                    break;
                case Heif4CharCode.Pasp:
                    int horizontalSpacing = (int)BinaryPrimitives.ReadUInt32BigEndian(buffer);
                    int verticalSpacing = (int)BinaryPrimitives.ReadUInt32BigEndian(buffer[4..]);
                    properties.Add(new KeyValuePair<Heif4CharCode, object>(Heif4CharCode.Pasp, new Size(horizontalSpacing, verticalSpacing)));
                    break;
                case Heif4CharCode.Pixi:
                    // Skip over version (8 bits) and flags (24 bits).
                    int channelCount = buffer[4];
                    int offset = 5;
                    int bitsPerPixel = 0;
                    for (int i = 0; i < channelCount; i++)
                    {
                        bitsPerPixel += buffer[offset + i];
                    }

                    properties.Add(new KeyValuePair<Heif4CharCode, object>(Heif4CharCode.Pixi, new int[] { channelCount, bitsPerPixel }));

                    break;
                case Heif4CharCode.Colr:
                    Heif4CharCode profileType = (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(buffer);
                    if (profileType is Heif4CharCode.RICC or Heif4CharCode.Prof)
                    {
                        byte[] iccData = new byte[itemLength - 4];
                        buffer[4..].CopyTo(iccData);
                        this.metadata.IccProfile = new IccProfile(iccData);
                    }

                    break;
                case Heif4CharCode.Altt:
                case Heif4CharCode.Imir:
                case Heif4CharCode.Irot:
                case Heif4CharCode.Iscl:
                case Heif4CharCode.HvcC:
                case Heif4CharCode.Av1C:
                case Heif4CharCode.Rloc:
                case Heif4CharCode.Udes:
                    // TODO: Implement
                    properties.Add(new KeyValuePair<Heif4CharCode, object>(itemType, new object()));
                    break;
                default:
                    throw new ImageFormatException($"Unknown item type in property box of '{PrettyPrint(itemType)}'");
            }
        }
    }

    private void ParsePropertyAssociation(BufferedReadStream stream, long boxLength, List<KeyValuePair<Heif4CharCode, object>> properties)
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

            KeyValuePair<Heif4CharCode, object> prop = properties[(int)propId];
            switch (prop.Key)
            {
                case Heif4CharCode.Ispe:
                    this.items[itemId].SetExtent((Size)prop.Value);
                    break;
                case Heif4CharCode.Pasp:
                    this.items[itemId].PixelAspectRatio = (Size)prop.Value;
                    break;
                case Heif4CharCode.Pixi:
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
            HeifItem? item = this.FindItemById(itemId);
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
                HeifLocation loc = new HeifLocation((long)extentOffset, (long)extentLength);
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
        HeifItemLink? thumbLink = this.itemLinks.FirstOrDefault(link => link.Type == Heif4CharCode.Thmb);
        if (thumbLink == null)
        {
            throw new NotImplementedException("No thumbnail found");
        }

        HeifItem? thumbItem = this.FindItemById(thumbLink.SourceId);
        if (thumbItem == null || thumbItem.Type != Heif4CharCode.Jpeg)
        {
            throw new NotImplementedException("No HVC decoding implemented yet");
        }

        int thumbFileOffset = (int)thumbItem.DataLocations[0].Offset;
        int thumbFileLength = (int)thumbItem.DataLocations[0].Length;
        stream.Skip((int)(thumbFileOffset - stream.Position));
        using IMemoryOwner<byte> thumbMemory = this.configuration.MemoryAllocator.Allocate<byte>(thumbFileLength);
        Span<byte> thumbSpan = thumbMemory.GetSpan();
        stream.Read(thumbSpan);

        HeifMetadata meta = this.metadata.GetHeifMetadata();
        meta.CompressionMethod = HeifCompressionMethod.LegacyJpeg;

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

    private HeifItem? FindItemById(uint itemId)
        => this.items.FirstOrDefault(item => item.Id == itemId);

    private static string ReadNullTerminatedString(Span<byte> span)
    {
        Span<byte> bytes = span[..span.IndexOf((byte)0)];
        return Encoding.UTF8.GetString(bytes);
    }

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
