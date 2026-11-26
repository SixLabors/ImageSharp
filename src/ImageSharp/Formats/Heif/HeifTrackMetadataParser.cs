// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Parses the Exif and XMP items implicitly associated with one HEIF image-sequence track.
/// </summary>
/// <remarks>
/// The track metadata box holds item declarations and item locations in the same layout as the still-image metadata box. The parser uses the item-information
/// and item-location parsers of <see cref="HeifItemBoxParser"/>. It reads the items through <see cref="HeifItemDataSource"/>.
/// </remarks>
internal sealed class HeifTrackMetadataParser
{
    /// <summary>
    /// The shared reader for box headers and payloads.
    /// </summary>
    private readonly HeifBoxReader boxReader;

    /// <summary>
    /// The segment integrity mode. The parser reports metadata item errors as ancillary errors.
    /// </summary>
    private readonly SegmentIntegrityHandling handling;

    /// <summary>
    /// Initializes a new instance of the <see cref="HeifTrackMetadataParser"/> class.
    /// </summary>
    /// <param name="boxReader">The shared reader for box headers and payloads.</param>
    /// <param name="handling">The segment integrity mode.</param>
    public HeifTrackMetadataParser(HeifBoxReader boxReader, SegmentIntegrityHandling handling)
    {
        this.boxReader = boxReader;
        this.handling = handling;
    }

    /// <summary>
    /// Gets the child boxes of a track metadata box that the parser reads. The parser requires the first child box.
    /// </summary>
    private static ReadOnlySpan<Heif4CharCode> MetadataChildren => [Heif4CharCode.Hdlr, Heif4CharCode.Iinf, Heif4CharCode.Iloc, Heif4CharCode.Idat];

    /// <summary>
    /// Parses the bounded metadata items from one track-level metadata box.
    /// </summary>
    /// <param name="stream">The seekable HEIF stream positioned at the metadata full-box header.</param>
    /// <param name="boxLength">The validated metadata payload length.</param>
    /// <param name="headerBuffer">The caller-owned box header buffer of at least eight bytes.</param>
    /// <param name="fileStartOffset">The absolute stream position at which the HEIF file begins.</param>
    /// <returns>The retained Exif and XMP payloads.</returns>
    public HeifSequenceMetadata Parse(Stream stream, long boxLength, Span<byte> headerBuffer, long fileStartOffset)
    {
        long metadataEnd = stream.Position + boxLength;
        HeifItemBoxParser.ReadMetadataHeader(stream, boxLength, headerBuffer, 0, "track metadata");

        // The parser requires the handler box as the first child, so that the handler type selects the metadata model.
        long firstChildStart = stream.Position;
        if (firstChildStart < metadataEnd)
        {
            HeifBoxReader.ReadHeader(stream, metadataEnd, headerBuffer, out Heif4CharCode firstChildType, false);
            if (firstChildType != Heif4CharCode.Hdlr)
            {
                throw new InvalidImageContentException("The track metadata box does not begin with its picture handler.");
            }

            stream.Position = firstChildStart;
        }

        Span<HeifBoxRange> children = stackalloc HeifBoxRange[MetadataChildren.Length];
        HeifBoxReader.IndexChildren(stream, metadataEnd - firstChildStart, headerBuffer, MetadataChildren, children, 1, "track metadata");
        using (IMemoryOwner<byte> handler = this.boxReader.ReadPayload(stream, children[0]))
        {
            if (HeifItemBoxParser.ParseHandler(handler.GetSpan()) != Heif4CharCode.Pict)
            {
                throw new InvalidImageContentException("The track metadata box does not use the picture handler.");
            }
        }

        if (!children[1].IsPresent && !children[2].IsPresent)
        {
            return new HeifSequenceMetadata(null, null);
        }

        if (!children[1].IsPresent || !children[2].IsPresent)
        {
            throw new InvalidImageContentException("The track metadata box has incomplete item declarations or locations.");
        }

        // The item locations refer to the declared items. Thus the parser reads the item declarations first.
        Dictionary<uint, HeifItem> items = [];
        using (IMemoryOwner<byte> itemInformation = this.boxReader.ReadPayload(stream, children[1]))
        {
            HeifItemBoxParser.ParseItemInfo(itemInformation.GetSpan(), items);
        }

        using (IMemoryOwner<byte> itemLocations = this.boxReader.ReadPayload(stream, children[2]))
        {
            HeifItemBoxParser.ParseItemLocation(itemLocations.GetSpan(), items);
        }

        HeifItem? exifItem = null;
        HeifItem? xmpItem = null;
        foreach (HeifItem item in items.Values)
        {
            if (item.Type == Heif4CharCode.Exif)
            {
                if (exifItem is not null)
                {
                    throw new InvalidImageContentException("The image-sequence track declares more than one Exif metadata item.");
                }

                exifItem = item;
            }
            else if (item.Type == Heif4CharCode.Mime
                && string.Equals(item.ContentType, "application/rdf+xml", StringComparison.Ordinal)
                && string.IsNullOrEmpty(item.ContentEncoding))
            {
                // ImageSharp cannot decode an encoded MIME payload. Thus the parser keeps an XMP item only when its optional encoding is empty.
                if (xmpItem is not null)
                {
                    throw new InvalidImageContentException("The image-sequence track declares more than one XMP metadata item.");
                }

                xmpItem = item;
            }
        }

        // ISO/IEC 14496-12, section 8.11.3: construction method 0 gives offsets from the start of the file. Construction method 1 gives offsets from the start
        // of the item data box payload of this metadata box.
        HeifItemDataSource source = new(fileStartOffset, stream.Length, children[3]);

        return new HeifSequenceMetadata(this.ReadItem(stream, source, exifItem), this.ReadItem(stream, source, xmpItem));
    }

    /// <summary>
    /// Validates and reads one recognized metadata item.
    /// </summary>
    /// <param name="stream">The seekable HEIF stream.</param>
    /// <param name="source">The item data source of the track metadata box.</param>
    /// <param name="item">The metadata item, or <see langword="null"/>.</param>
    /// <returns>
    /// The item payload, or <see langword="null"/> when there is no item or the mode ignores a truncated or empty item.
    /// </returns>
    private byte[]? ReadItem(Stream stream, HeifItemDataSource source, HeifItem? item)
    {
        if (item is null)
        {
            return null;
        }

        if (!source.ValidateExtents(item))
        {
            this.handling.ThrowOrIgnoreAncillaryError($"The data of track metadata item {item.Id} is truncated or empty.");
            return null;
        }

        // The payload survives parser disposal and is handed directly to ImageSharp's metadata profile model.
        byte[] data = new byte[item.DataLength];
        source.Read(stream, item, data);
        return data;
    }
}
