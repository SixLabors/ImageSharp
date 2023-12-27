// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
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
    private ImageMetadata? metadata;

    private uint primaryItem;

    private List<HeicItem> items;

    private List<HeicItemLink> itemLinks;

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

        while (stream.EofHitCount == 0)
        {
            long length = this.ReadBoxHeader(stream, out Heic4CharCode boxType);
            switch (boxType)
            {
                case Heic4CharCode.meta:
                    this.ParseMetadata(stream, length);
                    break;
                case Heic4CharCode.mdat:
                    this.ParseMediaData(stream, length);
                    break;
                default:
                    throw new ImageFormatException($"Unknown box type of '{Enum.GetName(boxType)}'");
            }
        }

        HeicItem? item = this.FindItemById(this.primaryItem);
        if (item == null)
        {
            throw new ImageFormatException("No primary item found");
        }

        var image = new Image<TPixel>(this.configuration, item.Extent.Width, item.Extent.Height, this.metadata);

        Buffer2D<TPixel> pixels = image.GetRootFramePixelBuffer();

        return image;
    }

    /// <inheritdoc/>
    public ImageInfo Identify(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        this.CheckFileTypeBox(stream);

        while (stream.EofHitCount == 0)
        {
            long length = this.ReadBoxHeader(stream, out Heic4CharCode boxType);
            switch (boxType)
            {
                case Heic4CharCode.meta:
                    this.ParseMetadata(stream, length);
                    break;
                default:
                    // Silently skip all other box types.
                    break;
            }
        }

        HeicItem? item = this.FindItemById(this.primaryItem);
        if (item == null)
        {
            throw new ImageFormatException("No primary item found");
        }

        return new ImageInfo(new PixelTypeInfo(item.BitsPerPixel), new(item.Extent.Width, item.Extent.Height), this.metadata);
    }

    private bool CheckFileTypeBox(BufferedReadStream stream)
    {
        var boxLength = this.ReadBoxHeader(stream, out Heic4CharCode boxType);
        Span<byte> buffer = stackalloc byte[(int)boxLength];
        stream.Read(buffer);
        var majorBrand = BinaryPrimitives.ReadUInt32BigEndian(buffer);

        // TODO: Interpret minorVersion and compatible brands.
        return boxType == Heic4CharCode.ftyp && majorBrand == (uint)Heic4CharCode.heic;
    }

    private long ReadBoxHeader(BufferedReadStream stream, out Heic4CharCode boxType)
    {
        // Read 4 bytes of length of box
        Span<byte> buf = stackalloc byte[8];
        stream.Read(buf);
        long boxSize = BinaryPrimitives.ReadUInt32BigEndian(buf);
        long headerSize = 8;

        // Read 4 bytes of box type
        boxType = (Heic4CharCode)BinaryPrimitives.ReadUInt32BigEndian(buf[4..]);

        if (boxSize == 1)
        {
            stream.Read(buf);
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
        while (stream.Position < endPosition)
        {
            long length = this.ReadBoxHeader(stream, out Heic4CharCode boxType);
            switch (boxType)
            {
                case Heic4CharCode.iprp:
                    this.ParseItemPropertyContainer(stream, length);
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
                case Heic4CharCode.dinf:
                case Heic4CharCode.grpl:
                case Heic4CharCode.hdlr:
                case Heic4CharCode.idat:
                case Heic4CharCode.iloc:
                case Heic4CharCode.ipro:
                    // TODO: Implement
                    break;
                default:
                    throw new ImageFormatException($"Unknown metadata box type of '{Enum.GetName(boxType)}'");
            }
        }
    }

    private void ParseItemInfo(BufferedReadStream stream, long boxLength)
    {
        Span<byte> buffer = stackalloc byte[(int)boxLength];
        stream.Read(buffer);
        uint entryCount;
        int bytesRead = 0;
        if (buffer[bytesRead] == 0)
        {
            bytesRead += 4;
            entryCount = BinaryPrimitives.ReadUInt16BigEndian(buffer[bytesRead..]);
            bytesRead += 2;
        }
        else
        {
            bytesRead += 4;
            entryCount = BinaryPrimitives.ReadUInt32BigEndian(buffer[bytesRead..]);
            bytesRead += 4;
        }

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
            uint itemId = 0U;
            if (version == 2)
            {
                itemId = BinaryPrimitives.ReadUInt16BigEndian(buffer[bytesRead..]);
                bytesRead += 2;
            }
            else if (version == 3)
            {
                itemId = BinaryPrimitives.ReadUInt32BigEndian(buffer[bytesRead..]);
                bytesRead += 4;
            }

            // Skip Protection Index, not sure what that means...
            bytesRead += 2;
            uint itemType = BinaryPrimitives.ReadUInt32BigEndian(buffer[bytesRead..]);
            bytesRead += 4;
            item = new HeicItem((Heic4CharCode)itemId, itemType);
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

        return bytesRead;
    }

    private void ParseItemReference(BufferedReadStream stream, long boxLength)
    {
        Span<byte> buffer = new byte[boxLength];
        stream.Read(buffer);
        int bytesRead = 0;
        bool largeIds = buffer[bytesRead] != 0;
        bytesRead += 4;
        while (bytesRead < boxLength)
        {
            ParseBoxHeader(buffer[bytesRead..], out long subBoxLength, out Heic4CharCode linkType);
            uint sourceId;
            if (largeIds)
            {
                sourceId = BinaryPrimitives.ReadUInt32BigEndian(buffer[bytesRead..]);
                bytesRead += 4;
            }
            else
            {
                sourceId = BinaryPrimitives.ReadUInt16BigEndian(buffer[bytesRead..]);
                bytesRead += 2;
            }

            HeicItemLink link = new(linkType, sourceId);

            int count = BinaryPrimitives.ReadUInt16BigEndian(buffer[bytesRead..]);
            bytesRead += 2;
            for (uint i = 0; i < count; i++)
            {
                uint destId;
                if (largeIds)
                {
                    destId = BinaryPrimitives.ReadUInt32BigEndian(buffer[bytesRead..]);
                    bytesRead += 4;
                }
                else
                {
                    destId = BinaryPrimitives.ReadUInt16BigEndian(buffer[bytesRead..]);
                    bytesRead += 2;
                }

                link.DestinationIds.Add(destId);
            }

            this.itemLinks!.Add(link);
        }
    }

    private void ParsePrimaryItem(BufferedReadStream stream, long boxLength)
    {
        // BoxLength should be 6 or 8.
        Span<byte> buffer = stackalloc byte[(int)boxLength];
        stream.Read(buffer);
        byte version = buffer[0];
        if (version == 0)
        {
            this.primaryItem = BinaryPrimitives.ReadUInt16BigEndian(buffer[4..]);
        }
        else
        {
            this.primaryItem = BinaryPrimitives.ReadUInt32BigEndian(buffer[4..]);
        }
    }

    private void ParseItemPropertyContainer(BufferedReadStream stream, long boxLength)
    {
        // Cannot use Dictionary here, Properties can have multiple instances with the same key.
        List<KeyValuePair<Heic4CharCode, object>> properties = new();
        long containerLength = this.ReadBoxHeader(stream, out Heic4CharCode containerType);
        if (containerType == Heic4CharCode.ipco)
        {
            // Parse Item Property Container, which is just an array of preperty boxes.
            long endPosition = stream.Position + containerLength;
            while (stream.Position < endPosition)
            {
                int length = (int)this.ReadBoxHeader(stream, out Heic4CharCode boxType);
                Span<byte> buffer = stackalloc byte[length];
                switch (boxType)
                {
                    case Heic4CharCode.ispe:
                        // Length should be 12.
                        stream.Read(buffer);

                        // Skip over version (8 bits) and flags (24 bits).
                        int width = (int)BinaryPrimitives.ReadUInt32BigEndian(buffer[4..]);
                        int height = (int)BinaryPrimitives.ReadUInt32BigEndian(buffer[8..]);
                        properties.Add(new KeyValuePair<Heic4CharCode, object>(Heic4CharCode.ispe, new Size(width, height)));
                        break;
                    case Heic4CharCode.pasp:
                        // Length should be 8.
                        stream.Read(buffer);
                        int horizontalSpacing = (int)BinaryPrimitives.ReadUInt32BigEndian(buffer);
                        int verticalSpacing = (int)BinaryPrimitives.ReadUInt32BigEndian(buffer[4..]);
                        properties.Add(new KeyValuePair<Heic4CharCode, object>(Heic4CharCode.pasp, new Size(horizontalSpacing, verticalSpacing)));
                        break;
                    case Heic4CharCode.pixi:
                        stream.Read(buffer);

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
                    case Heic4CharCode.imir:
                    case Heic4CharCode.irot:
                    case Heic4CharCode.iscl:
                    case Heic4CharCode.rloc:
                    case Heic4CharCode.udes:
                        // TODO: Implement
                        break;
                    default:
                        throw new ImageFormatException($"Unknown item property box type of '{Enum.GetName(boxType)}'");
                }
            }
        }
        else if (containerType == Heic4CharCode.ipma)
        {
            // Parse Item Property Association
            Span<byte> buffer = stackalloc byte[(int)boxLength];
            byte version = buffer[0];
            byte flags = buffer[3];
            int itemId;
            int bytesRead = 4;
            if (version < 1)
            {
                itemId = BinaryPrimitives.ReadUInt16BigEndian(buffer[bytesRead..]);
                bytesRead += 2;
            }
            else
            {
                itemId = (int)BinaryPrimitives.ReadUInt32BigEndian(buffer[bytesRead..]);
                bytesRead += 4;
            }

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
    }

    private void ParseMediaData(BufferedReadStream stream, long boxLength)
    {
        // TODO: Implement
    }

    private HeicItem? FindItemById(uint itemId)
        => this.items.FirstOrDefault(item => item.Id == itemId);

    private static string ReadNullTerminatedString(Span<byte> span)
    {
        Span<byte> bytes = span[..span.IndexOf((byte)0)];
        return Encoding.UTF8.GetString(bytes);
    }
}
