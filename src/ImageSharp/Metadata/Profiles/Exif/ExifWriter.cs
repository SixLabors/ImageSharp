// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    /// <summary>
    /// Contains methods for writing EXIF metadata.
    /// </summary>
    internal sealed class ExifWriter
    {
        /// <summary>
        /// Which parts will be written.
        /// </summary>
        private readonly ExifParts allowedParts;
        private readonly IList<IExifValue> values;
        private List<int> dataOffsets;
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
            uint length;

            IExifValue exifOffset = GetOffsetValue(this.ifdValues, this.exifValues, ExifTag.SubIFDOffset);
            IExifValue gpsOffset = GetOffsetValue(this.ifdValues, this.gpsValues, ExifTag.GPSIFDOffset);

            if (this.ifdValues.Count == 0 && this.exifValues.Count == 0 && this.gpsValues.Count == 0)
            {
                return Array.Empty<byte>();
            }

            uint ifdLength = this.GetLength(this.ifdValues) + 4U;
            uint exifLength = this.GetLength(this.exifValues);
            uint gpsLength = this.GetLength(this.gpsValues);

            length = ifdLength + exifLength + gpsLength;

            if (length == 4U)
            {
                return Array.Empty<byte>();
            }

            // two bytes for the byte Order marker 'II' or 'MM', followed by the number 42 (0x2A) and a 0, making 4 bytes total
            length += (uint)ExifConstants.LittleEndianByteOrderMarker.Length;

            length += 4 + 2;

            var result = new byte[length];

            int i = 0;

            // The byte order marker for little-endian, followed by the number 42 and a 0
            ExifConstants.LittleEndianByteOrderMarker.CopyTo(result.AsSpan(start: i));
            i += ExifConstants.LittleEndianByteOrderMarker.Length;

            uint ifdOffset = ((uint)i - startIndex) + 4U;
            uint thumbnailOffset = ifdOffset + ifdLength + exifLength + gpsLength;

            exifOffset?.TrySetValue(ifdOffset + ifdLength);
            gpsOffset?.TrySetValue(ifdOffset + ifdLength + exifLength);

            i = WriteUInt32(ifdOffset, result, i);
            i = this.WriteHeaders(this.ifdValues, result, i);
            i = WriteUInt32(thumbnailOffset, result, i);
            i = this.WriteData(startIndex, this.ifdValues, result, i);

            if (exifLength > 0)
            {
                i = this.WriteHeaders(this.exifValues, result, i);
                i = this.WriteData(startIndex, this.exifValues, result, i);
            }

            if (gpsLength > 0)
            {
                i = this.WriteHeaders(this.gpsValues, result, i);
                i = this.WriteData(startIndex, this.gpsValues, result, i);
            }

            WriteUInt16(0, result, i);

            return result;
        }

        private static unsafe int WriteSingle(float value, Span<byte> destination, int offset)
        {
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(offset, 4), *((int*)&value));

            return offset + 4;
        }

        private static unsafe int WriteDouble(double value, Span<byte> destination, int offset)
        {
            BinaryPrimitives.WriteInt64LittleEndian(destination.Slice(offset, 8), *((long*)&value));

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

        private static int WriteInt32(int value, Span<byte> destination, int offset)
        {
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(offset, 4), value);

            return offset + 4;
        }

        private static IExifValue GetOffsetValue(List<IExifValue> ifdValues, List<IExifValue> values, ExifTag offset)
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

                ExifValue result = ExifValues.Create(offset);
                ifdValues.Add(result);

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
            var result = new List<IExifValue>();

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
            object value = exifValue.GetValue();
            if (value is null)
            {
                return false;
            }

            if (exifValue.DataType == ExifDataType.Ascii)
            {
                string stringValue = (string)value;
                return stringValue.Length > 0;
            }

            if (value is Array arrayValue)
            {
                return arrayValue.Length > 0;
            }

            return true;
        }

        private uint GetLength(IList<IExifValue> values)
        {
            if (values.Count == 0)
            {
                return 0;
            }

            uint length = 2;

            foreach (IExifValue value in values)
            {
                uint valueLength = GetLength(value);

                length += 2 + 2 + 4 + 4;

                if (valueLength > 4)
                {
                    length += valueLength;
                }
            }

            return length;
        }

        private static uint GetLength(IExifValue value) => GetNumberOfComponents(value) * ExifDataTypes.GetSize(value.DataType);

        private static uint GetNumberOfComponents(IExifValue exifValue)
        {
            object value = exifValue.GetValue();

            if (exifValue.DataType == ExifDataType.Ascii)
            {
                return (uint)Encoding.UTF8.GetBytes((string)value).Length + 1;
            }

            if (value is Array arrayValue)
            {
                return (uint)arrayValue.Length;
            }

            return 1;
        }

        private int WriteArray(IExifValue value, Span<byte> destination, int offset)
        {
            if (value.DataType == ExifDataType.Ascii)
            {
                return this.WriteValue(ExifDataType.Ascii, value.GetValue(), destination, offset);
            }

            int newOffset = offset;
            foreach (object obj in (Array)value.GetValue())
            {
                newOffset = this.WriteValue(value.DataType, obj, destination, newOffset);
            }

            return newOffset;
        }

        private int WriteData(uint startIndex, List<IExifValue> values, Span<byte> destination, int offset)
        {
            if (this.dataOffsets.Count == 0)
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
                    newOffset = this.WriteValue(value, destination, newOffset);
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
                newOffset = WriteUInt16((ushort)value.Tag, destination, newOffset);
                newOffset = WriteUInt16((ushort)value.DataType, destination, newOffset);
                newOffset = WriteUInt32(GetNumberOfComponents(value), destination, newOffset);

                uint length = GetLength(value);
                if (length > 4)
                {
                    this.dataOffsets.Add(newOffset);
                }
                else
                {
                    this.WriteValue(value, destination, newOffset);
                }

                newOffset += 4;
            }

            return newOffset;
        }

        private static void WriteRational(Span<byte> destination, in Rational value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(0, 4), value.Numerator);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(4, 4), value.Denominator);
        }

        private static void WriteSignedRational(Span<byte> destination, in SignedRational value)
        {
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(0, 4), value.Numerator);
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(4, 4), value.Denominator);
        }

        private int WriteValue(ExifDataType dataType, object value, Span<byte> destination, int offset)
        {
            switch (dataType)
            {
                case ExifDataType.Ascii:
                    offset = Write(Encoding.UTF8.GetBytes((string)value), destination, offset);
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
                case ExifDataType.Rational:
                    WriteRational(destination.Slice(offset, 8), (Rational)value);
                    return offset + 8;
                case ExifDataType.SignedByte:
                    destination[offset] = unchecked((byte)((sbyte)value));
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

        private int WriteValue(IExifValue value, Span<byte> destination, int offset)
        {
            if (value.IsArray && value.DataType != ExifDataType.Ascii)
            {
                return this.WriteArray(value, destination, offset);
            }

            return this.WriteValue(value.DataType, value.GetValue(), destination, offset);
        }
    }
}
