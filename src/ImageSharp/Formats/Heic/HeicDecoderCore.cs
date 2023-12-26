// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

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
            throw new InvalidImageFormatException();
        }

        while (!stream.Eof)
        {
            var length = ReadBoxHeader(stream, out var boxType);
            switch (boxType)
            {
                case HeicBoxType.Meta:
                    ParseMetadata(stream, length);
                    break;
                case HeicBoxType.MediaData:
                    ParseMediaData(stream, length);
                    break;
                default:
                    throw new ImageFormatException($"Unknown box type of '{FourCharacterCode.ToString(boxType)}'");
            }
        }

        var image = new Image<TPixel>(this.configuration, this.pixelSize.Width, this.pixelSize.Height, this.metadata);

        Buffer2D<TPixel> pixels = image.GetRootFramePixelBuffer();

        return image;
    }

    /// <inheritdoc/>
    public ImageInfo Identify(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        this.CheckFileTypeBox(stream);

        while (!stream.Eof)
        {
            var length = ReadBoxHeader(stream, out var boxType);
            var buffer = new byte[length];
            stream.Read(buffer);
            switch (boxType)
            {
                case HeicBoxType.Metadata:
                    ParseMetadata(buffer);
                    break;
                default:
                    // Silently skip all other box types.
                    break;
            }
        }
        return new ImageInfo(new PixelTypeInfo(bitsPerPixel), new(this.pixelSize.Width, this.pixelSize.Height), this.metadata);
    }

    private bool CheckFileTypeBox(BufferedReadStream stream)
    {
        var boxLength = ReadBoxHeader(stream, out var boxType);
        Span<byte> buffer = stackalloc new byte[boxLength];
        stream.Read(buffer);
        var majorBrand = BinaryPrimitives.ReadUInt32BigEndian(buf);
        // TODO: Interpret minorVersion and compatible brands.
        return boxTypepe == HeicBoxType.FileType && majorBrand == HeicConstants.HeicBrand;
    }

    private ulong ReadBoxHeader(BufferedReadStream stream, out uint boxType)
    {
        // Read 4 bytes of length of box
        Span<byte> buf = stackalloc new byte[8];
        stream.Read(buf);
        ulong boxSize = BinaryPrimitives.ReadUInt32BigEndian(buf);
        ulong headerSize = 8;
        // Read 4 bytes of box type
        boxType = BinaryPrimitives.ReadUInt32BigEndian(buf.Slice(4));

        if (boxSize == 1)
        {
            stream.Read(buf);
            boxSize = BinaryPrimitives.ReadUInt64BigEndian(buf);
            headerSize += 8UL;
        }

        return boxSize - headerSize;
    }

    private uint ParseBoxHeader(Span<byte> buffer, out ulong length, out uint boxType)
    {
        ulong boxSize = BinaryPrimitives.ReadUInt32BigEndian(buffer);
        ulong bytesRead = 4;
        boxType = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(bytesRead));
        bytesRead += 4;
        if (boxSize == 1)
        {
            boxSize = BinaryPrimitives.ReadUInt64BigEndian(buffer.Slice(bytesRead));
            bytesRead += 8;
        }

        length = boxSize - bytesRead;
        return bytesRead;
    }

    private void ParseMetadata(BufferedReadStream stream, uint boxLength)
    {
        var endPosition = stream.Position + boxLength;
        while (stream.Position < endPosition)
        {
            var length = ReadBoxHeader(stream, out var boxType);
            switch (boxType)
            {
                case HeicMetaSubBoxType.ItemProperty:
                    ParseItemPropertyContainer(stream, length);
                    break;
                case HeicMetaSubBoxType.ItemInfo:
                    ParseItemInfo(stream, length);
                    break;
                case HeicMetaSubBoxType.ItemReference:
                    ParseItemReference(stream, length);
                    break;
                case HeicMetaSubBoxType.DataInformation:
                case HeicMetaSubBoxType.GroupsList:
                case HeicMetaSubBoxType.Handler:
                case HeicMetaSubBoxType.ItemData:
                case HeicMetaSubBoxType.ItemLocation:
                case HeicMetaSubBoxType.ItemProtection:
                case HeicMetaSubBoxType.PrimaryItem:
                    // TODO: Implement
                    break;
                default:
                    throw new ImageFormatException($"Unknown metadata box type of '{FourCharacterCode.ToString(boxType)}'");
            }
        }
    }

    private void ParseItemInfo(BufferedReadStream stream, uint boxLength)
    {
        Span<byte> buffer = new byte[length];
        stream.Read(buffer);
        uint entryCount;
        int bytesRead = 0;
        if (buffer[bytesRead] == 0)
        {
            bytesRead += 4;
            entryCount = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(bytesRead));
            bytesRead += 2;
        }
        else
        {
            bytesRead += 4;
            entryCount = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(bytesRead));
            bytesRead += 4;
        }

        for(uint i = 0; i < entryCount; i++)
        {
            bytesRead += ParseBoxHeader(out var subBoxLength, out var boxType);
            ParseItemInfoEntry(buffer.Slice(bytesRead, subBoxLength));
            bytesRead += subBoxLength;
        }
    }

    private void ParseItemInfoEntry(Span<byte> buffer, uint boxLength)
    {
        int bytesRead = 0;
        var version = buffer[bytesRead];
        bytesRead += 4;
        var item = new HeicItem();
        if (version == 0 || version == 1)
        {
            item.Id = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(bytesRead));
            bytesRead += 2;
            item.ProtectionIndex = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(bytesRead));
            bytesRead += 2;
            item.Name = ReadNullTerminatedString(buffer.Slice(bytesRead));
            bytesRead += item.Name.Length + 1;
            item.ContentType = ReadNullTerminatedString(buffer.Slice(bytesRead));
            bytesRead += item.ContentType.Length + 1;
            // Optional field.
            if (bytesRead < boxLength)
            {
                item.ContentEncoding = ReadNullTerminatedString(buffer.Slice(bytesRead));
                bytesRead += item.ContentEncoding.Length + 1;
            }
        }

        if (version == 1)
        {
            // Optional fields.
            if (bytesRead < boxLength)
            {
                item.ExtensionType = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(bytesRead));
                bytesRead += 4;
            }

            if (bytesRead < boxLength)
            {
                // TODO: Parse item.Extension
            }
        }

        if (version >= 2)
        {
            if (getVersion() == 2)
            {
                item.Id = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(bytesRead));
                bytesRead += 2;
            }
            else if (getVersion() == 3)
            {
                item.Id = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(bytesRead));
                bytesRead += 4;
            }

            item.ProtectionIndex = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(bytesRead));
            bytesRead += 2;
            item.Type = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(bytesRead));
            bytesRead += 4;
            item.Name = ReadNullTerminatedString(buffer.Slice(bytesRead));
            bytesRead += item.Name.Length + 1;
            if (item.Type == "mime")
            {
                item.ContentType = ReadNullTerminatedString(buffer.Slice(bytesRead));
                bytesRead += item.ContentType.Length + 1;
                // Optional field.
                if (bytesRead < boxLength)
                {
                    item.ContentEncoding = ReadNullTerminatedString(buffer.Slice(bytesRead));
                    bytesRead += item.ContentEncoding.Length + 1;
                }
            }
            else if (item.Type == "uri ")
            {
                item.UriType = ReadNullTerminatedString(buffer.Slice(bytesRead));
                bytesRead += item.ContentEncoding.Length + 1;
            }
        }
    }

    private void ParseItemReference(BufferedReadStream stream, uint boxLength)
    {
        Span<byte> buffer = new byte[length];
        stream.Read(buffer);
        int bytesRead = 0;
        bool largeIds = buffer[bytesRead] != 0;
        bytesRead += 4;
        while(bytesRead < boxLength)
        {
            ParseBoxHeader(buffer.Slice(bytesRead), out var subBoxLength, out var linkType);
            var link = new HeicItemLink();
            link.Type = linkType;
            if (largeIds)
            {
                link.Source = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(bytesRead));
                bytesRead += 4;
            }
            else
            {
                link.Source = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(bytesRead));
                bytesRead += 2;
            }

            var count = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(bytesRead));
            bytesRead += 2;
            for(uint i = 0; i < count; i++)
            {
                uint destId;
                if (largeIds)
                {
                    destId = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(bytesRead));
                    bytesRead += 4;
                }
                else
                {
                    destId = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(bytesRead));
                    bytesRead += 2;
                }
                link.Destinations.Add(destId);
            }

            itemLinks.Add(link);
        }
    }

    private void ParseItemPropertyContainer(BufferedReadStream stream, uint boxLength)
    {
        var containerLength = ReadBoxHeader(stream, out var containerType);
        if (containerType == FourCharacterCode.ipco)
        {
            // Parse Item Property Container, which is just an array of preperty boxes.
            var endPosition = stream.Position + containerLength;
            while (stream.Position < endPosition)
            {
                var length = ReadBoxHeader(stream, out var boxType);
                switch (boxType)
                {
                    case HeicItemPropertyType.ImageSpatialExtents:
                        // Length should be 12.
                        Span<byte> buffer = stackalloc new byte[length];
                        stream.Read(buffer);
                        // Skip over version (8 bits) and flags (24 bits).
                        var width = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(4));
                        var height = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(8));
                        break;
                    case HeicItemPropertyType.PixelAspectRatio:
                        // Length should be 8.
                        Span<byte> buffer = stackalloc new byte[length];
                        stream.Read(buffer);
                        var horizontalSpacing = BinaryPrimitives.ReadUInt32BigEndian(buffer);
                        var verticalSpacing = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(4));
                        break;
                    case HeicItemPropertyType.PixelInformation:
                        Span<byte> buffer = stackalloc new byte[length];
                        stream.Read(buffer);
                        // Skip over version (8 bits) and flags (24 bits).
                        var channelCount = buffer[4];
                        int offset = 5;
                        int bitsPerPixel = 0;
                        for (int i = 0; i < channelCount; i++)
                        {
                            bitsPerPixel += buffer[i];
                        }
                        break;
                    case HeicItemPropertyType.AcessibilityText:
                    case HeicItemPropertyType.ImageMirror:
                    case HeicItemPropertyType.ImageRotation:
                    case HeicItemPropertyType.ImageScaling:
                    case HeicItemPropertyType.RelativeLocation:
                    case HeicItemPropertyType.UserDescription;
                        // TODO: Implement
                        break;
                    default:
                        throw new ImageFormatException($"Unknown item property box type of '{FourCharacterCode.ToString(boxType)}'");
                }
            }
        }
        else if (containerType == FourCharacterCode.ipma)
        {
            // Parse Item Property Association
        }
    }

    private void ParseMediaData(BufferedReadStream stream, uint boxLength)
    {
        // TODO: Implement
    }

    /// <summary>
    /// Forwards the stream to just past the Start of NAL marker.
    /// </summary>
    private void FindStartOfNal(BufferedReadStream stream)
    {
        uint i = stream.Position;
        uint length = 0;
        var dataLength = stream.Length;

        while (i < streamLength)
        {
            var current = stream.ReadByte();
            if (current == 0)
            {
                length++;
            }
            else if (length > 1 && current == 1)
            {
                // Found the marker !
                //length++;
                break;
            }
            else
            {
                // False alarm, resetting...
                length = 0;
            }
            i++;
        }
    }
}
