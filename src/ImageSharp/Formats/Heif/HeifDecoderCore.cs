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

    private Av1CodecConfiguration av1CodecConfiguration;

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
    protected override ImageInfo Identify(BufferedReadStream stream, CancellationToken cancellationToken)
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

        return new ImageInfo(new(item.Extent.Width, item.Extent.Height), this.metadata);
    }

    private bool CheckFileTypeBox(BufferedReadStream stream)
    {
        long boxLength = this.ReadBoxHeader(stream, out Heif4CharCode boxType);
        using IMemoryOwner<byte> boxMemory = this.ReadIntoBuffer(stream, boxLength);
        Span<byte> boxBuffer = boxMemory.GetSpan();
        uint majorBrand = BinaryPrimitives.ReadUInt32BigEndian(boxBuffer);
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
        Span<byte> buf = stackalloc byte[8];
        int bytesRead = stream.Read(buf);
        if (bytesRead != 8)
        {
            throw new InvalidImageContentException("Not enough data to read the Box header");
        }

        long boxSize = BinaryPrimitives.ReadUInt32BigEndian(buf);
        long headerSize = 8;

        // Read 4 bytes of box type
        boxType = (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(buf[4..]);

        if (boxSize == 1)
        {
            bytesRead = stream.Read(buf);
            if (bytesRead != 8)
            {
                throw new InvalidImageContentException("Not enough data to read the Large Box header");
            }

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
            EnsureBoxInsideParent(length, boxLength);
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
        using IMemoryOwner<byte> boxMemory = this.ReadIntoBuffer(stream, boxLength);
        Span<byte> boxBuffer = boxMemory.GetSpan();

        // Only read the handler type, to check if this is not a movie file.
        int bytesRead = 8;
        Heif4CharCode handlerType = (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(boxBuffer[bytesRead..]);
        if (handlerType != Heif4CharCode.Pict)
        {
            throw new ImageFormatException("Not a picture file.");
        }
    }

    private void ParseItemInfo(BufferedReadStream stream, long boxLength)
    {
        using IMemoryOwner<byte> boxMemory = this.ReadIntoBuffer(stream, boxLength);
        Span<byte> boxBuffer = boxMemory.GetSpan();
        uint entryCount;
        int bytesRead = 0;
        byte version = boxBuffer[bytesRead];
        bytesRead += 4;
        entryCount = ReadUInt16Or32(boxBuffer, version != 0, ref bytesRead);

        for (uint i = 0; i < entryCount; i++)
        {
            bytesRead += this.ParseItemInfoEntry(boxBuffer[bytesRead..]);
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
        using IMemoryOwner<byte> boxMemory = this.ReadIntoBuffer(stream, boxLength);
        Span<byte> boxBuffer = boxMemory.GetSpan();
        int bytesRead = 0;
        bool largeIds = boxBuffer[bytesRead] != 0;
        bytesRead += 4;
        while (bytesRead < boxLength)
        {
            bytesRead += ParseBoxHeader(boxBuffer[bytesRead..], out long subBoxLength, out Heif4CharCode linkType);
            uint sourceId = ReadUInt16Or32(boxBuffer, largeIds, ref bytesRead);
            HeifItemLink link = new(linkType, sourceId);

            int count = BinaryPrimitives.ReadUInt16BigEndian(boxBuffer[bytesRead..]);
            bytesRead += 2;
            for (uint i = 0; i < count; i++)
            {
                uint destId = ReadUInt16Or32(boxBuffer, largeIds, ref bytesRead);
                link.DestinationIds.Add(destId);
            }

            this.itemLinks!.Add(link);
        }
    }

    private void ParsePrimaryItem(BufferedReadStream stream, long boxLength)
    {
        // BoxLength should be 6 or 8.
        using IMemoryOwner<byte> boxMemory = this.ReadIntoBuffer(stream, boxLength);
        Span<byte> boxBuffer = boxMemory.GetSpan();
        byte version = boxBuffer[0];
        int bytesRead = 4;
        this.primaryItem = ReadUInt16Or32(boxBuffer, version != 0, ref bytesRead);
    }

    private void ParseItemProperties(BufferedReadStream stream, long boxLength)
    {
        // Cannot use Dictionary here, Properties can have multiple instances with the same key.
        List<KeyValuePair<Heif4CharCode, object>> properties = new();
        long endBoxPosition = stream.Position + boxLength;
        while (stream.Position < endBoxPosition)
        {
            long containerLength = this.ReadBoxHeader(stream, out Heif4CharCode containerType);
            EnsureBoxInsideParent(containerLength, boxLength);
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
            EnsureBoxInsideParent(itemLength, boxLength);
            using IMemoryOwner<byte> boxMemory = this.ReadIntoBuffer(stream, itemLength);
            Span<byte> boxBuffer = boxMemory.GetSpan();
            switch (itemType)
            {
                case Heif4CharCode.Ispe:
                    // Skip over version (8 bits) and flags (24 bits).
                    int width = (int)BinaryPrimitives.ReadUInt32BigEndian(boxBuffer[4..]);
                    int height = (int)BinaryPrimitives.ReadUInt32BigEndian(boxBuffer[8..]);
                    properties.Add(new KeyValuePair<Heif4CharCode, object>(Heif4CharCode.Ispe, new Size(width, height)));
                    break;
                case Heif4CharCode.Pasp:
                    int horizontalSpacing = (int)BinaryPrimitives.ReadUInt32BigEndian(boxBuffer);
                    int verticalSpacing = (int)BinaryPrimitives.ReadUInt32BigEndian(boxBuffer[4..]);
                    properties.Add(new KeyValuePair<Heif4CharCode, object>(Heif4CharCode.Pasp, new Size(horizontalSpacing, verticalSpacing)));
                    break;
                case Heif4CharCode.Pixi:
                    // Skip over version (8 bits) and flags (24 bits).
                    int channelCount = boxBuffer[4];
                    int offset = 5;
                    int bitsPerPixel = 0;
                    for (int i = 0; i < channelCount; i++)
                    {
                        bitsPerPixel += boxBuffer[offset + i];
                    }

                    properties.Add(new KeyValuePair<Heif4CharCode, object>(Heif4CharCode.Pixi, new int[] { channelCount, bitsPerPixel }));

                    break;
                case Heif4CharCode.Colr:
                    Heif4CharCode profileType = (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(boxBuffer);
                    if (profileType is Heif4CharCode.RICC or Heif4CharCode.Prof)
                    {
                        byte[] iccData = new byte[itemLength - 4];
                        boxBuffer[4..].CopyTo(iccData);
                        this.metadata.IccProfile = new IccProfile(iccData);
                    }

                    break;
                case Heif4CharCode.Av1C:
                    this.av1CodecConfiguration = new(boxBuffer);
                    break;
                case Heif4CharCode.Altt:
                case Heif4CharCode.Imir:
                case Heif4CharCode.Irot:
                case Heif4CharCode.Iscl:
                case Heif4CharCode.HvcC:
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
        using IMemoryOwner<byte> boxMemory = this.ReadIntoBuffer(stream, boxLength);
        Span<byte> boxBuffer = boxMemory.GetSpan();
        byte version = boxBuffer[0];
        byte flags = boxBuffer[3];
        int bytesRead = 4;
        int itemId = (int)ReadUInt16Or32(boxBuffer, version >= 1, ref bytesRead);

        int associationCount = boxBuffer[bytesRead++];
        for (int i = 0; i < associationCount; i++)
        {
            uint propId;
            if (flags == 1)
            {
                propId = BinaryPrimitives.ReadUInt16BigEndian(boxBuffer[bytesRead..]) & 0x4FFFU;
                bytesRead += 2;
            }
            else
            {
                propId = boxBuffer[bytesRead++] & 0x4FU;
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
        using IMemoryOwner<byte> boxMemory = this.ReadIntoBuffer(stream, boxLength);
        Span<byte> boxBuffer = boxMemory.GetSpan();
        int bytesRead = 0;
        byte version = boxBuffer[bytesRead];
        bytesRead += 4;
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

        uint itemCount = ReadUInt16Or32(boxBuffer, version == 2, ref bytesRead);
        for (uint i = 0; i < itemCount; i++)
        {
            uint itemId = ReadUInt16Or32(boxBuffer, version == 2, ref bytesRead);
            HeifItem? item = this.FindItemById(itemId);
            HeifLocationOffsetOrigin constructionMethod = HeifLocationOffsetOrigin.FileOffset;
            if (version is 1 or 2)
            {
                bytesRead++;
                byte b3 = boxBuffer[bytesRead];
                bytesRead++;
                constructionMethod = (HeifLocationOffsetOrigin)(b3 & 0x0f);
            }

            uint dataReferenceIndex = BinaryPrimitives.ReadUInt16BigEndian(boxBuffer[bytesRead..]);
            bytesRead += 2;
            long baseOffset = ReadUIntVariable(boxBuffer, baseOffsetSize, ref bytesRead);
            uint extentCount = BinaryPrimitives.ReadUInt16BigEndian(boxBuffer[bytesRead..]);
            bytesRead += 2;
            for (uint j = 0; j < extentCount; j++)
            {
                uint extentIndex = 0;
                if (version is 1 or 2 && indexSize > 0)
                {
                    extentIndex = (uint)ReadUIntVariable(boxBuffer, indexSize, ref bytesRead);
                }

                long extentOffset = ReadUIntVariable(boxBuffer, offsetSize, ref bytesRead);
                long extentLength = ReadUIntVariable(boxBuffer, lengthSize, ref bytesRead);
                HeifLocation loc = new(constructionMethod, baseOffset, extentOffset, extentLength);
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

    private static long ReadUIntVariable(Span<byte> buffer, int numBytes, ref int bytesRead)
    {
        long result = 0L;
        int shift = 0;
        if (numBytes > 8)
        {
            throw new InvalidImageContentException($"Can't store large integer of {numBytes * 8} bits.");
        }
        else
        if (numBytes > 4)
        {
            result = (long)BinaryPrimitives.ReadUInt64BigEndian(buffer[bytesRead..]);
            shift = 8 - numBytes;
        }
        else if (numBytes > 2)
        {
            result = BinaryPrimitives.ReadUInt32BigEndian(buffer[bytesRead..]);
            shift = 4 - numBytes;
        }
        else if (numBytes > 1)
        {
            result = BinaryPrimitives.ReadUInt16BigEndian(buffer[bytesRead..]);
        }
        else if (numBytes == 1)
        {
            result = buffer[bytesRead];
        }

        bytesRead += numBytes;
        result >>= shift << 3;
        return result;
    }

    private Image<TPixel> ParseMediaData<TPixel>(Stream stream, long boxLength)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        EnsureBoxBoundary(boxLength, stream);

        IComparer<HeifLocation> comparer = new HeifLocationComparer(stream.Position, stream.Position);
        SortedList<HeifLocation, HeifItem> locations = new(comparer);
        foreach (HeifItem item in this.items)
        {
            HeifLocation loc = item.DataLocations[0];
            if (loc.Length != 0)
            {
                locations[loc] = item;
            }
        }

        using DisposableDictionary<uint, IMemoryOwner<byte>> buffers = new(locations.Count);
        foreach (HeifLocation loc in locations.Keys)
        {
            HeifItem item = locations[loc];
            long streamPosition = loc.GetStreamPosition(stream.Position, stream.Position);
            long dataLength = loc.Length;
            stream.Skip((int)(streamPosition - stream.Position));
            EnsureBoxBoundary(dataLength, stream);
            buffers.Add(item.Id, this.ReadIntoBuffer(stream, dataLength));
        }

        HeifItem? rootItem = this.FindItemById(this.primaryItem);
        if (rootItem == null)
        {
            throw new ImageFormatException("No primary HEIF item defined.");
        }

        IHeifItemDecoder<TPixel>? itemDecoder;
        if (rootItem.Type == Heif4CharCode.Grid)
        {
            itemDecoder = new GridHeifItemDecoder<TPixel>(this.configuration, this.items, this.itemLinks, buffers);
        }

        itemDecoder = HeifCompressionFactory.GetDecoder<TPixel>(rootItem.Type);
        HeifItem itemToDecode = rootItem;
        if (itemDecoder == null)
        {
            // Unable to decode the primary image, decode the thumbnail instead.
            HeifItemLink? thumbLink = this.itemLinks.FirstOrDefault(link => link.Type == Heif4CharCode.Thmb);
            if (thumbLink != null)
            {
                HeifItem? thumbItem = this.FindItemById(thumbLink.SourceId);
                if (thumbItem != null)
                {
                    itemDecoder = HeifCompressionFactory.GetDecoder<TPixel>(thumbItem.Type);
                    if (itemDecoder != null)
                    {
                        itemToDecode = thumbItem;
                    }
                }
            }
        }

        if (itemDecoder == null)
        {
            throw new ImageFormatException("No decodable item found inside this HEIF container.");
        }

        HeifMetadata meta = this.metadata.GetHeifMetadata();
        meta.CompressionMethod = itemDecoder.CompressionMethod;

        IMemoryOwner<byte> itemMemory = buffers[itemToDecode.Id];
        return itemDecoder.DecodeItemData(this.configuration, itemToDecode, itemMemory.GetSpan());
    }

    private static void SkipBox(Stream stream, long boxLength)
        => stream.Skip((int)boxLength);

    private IMemoryOwner<byte> ReadIntoBuffer(Stream stream, long length)
    {
        IMemoryOwner<byte> buffer = this.configuration.MemoryAllocator.Allocate<byte>((int)length);
        int bytesRead = stream.Read(buffer.GetSpan());
        if (bytesRead != length)
        {
            throw new InvalidImageContentException("Stream length not sufficient for box content");
        }

        return buffer;
    }

    private static void EnsureBoxBoundary(long boxLength, Stream stream)
        => EnsureBoxInsideParent(boxLength, stream.Length - stream.Position);

    private static void EnsureBoxInsideParent(long boxLength, long parentLength)
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
