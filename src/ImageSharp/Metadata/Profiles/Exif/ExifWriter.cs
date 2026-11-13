// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif;

/// <summary>
/// Contains methods for writing EXIF metadata.
/// </summary>
internal sealed class ExifWriter
{
    /// <summary>
    /// The size of the tag, type, and component count fields in a classic directory entry.
    /// </summary>
    internal const int DirectoryEntryHeaderLength = 8;

    /// <summary>
    /// Which parts will be written.
    /// </summary>
    private readonly ExifParts allowedParts;
    private readonly IList<IExifValue> values;
    private List<int>? dataOffsets;
    private readonly List<IExifValue> ifdValues;
    private readonly List<IExifValue> exifValues;
    private readonly List<IExifValue> gpsValues;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExifWriter"/> class.
    /// </summary>
    /// <param name="values">The values.</param>
    /// <param name="allowedParts">The allowed parts.</param>
    public ExifWriter(IList<IExifValue> values, ExifParts allowedParts)
    {
        this.values = values;
        this.allowedParts = allowedParts;
        this.ifdValues = this.GetPartValues(ExifParts.IfdTags);
        this.exifValues = this.GetPartValues(ExifParts.ExifTags);
        this.gpsValues = this.GetPartValues(ExifParts.GpsTags);
    }

    /// <summary>
    /// Returns the EXIF data.
    /// </summary>
    /// <returns>
    /// The <see cref="T:byte[]"/>.
    /// </returns>
    public byte[] GetData()
    {
        const uint startIndex = 0;

        IExifValue? exifOffset = GetOffsetValue(this.ifdValues, this.exifValues, ExifTag.SubIFDOffset);
        IExifValue? gpsOffset = GetOffsetValue(this.ifdValues, this.gpsValues, ExifTag.GPSIFDOffset);

        ulong ifdLength = GetLength(this.ifdValues);
        ulong exifLength = GetLength(this.exifValues);
        ulong gpsLength = GetLength(this.gpsValues);

        ulong length = ifdLength + exifLength + gpsLength;

        if (length == 0)
        {
            return Array.Empty<byte>();
        }

        // two bytes for the byte Order marker 'II' or 'MM', followed by the number 42 (0x2A) and a 0, making 4 bytes total
        length += (uint)ExifConstants.LittleEndianByteOrderMarker.Length;

        // first IFD offset
        length += 4;

        if (length > (ulong)Array.MaxLength)
        {
            throw new ImageFormatException("The EXIF profile exceeds the supported size.");
        }

        byte[] result = new byte[(int)length];

        int i = 0;

        // The byte order marker for little-endian, followed by the number 42 and a 0
        ExifConstants.LittleEndianByteOrderMarker.CopyTo(result.AsSpan(start: i));
        i += ExifConstants.LittleEndianByteOrderMarker.Length;

        uint ifdOffset = (uint)i - startIndex + 4U;

        // The full profile fits in the output array, so its directory offsets fit in four bytes.
        exifOffset?.TrySetValue(ifdOffset + (uint)ifdLength);
        gpsOffset?.TrySetValue(ifdOffset + (uint)(ifdLength + exifLength));

        i = WriteUInt32(ifdOffset, result, i);
        i = this.WriteHeaders(this.ifdValues, result, i);
        i = this.WriteData(startIndex, this.ifdValues, result, i);

        if (exifLength > 0)
        {
            i = this.WriteHeaders(this.exifValues, result, i);
            i = this.WriteData(startIndex, this.exifValues, result, i);
        }

        if (gpsLength > 0)
        {
            i = this.WriteHeaders(this.gpsValues, result, i);
            this.WriteData(startIndex, this.gpsValues, result, i);
        }

        return result;
    }

