// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using SixLabors.ImageSharp.Primitives;

namespace SixLabors.ImageSharp.MetaData.Profiles.Exif
{
    /// <summary>
    /// Contains methods for writing EXIF metadata.
    /// </summary>
    internal sealed class ExifWriter
    {
        /// <summary>
        /// Which parts will be written.
        /// </summary>
        private ExifParts allowedParts;
        private IList<ExifValue> values;
        private List<int> dataOffsets;
        private List<int> ifdIndexes;
        private List<int> exifIndexes;
        private List<int> gpsIndexes;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExifWriter"/> class.
        /// </summary>
        /// <param name="values">The values.</param>
        /// <param name="allowedParts">The allowed parts.</param>
        public ExifWriter(IList<ExifValue> values, ExifParts allowedParts)
        {
            this.values = values;
            this.allowedParts = allowedParts;
            this.ifdIndexes = this.GetIndexes(ExifParts.IfdTags, ExifTags.Ifd);
            this.exifIndexes = this.GetIndexes(ExifParts.ExifTags, ExifTags.Exif);
            this.gpsIndexes = this.GetIndexes(ExifParts.GPSTags, ExifTags.Gps);
        }

        /// <summary>
        /// Returns the EXIF data.
        /// </summary>
        /// <param name="includeExifIdCode">Indicates, if the Exif ID code should be included.
        /// This Exif ID code should not be included in case of PNG's. Defaults to true.</param>
        /// <returns>
        /// The <see cref="T:byte[]"/>.
        /// </returns>
        public byte[] GetData(bool includeExifIdCode = true)
        {
            uint startIndex = 6;
            uint length;
            int exifIndex = -1;
            int gpsIndex = -1;

            if (this.exifIndexes.Count > 0)
            {
                exifIndex = (int)this.GetIndex(this.ifdIndexes, ExifTag.SubIFDOffset);
            }

            if (this.gpsIndexes.Count > 0)
            {
                gpsIndex = (int)this.GetIndex(this.ifdIndexes, ExifTag.GPSIFDOffset);
            }

            uint ifdLength = 2 + this.GetLength(this.ifdIndexes) + 4;
            uint exifLength = this.GetLength(this.exifIndexes);
            uint gpsLength = this.GetLength(this.gpsIndexes);

            if (exifLength > 0)
            {
                exifLength += 2;
            }

            if (gpsLength > 0)
            {
                gpsLength += 2;
            }

            length = ifdLength + exifLength + gpsLength;

            if (length == 6)
            {
                return null;
            }

            if (includeExifIdCode)
            {
                // Exif Code (6 bytes) + byte order marker (4 bytes)
                length += 10;
            }
            else
            {
                // special case for PNG eXIf Chunk:
                // two bytes for the byte Order marker 'II', followed by the number 42 (0x2A) and a 0, making 4 bytes total
                length += 4;

                // if the Exif Code ("Exif00") is not included, the start index is 0 instead of 6
                startIndex = 0;
            }

            length += 4 + 2;

            byte[] result = new byte[length];

            int i = 0;
            if (includeExifIdCode)
            {
                result[i++] = (byte)'E';
                result[i++] = (byte)'x';
                result[i++] = (byte)'i';
                result[i++] = (byte)'f';
                result[i++] = 0x00;
                result[i++] = 0x00;
            }

            // the byte order marker for little-endian, followed by the number 42 and a 0
            result[i++] = (byte)'I';
            result[i++] = (byte)'I';
            result[i++] = 0x2A;
            result[i++] = 0x00;

            uint ifdOffset = ((uint)i - startIndex) + 4;
            uint thumbnailOffset = ifdOffset + ifdLength + exifLength + gpsLength;

            if (exifLength > 0)
            {
                this.values[exifIndex] = this.values[exifIndex].WithValue(ifdOffset + ifdLength);
            }

            if (gpsLength > 0)
            {
                this.values[gpsIndex] = this.values[gpsIndex].WithValue(ifdOffset + ifdLength + exifLength);
            }

            i = WriteUInt32(ifdOffset, result, i);
            i = this.WriteHeaders(this.ifdIndexes, result, i);
            i = WriteUInt32(thumbnailOffset, result, i);
            i = this.WriteData(startIndex, this.ifdIndexes, result, i);

            if (exifLength > 0)
            {
                i = this.WriteHeaders(this.exifIndexes, result, i);
                i = this.WriteData(startIndex, this.exifIndexes, result, i);
            }

            if (gpsLength > 0)
            {
                i = this.WriteHeaders(this.gpsIndexes, result, i);
                i = this.WriteData(startIndex, this.gpsIndexes, result, i);
            }

            WriteUInt16((ushort)0, result, i);

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

        private int GetIndex(IList<int> indexes, ExifTag tag)
        {
            foreach (int index in indexes)
            {
                if (this.values[index].Tag == tag)
                {
                    return index;
                }
            }

            int newIndex = this.values.Count;
            indexes.Add(newIndex);
            this.values.Add(ExifValue.Create(tag, null));
            return newIndex;
        }

        private List<int> GetIndexes(ExifParts part, ExifTag[] tags)
        {
            if (((int)this.allowedParts & (int)part) == 0)
            {
                return new List<int>();
            }

            var result = new List<int>();
            for (int i = 0; i < this.values.Count; i++)
            {
                ExifValue value = this.values[i];

                if (!value.HasValue)
                {
                    continue;
                }

                int index = Array.IndexOf(tags, value.Tag);
                if (index > -1)
                {
                    result.Add(i);
                }
            }

            return result;
        }

        private uint GetLength(IList<int> indexes)
        {
            uint length = 0;

            foreach (int index in indexes)
            {
                uint valueLength = (uint)this.values[index].Length;

                if (valueLength > 4)
                {
                    length += 12 + valueLength;
                }
                else
                {
                    length += 12;
                }
            }

            return length;
        }

        private int WriteArray(ExifValue value, byte[] destination, int offset)
        {
            if (value.DataType == ExifDataType.Ascii)
            {
                return this.WriteValue(ExifDataType.Ascii, value.Value, destination, offset);
            }

            int newOffset = offset;
            foreach (object obj in (Array)value.Value)
            {
                newOffset = this.WriteValue(value.DataType, obj, destination, newOffset);
            }

            return newOffset;
        }

        private int WriteData(uint startIndex, List<int> indexes, byte[] destination, int offset)
        {
            if (this.dataOffsets.Count == 0)
            {
                return offset;
            }

            int newOffset = offset;

            int i = 0;
            foreach (int index in indexes)
            {
                ExifValue value = this.values[index];
                if (value.Length > 4)
                {
                    WriteUInt32((uint)(newOffset - startIndex), destination, this.dataOffsets[i++]);
                    newOffset = this.WriteValue(value, destination, newOffset);
                }
            }

            return newOffset;
        }

        private int WriteHeaders(List<int> indexes, byte[] destination, int offset)
        {
            this.dataOffsets = new List<int>();

            int newOffset = WriteUInt16((ushort)indexes.Count, destination, offset);

            if (indexes.Count == 0)
            {
                return newOffset;
            }

            foreach (int index in indexes)
            {
                ExifValue value = this.values[index];
                newOffset = WriteUInt16((ushort)value.Tag, destination, newOffset);
                newOffset = WriteUInt16((ushort)value.DataType, destination, newOffset);
                newOffset = WriteUInt32((uint)value.NumberOfComponents, destination, newOffset);

                if (value.Length > 4)
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
                    return Write(Encoding.UTF8.GetBytes((string)value), destination, offset);
                case ExifDataType.Byte:
                case ExifDataType.Undefined:
                    destination[offset] = (byte)value;
                    return offset + 1;
                case ExifDataType.DoubleFloat:
                    return WriteDouble((double)value, destination, offset);
                case ExifDataType.Short:
                    return WriteUInt16((ushort)value, destination, offset);
                case ExifDataType.Long:
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

        private int WriteValue(ExifValue value, byte[] destination, int offset)
        {
            if (value.IsArray && value.DataType != ExifDataType.Ascii)
            {
                return this.WriteArray(value, destination, offset);
            }

            return this.WriteValue(value.DataType, value.Value, destination, offset);
        }
    }
}