// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Validates and reads the extents of HEIF items. The decoder for still images and the parser for track metadata use this type.
/// </summary>
/// <remarks>
/// ISO/IEC 14496-12, section 8.11.3: construction method 0 gives offsets from the start of the file. Construction method 1 gives offsets from the start of the
/// item data box payload. An item is the concatenation of its extents in the declared order.
/// </remarks>
internal readonly struct HeifItemDataSource
{
    /// <summary>
    /// The absolute stream position at which the HEIF file begins.
    /// </summary>
    private readonly long fileStartOffset;

    /// <summary>
    /// The number of file bytes from <see cref="fileStartOffset"/> to the end of the stream.
    /// </summary>
    private readonly long fileLength;

    /// <summary>
    /// The stream range of the item data box payload. The range is not present when the file has no item data box.
    /// </summary>
    private readonly HeifBoxRange itemData;

    /// <summary>
    /// Initializes a new instance of the <see cref="HeifItemDataSource"/> struct.
    /// </summary>
    /// <param name="fileStartOffset">The absolute stream position at which the HEIF file begins.</param>
    /// <param name="streamLength">The length of the stream.</param>
    /// <param name="itemData">The stream range of the item data box payload. The range is not present when there is no item data box.</param>
    public HeifItemDataSource(long fileStartOffset, long streamLength, HeifBoxRange itemData)
    {
        this.fileStartOffset = fileStartOffset;
        this.fileLength = streamLength - fileStartOffset;
        this.itemData = itemData;
    }

    /// <summary>
    /// Validates the extents of one item before the caller allocates its buffer, and sets <see cref="HeifItem.DataLength"/>.
    /// </summary>
    /// <remarks>
    /// The item buffer holds only the bytes that the file holds. Thus a small file cannot request a buffer of up to <see cref="int.MaxValue"/> bytes. The item
    /// data ends where the first truncated extent ends, because the bytes of the later extents do not follow it. Only a file extent that ends after the end of
    /// the file is a truncation. The item data box is complete. Thus an extent outside it is a structural error.
    /// </remarks>
    /// <param name="item">The item to validate.</param>
    /// <returns>
    /// <see langword="true"/> when the file holds all of the declared item bytes. <see langword="false"/> when the item is truncated or declares no bytes. The
    /// caller reports this result through the integrity mode of the item data.
    /// </returns>
    /// <exception cref="InvalidImageContentException">An extent is outside its item data box, or the item is too large.</exception>
    public bool ValidateExtents(HeifItem item)
    {
        long declaredLength = 0;
        long readableLength = 0;
        bool complete = true;
        foreach (HeifLocation location in item.DataLocations)
        {
            // The item location parser limits each field to long.MaxValue. Thus only the sum of the fields can overflow.
            if (location.BaseOffset > long.MaxValue - location.Offset)
            {
                throw new InvalidImageContentException($"Item {item.Id} has an invalid extent offset.");
            }

            long relativeOffset = location.BaseOffset + location.Offset;
            long sourceLength = this.fileLength;
            if (location.Origin == HeifLocationOffsetOrigin.ItemDataOffset)
            {
                if (!this.itemData.IsPresent || relativeOffset > this.itemData.Length || location.Length > this.itemData.Length - relativeOffset)
                {
                    throw new InvalidImageContentException($"Item {item.Id} has an extent outside its item data box.");
                }

                sourceLength = this.itemData.Length;
            }

            if (declaredLength > int.MaxValue - location.Length)
            {
                throw new InvalidImageContentException($"Item {item.Id} data is too large to buffer.");
            }

            declaredLength += location.Length;
            if (complete)
            {
                long availableLength = relativeOffset >= sourceLength ? 0 : Math.Min(location.Length, sourceLength - relativeOffset);
                readableLength += availableLength;
                complete = availableLength == location.Length;
            }
        }

        item.DataLength = (int)readableLength;
        return complete && declaredLength > 0;
    }

    /// <summary>
    /// Reads the bytes of one item that <see cref="ValidateExtents"/> validated.
    /// </summary>
    /// <param name="stream">The complete seekable stream.</param>
    /// <param name="item">The validated item.</param>
    /// <param name="destination">The destination of exactly <see cref="HeifItem.DataLength"/> bytes.</param>
    public void Read(Stream stream, HeifItem item, Span<byte> destination)
    {
        DebugGuard.IsTrue(destination.Length == item.DataLength, nameof(destination), "The destination must hold the validated item length.");
        int written = 0;
        foreach (HeifLocation location in item.DataLocations)
        {
            if (written == destination.Length)
            {
                break;
            }

            // If an extent is truncated, the validated length ends inside it. Thus the last extent that this method reads can be partial.
            int length = (int)Math.Min(location.Length, destination.Length - written);
            long origin = location.Origin == HeifLocationOffsetOrigin.FileOffset ? this.fileStartOffset : this.itemData.Offset;
            stream.Position = origin + location.BaseOffset + location.Offset;
            HeifBoxReader.ReadExactly(stream, destination.Slice(written, length), "An item extent is truncated.");
            written += length;
        }
    }
}