    private static unsafe int WriteSingle(float value, Span<byte> destination, int offset)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(offset, 4), *(int*)&value);

        return offset + 4;
    }

    private static unsafe int WriteDouble(double value, Span<byte> destination, int offset)
    {
        BinaryPrimitives.WriteInt64LittleEndian(destination.Slice(offset, 8), *(long*)&value);

        return offset + 8;
    }

    private static int Write(ReadOnlySpan<byte> source, Span<byte> destination, int offset)
    {
        source.CopyTo(destination.Slice(offset, source.Length));

        return offset + source.Length;
    }

    private static int WriteInt16(short value, Span<byte> destination, int offset)
    {
        BinaryPrimitives.WriteInt16LittleEndian(destination.Slice(offset, 2), value);

        return offset + 2;
    }

    private static int WriteUInt16(ushort value, Span<byte> destination, int offset)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(offset, 2), value);

        return offset + 2;
    }

    private static int WriteUInt32(uint value, Span<byte> destination, int offset)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(offset, 4), value);

        return offset + 4;
    }

    private static int WriteInt64(long value, Span<byte> destination, int offset)
    {
        BinaryPrimitives.WriteInt64LittleEndian(destination.Slice(offset, 8), value);

        return offset + 8;
    }

    private static int WriteUInt64(ulong value, Span<byte> destination, int offset)
    {
        BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(offset, 8), value);

        return offset + 8;
    }

    private static int WriteInt32(int value, Span<byte> destination, int offset)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(offset, 4), value);

        return offset + 4;
    }

    private static IExifValue? GetOffsetValue(List<IExifValue> ifdValues, List<IExifValue> values, ExifTag offset)
    {
        int index = -1;

        for (int i = 0; i < ifdValues.Count; i++)
        {
            if (ifdValues[i].Tag == offset)
            {
                index = i;
            }
        }

        if (values.Count > 0)
        {
            if (index != -1)
            {
                return ifdValues[index];
            }

            ExifValue? result = ExifValues.Create(offset);

            if (result is not null)
            {
                ifdValues.Add(result);
            }

            return result;
        }
        else if (index != -1)
        {
            ifdValues.RemoveAt(index);
        }

        return null;
    }

    private List<IExifValue> GetPartValues(ExifParts part)
    {
        List<IExifValue> result = new();

        if (!EnumUtils.HasFlag(this.allowedParts, part))
        {
            return result;
        }

        foreach (IExifValue value in this.values)
        {
            if (!HasValue(value))
            {
                continue;
            }

            if (ExifTags.GetPart(value.Tag) == part)
            {
                result.Add(value);
            }
        }

        return result;
    }

    private static bool HasValue(IExifValue exifValue)
    {
        object? value = exifValue.GetValue();
        if (value is null)
        {
            return false;
        }

        if (exifValue.DataType == ExifDataType.Ascii && value is string stringValue)
        {
            return stringValue.Length > 0;
        }

        if (value is Array arrayValue)
        {
            return arrayValue.Length > 0;
        }

        return true;
    }

    private static ulong GetLength(IList<IExifValue> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        if (values.Count > ushort.MaxValue)
        {
            throw new ImageFormatException("An EXIF directory contains too many entries.");
        }

        // Count the whole directory in 64 bits. GetData checks the total against the output array limit.
        ulong length = 2;

        foreach (IExifValue value in values)
        {
            uint valueLength = GetLength(value);

            length += DirectoryEntryHeaderLength + 4;

            if (valueLength > 4)
            {
                length += valueLength;
            }
        }

        // next IFD offset
        return length + 4;
    }

    /// <summary>
    /// Gets the encoded value size in bytes. Rejects sizes that exceed the four-byte directory offset range.
    /// </summary>
    /// <param name="value">The value to encode.</param>
    /// <returns>The byte count for the value.</returns>
    internal static uint GetLength(IExifValue value) => checked(GetNumberOfComponents(value) * ExifDataTypes.GetSize(value.DataType));

    internal static uint GetNumberOfComponents(IExifValue exifValue)
    {
        object? value = exifValue.GetValue();

        if (ExifUcs2StringHelpers.IsUcs2Tag((ExifTagValue)(ushort)exifValue.Tag))
        {
            return (uint)ExifUcs2StringHelpers.Ucs2Encoding.GetByteCount((string?)value!);
        }

        if (value is EncodedString encodedString)
        {
            return ExifEncodedStringHelpers.GetDataLength(encodedString);
        }

        if (exifValue.DataType == ExifDataType.Ascii)
        {
            return (uint)ExifConstants.DefaultEncoding.GetByteCount((string?)value!) + 1;
        }

        if (value is Array arrayValue)
        {
            return (uint)arrayValue.Length;
        }

        return 1;
    }

    private static int WriteArray(IExifValue value, Span<byte> destination, int offset)
    {
        int newOffset = offset;
        foreach (object obj in (Array)value.GetValue()!)
        {
            newOffset = WriteValue(value.DataType, obj, destination, newOffset);
        }

        return newOffset;
    }

    private int WriteData(uint startIndex, List<IExifValue> values, Span<byte> destination, int offset)
    {
        if (this.dataOffsets is null || this.dataOffsets.Count == 0)
        {
            return offset;
        }

        int newOffset = offset;

        int i = 0;
        foreach (IExifValue value in values)
        {
            if (GetLength(value) > 4)
            {
                WriteUInt32((uint)(newOffset - startIndex), destination, this.dataOffsets[i++]);
                newOffset = WriteValue(value, destination, newOffset);
            }
        }

        return newOffset;
    }

    private int WriteHeaders(List<IExifValue> values, Span<byte> destination, int offset)
    {
        this.dataOffsets = new List<int>();

        int newOffset = WriteUInt16((ushort)values.Count, destination, offset);

        if (values.Count == 0)
        {
            return newOffset;
        }

        foreach (IExifValue value in values)
        {
            WriteEntryHeader(value, destination[newOffset..], littleEndian: true);
            newOffset += DirectoryEntryHeaderLength;

            uint length = GetLength(value);
            if (length > 4)
            {
                this.dataOffsets.Add(newOffset);
            }
            else
            {
                WriteValue(value, destination, newOffset);
            }

            newOffset += 4;
        }

        // next IFD offset
        return WriteUInt32(0, destination, newOffset);
    }

    /// <summary>
    /// Writes the common fields of a classic EXIF or TIFF directory entry.
    /// </summary>
    /// <param name="entry">The directory entry.</param>
    /// <param name="destination">The destination for the entry header.</param>
    /// <param name="littleEndian">Whether the directory uses little-endian byte order.</param>
    internal static void WriteEntryHeader(IExifValue entry, Span<byte> destination, bool littleEndian)
    {
        ushort tag = (ushort)entry.Tag;
        ushort type = (ushort)entry.DataType;
        uint count = GetNumberOfComponents(entry);
        if (littleEndian)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(destination, tag);
            BinaryPrimitives.WriteUInt16LittleEndian(destination[2..], type);
            BinaryPrimitives.WriteUInt32LittleEndian(destination[4..], count);
        }
        else
        {
            BinaryPrimitives.WriteUInt16BigEndian(destination, tag);
            BinaryPrimitives.WriteUInt16BigEndian(destination[2..], type);
            BinaryPrimitives.WriteUInt32BigEndian(destination[4..], count);
        }
    }

    private static void WriteRational(Span<byte> destination, in Rational value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(destination[..4], value.Numerator);
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(4, 4), value.Denominator);
    }

    private static void WriteSignedRational(Span<byte> destination, in SignedRational value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination[..4], value.Numerator);
        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(4, 4), value.Denominator);
    }

    private static int WriteValue(ExifDataType dataType, object value, Span<byte> destination, int offset)
    {
        switch (dataType)
        {
            case ExifDataType.Ascii:
                offset = Write(ExifConstants.DefaultEncoding.GetBytes((string)value), destination, offset);
                destination[offset] = 0;
                return offset + 1;
            case ExifDataType.Byte:
            case ExifDataType.Undefined:
                destination[offset] = (byte)value;
                return offset + 1;
            case ExifDataType.DoubleFloat:
                return WriteDouble((double)value, destination, offset);
            case ExifDataType.Short:
                if (value is Number shortNumber)
                {
                    return WriteUInt16((ushort)shortNumber, destination, offset);
                }

                return WriteUInt16((ushort)value, destination, offset);
            case ExifDataType.Long:
                if (value is Number longNumber)
                {
                    return WriteUInt32((uint)longNumber, destination, offset);
                }

                return WriteUInt32((uint)value, destination, offset);
            case ExifDataType.Long8:
                return WriteUInt64((ulong)value, destination, offset);
            case ExifDataType.SignedLong8:
                return WriteInt64((long)value, destination, offset);
            case ExifDataType.Rational:
                WriteRational(destination.Slice(offset, 8), (Rational)value);
                return offset + 8;
            case ExifDataType.SignedByte:
                destination[offset] = unchecked((byte)(sbyte)value);
                return offset + 1;
            case ExifDataType.SignedLong:
                return WriteInt32((int)value, destination, offset);
            case ExifDataType.SignedShort:
                return WriteInt16((short)value, destination, offset);
            case ExifDataType.SignedRational:
                WriteSignedRational(destination.Slice(offset, 8), (SignedRational)value);
                return offset + 8;
            case ExifDataType.SingleFloat:
                return WriteSingle((float)value, destination, offset);
            default:
                throw new NotImplementedException();
        }
    }

    internal static int WriteValue(IExifValue exifValue, Span<byte> destination, int offset)
    {
        object? value = exifValue.GetValue();
        Guard.NotNull(value);

        if (ExifUcs2StringHelpers.IsUcs2Tag((ExifTagValue)(ushort)exifValue.Tag))
        {
            return offset + ExifUcs2StringHelpers.Write((string)value, destination[offset..]);
        }
        else if (value is EncodedString encodedString)
        {
            return offset + ExifEncodedStringHelpers.Write(encodedString, destination[offset..]);
        }

        if (exifValue.IsArray)
        {
            return WriteArray(exifValue, destination, offset);
        }

        return WriteValue(exifValue.DataType, value, destination, offset);
    }
}
