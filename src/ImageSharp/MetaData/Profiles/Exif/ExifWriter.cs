// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        /// The start index.
        /// </summary>
        private const int StartIndex = 6;

        /// <summary>
        /// Which parts will be written.
        /// </summary>
        private ExifParts allowedParts;
        private IList<ExifValue> values;
        private IList<int> dataOffsets;
        private IList<int> ifdIndexes;
        private IList<int> exifIndexes;
        private IList<int> gpsIndexes;

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
        /// <returns>
        /// The <see cref="T:byte[]"/>.
        /// </returns>
        public byte[] GetData()
        {
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

            length += 10 + 4 + 2;

            byte[] result = new byte[length];
            result[0] = (byte)'E';
            result[1] = (byte)'x';
            result[2] = (byte)'i';
            result[3] = (byte)'f';
            result[4] = 0x00;
            result[5] = 0x00;
            result[6] = (byte)'I';
            result[7] = (byte)'I';
            result[8] = 0x2A;
            result[9] = 0x00;

            int i = 10;
            uint ifdOffset = ((uint)i - StartIndex) + 4;
            uint thumbnailOffset = ifdOffset + ifdLength + exifLength + gpsLength;

            if (exifLength > 0)
            {
                this.values[exifIndex] = this.values[exifIndex].WithValue(ifdOffset + ifdLength);
            }

            if (gpsLength > 0)
            {
                this.values[gpsIndex] = this.values[gpsIndex].WithValue(ifdOffset + ifdLength + exifLength);
            }

            i = Write(BitConverter.GetBytes(ifdOffset), result, i);
            i = this.WriteHeaders(this.ifdIndexes, result, i);
            i = Write(BitConverter.GetBytes(thumbnailOffset), result, i);
            i = this.WriteData(this.ifdIndexes, result, i);

            if (exifLength > 0)
            {
                i = this.WriteHeaders(this.exifIndexes, result, i);
                i = this.WriteData(this.exifIndexes, result, i);
            }

            if (gpsLength > 0)
            {
                i = this.WriteHeaders(this.gpsIndexes, result, i);
                i = this.WriteData(this.gpsIndexes, result, i);
            }

            Write(BitConverter.GetBytes((ushort)0), result, i);

            return result;
        }

        private static int Write(byte[] source, byte[] destination, int offset)
        {
            Buffer.BlockCopy(source, 0, destination, offset, source.Length);

            return offset + source.Length;
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
                return new Collection<int>();
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

        private int WriteData(IList<int> indexes, byte[] destination, int offset)
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
                    Write(BitConverter.GetBytes(newOffset - StartIndex), destination, this.dataOffsets[i++]);
                    newOffset = this.WriteValue(value, destination, newOffset);
                }
            }

            return newOffset;
        }

        private int WriteHeaders(IList<int> indexes, byte[] destination, int offset)
        {
            this.dataOffsets = new List<int>();

            int newOffset = Write(BitConverter.GetBytes((ushort)indexes.Count), destination, offset);

            if (indexes.Count == 0)
            {
                return newOffset;
            }

            foreach (int index in indexes)
            {
                ExifValue value = this.values[index];
                newOffset = Write(BitConverter.GetBytes((ushort)value.Tag), destination, newOffset);
                newOffset = Write(BitConverter.GetBytes((ushort)value.DataType), destination, newOffset);
                newOffset = Write(BitConverter.GetBytes((uint)value.NumberOfComponents), destination, newOffset);

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

        private int WriteRational(in Rational value, byte[] destination, int offset)
        {
            Write(BitConverter.GetBytes(value.Numerator), destination, offset);
            Write(BitConverter.GetBytes(value.Denominator), destination, offset + 4);

            return offset + 8;
        }

        private int WriteSignedRational(in SignedRational value, byte[] destination, int offset)
        {
            Write(BitConverter.GetBytes(value.Numerator), destination, offset);
            Write(BitConverter.GetBytes(value.Denominator), destination, offset + 4);

            return offset + 8;
        }

        private int WriteValue(ExifDataType dataType, object value, byte[] destination, int offset)
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
                    return Write(BitConverter.GetBytes((double)value), destination, offset);
                case ExifDataType.Short:
                    return Write(BitConverter.GetBytes((ushort)value), destination, offset);
                case ExifDataType.Long:
                    return Write(BitConverter.GetBytes((uint)value), destination, offset);
                case ExifDataType.Rational:
                    return this.WriteRational((Rational)value, destination, offset);
                case ExifDataType.SignedByte:
                    destination[offset] = unchecked((byte)((sbyte)value));
                    return offset + 1;
                case ExifDataType.SignedLong:
                    return Write(BitConverter.GetBytes((int)value), destination, offset);
                case ExifDataType.SignedShort:
                    return Write(BitConverter.GetBytes((short)value), destination, offset);
                case ExifDataType.SignedRational:
                    return this.WriteSignedRational((SignedRational)value, destination, offset);
                case ExifDataType.SingleFloat:
                    return Write(BitConverter.GetBytes((float)value), destination, offset);
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