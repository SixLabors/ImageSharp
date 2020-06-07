// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Text;

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    /// <summary>
    /// Reads and parses EXIF data from a byte array.
    /// </summary>
    internal sealed class ExifReader
    {
        private List<ExifTag> invalidTags;
        private readonly byte[] exifData;
        private int position;
        private bool isBigEndian;
        private uint exifOffset;
        private uint gpsOffset;

        public ExifReader(byte[] exifData)
        {
            this.exifData = exifData ?? throw new ArgumentNullException(nameof(exifData));
        }

        private delegate TDataType ConverterMethod<TDataType>(ReadOnlySpan<byte> data);

        /// <summary>
        /// Gets the invalid tags.
        /// </summary>
        public IReadOnlyList<ExifTag> InvalidTags => this.invalidTags ?? (IReadOnlyList<ExifTag>)Array.Empty<ExifTag>();

        /// <summary>
        /// Gets the thumbnail length in the byte stream.
        /// </summary>
        public uint ThumbnailLength { get; private set; }

        /// <summary>
        /// Gets the thumbnail offset position in the byte stream.
        /// </summary>
        public uint ThumbnailOffset { get; private set; }

        /// <summary>
        /// Gets the remaining length.
        /// </summary>
        private int RemainingLength
        {
            get
            {
                if (this.position >= this.exifData.Length)
                {
                    return 0;
                }

                return this.exifData.Length - this.position;
            }
        }

        /// <summary>
        /// Reads and returns the collection of EXIF values.
        /// </summary>
        /// <returns>
        /// The <see cref="Collection{ExifValue}"/>.
        /// </returns>
        public List<IExifValue> ReadValues()
        {
            var values = new List<IExifValue>();

            // II == 0x4949
            this.isBigEndian = this.ReadUInt16() != 0x4949;

            if (this.ReadUInt16() != 0x002A)
            {
                return values;
            }

            uint ifdOffset = this.ReadUInt32();
            this.AddValues(values, ifdOffset);

            uint thumbnailOffset = this.ReadUInt32();
            this.GetThumbnail(thumbnailOffset);

            if (this.exifOffset != 0)
            {
                this.AddValues(values, this.exifOffset);
            }

            if (this.gpsOffset != 0)
            {
                this.AddValues(values, this.gpsOffset);
            }

            return values;
        }

        private static TDataType[] ToArray<TDataType>(ExifDataType dataType, ReadOnlySpan<byte> data, ConverterMethod<TDataType> converter)
        {
            int dataTypeSize = (int)ExifDataTypes.GetSize(dataType);
            int length = data.Length / dataTypeSize;

            var result = new TDataType[length];

            for (int i = 0; i < length; i++)
            {
                ReadOnlySpan<byte> buffer = data.Slice(i * dataTypeSize, dataTypeSize);

                result.SetValue(converter(buffer), i);
            }

            return result;
        }

        private byte ConvertToByte(ReadOnlySpan<byte> buffer) => buffer[0];

        private string ConvertToString(ReadOnlySpan<byte> buffer)
        {
            int nullCharIndex = buffer.IndexOf((byte)0);

            if (nullCharIndex > -1)
            {
                buffer = buffer.Slice(0, nullCharIndex);
            }

            return Encoding.UTF8.GetString(buffer);
        }

        /// <summary>
        /// Adds the collection of EXIF values to the reader.
        /// </summary>
        /// <param name="values">The values.</param>
        /// <param name="index">The index.</param>
        private void AddValues(List<IExifValue> values, uint index)
        {
            if (index > (uint)this.exifData.Length)
            {
                return;
            }

            this.position = (int)index;
            int count = this.ReadUInt16();

            for (int i = 0; i < count; i++)
            {
                if (!this.TryReadValue(out ExifValue value))
                {
                    continue;
                }

                bool duplicate = false;
                foreach (IExifValue val in values)
                {
                    if (val == value)
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (duplicate)
                {
                    continue;
                }

                if (value == ExifTag.SubIFDOffset)
                {
                    this.exifOffset = ((ExifLong)value).Value;
                }
                else if (value == ExifTag.GPSIFDOffset)
                {
                    this.gpsOffset = ((ExifLong)value).Value;
                }
                else
                {
                    values.Add(value);
                }
            }
        }

        private object ConvertValue(ExifDataType dataType, ReadOnlySpan<byte> buffer, uint numberOfComponents)
        {
            if (buffer.Length == 0)
            {
                return null;
            }

            switch (dataType)
            {
                case ExifDataType.Unknown:
                    return null;
                case ExifDataType.Ascii:
                    return this.ConvertToString(buffer);
                case ExifDataType.Byte:
                    if (numberOfComponents == 1)
                    {
                        return this.ConvertToByte(buffer);
                    }

                    return buffer.ToArray();
                case ExifDataType.DoubleFloat:
                    if (numberOfComponents == 1)
                    {
                        return this.ConvertToDouble(buffer);
                    }

                    return ToArray(dataType, buffer, this.ConvertToDouble);
                case ExifDataType.Long:
                    if (numberOfComponents == 1)
                    {
                        return this.ConvertToUInt32(buffer);
                    }

                    return ToArray(dataType, buffer, this.ConvertToUInt32);
                case ExifDataType.Rational:
                    if (numberOfComponents == 1)
                    {
                        return this.ToRational(buffer);
                    }

                    return ToArray(dataType, buffer, this.ToRational);
                case ExifDataType.Short:
                    if (numberOfComponents == 1)
                    {
                        return this.ConvertToShort(buffer);
                    }

                    return ToArray(dataType, buffer, this.ConvertToShort);
                case ExifDataType.SignedByte:
                    if (numberOfComponents == 1)
                    {
                        return this.ConvertToSignedByte(buffer);
                    }

                    return ToArray(dataType, buffer, this.ConvertToSignedByte);
                case ExifDataType.SignedLong:
                    if (numberOfComponents == 1)
                    {
                        return this.ConvertToInt32(buffer);
                    }

                    return ToArray(dataType, buffer, this.ConvertToInt32);
                case ExifDataType.SignedRational:
                    if (numberOfComponents == 1)
                    {
                        return this.ToSignedRational(buffer);
                    }

                    return ToArray(dataType, buffer, this.ToSignedRational);
                case ExifDataType.SignedShort:
                    if (numberOfComponents == 1)
                    {
                        return this.ConvertToSignedShort(buffer);
                    }

                    return ToArray(dataType, buffer, this.ConvertToSignedShort);
                case ExifDataType.SingleFloat:
                    if (numberOfComponents == 1)
                    {
                        return this.ConvertToSingle(buffer);
                    }

                    return ToArray(dataType, buffer, this.ConvertToSingle);
                case ExifDataType.Undefined:
                    if (numberOfComponents == 1)
                    {
                        return this.ConvertToByte(buffer);
                    }

                    return buffer.ToArray();
                default:
                    throw new NotSupportedException();
            }
        }

        private bool TryReadValue(out ExifValue exifValue)
        {
            exifValue = default;

            // 2   | 2    | 4     | 4
            // tag | type | count | value offset
            if (this.RemainingLength < 12)
            {
                return false;
            }

            var tag = (ExifTagValue)this.ReadUInt16();
            ExifDataType dataType = EnumUtils.Parse(this.ReadUInt16(), ExifDataType.Unknown);

            // Ensure that the data type is valid
            if (dataType == ExifDataType.Unknown)
            {
                return false;
            }

            uint numberOfComponents = this.ReadUInt32();

            // Issue #132: ExifDataType == Undefined is treated like a byte array.
            // If numberOfComponents == 0 this value can only be handled as an inline value and must fallback to 4 (bytes)
            if (dataType == ExifDataType.Undefined && numberOfComponents == 0)
            {
                numberOfComponents = 4;
            }

            uint size = numberOfComponents * ExifDataTypes.GetSize(dataType);

            this.TryReadSpan(4, out ReadOnlySpan<byte> offsetBuffer);

            object value;
            if (size > 4)
            {
                int oldIndex = this.position;
                uint newIndex = this.ConvertToUInt32(offsetBuffer);

                // Ensure that the new index does not overrun the data
                if (newIndex > int.MaxValue)
                {
                    this.AddInvalidTag(new UnkownExifTag(tag));
                    return false;
                }

                this.position = (int)newIndex;

                if (this.RemainingLength < size)
                {
                    this.AddInvalidTag(new UnkownExifTag(tag));

                    this.position = oldIndex;
                    return false;
                }

                this.TryReadSpan((int)size, out ReadOnlySpan<byte> dataBuffer);

                value = this.ConvertValue(dataType, dataBuffer, numberOfComponents);
                this.position = oldIndex;
            }
            else
            {
                value = this.ConvertValue(dataType, offsetBuffer, numberOfComponents);
            }

            exifValue = ExifValues.Create(tag) ?? ExifValues.Create(tag, dataType, numberOfComponents);

            if (exifValue is null)
            {
                this.AddInvalidTag(new UnkownExifTag(tag));
                return false;
            }

            if (!exifValue.TrySetValue(value))
            {
                return false;
            }

            return true;
        }

        private void AddInvalidTag(ExifTag tag)
            => (this.invalidTags ?? (this.invalidTags = new List<ExifTag>())).Add(tag);

        private bool TryReadSpan(int length, out ReadOnlySpan<byte> span)
        {
            if (this.RemainingLength < length)
            {
                span = default;

                return false;
            }

            span = new ReadOnlySpan<byte>(this.exifData, this.position, length);

            this.position += length;

            return true;
        }

        private uint ReadUInt32()
        {
            // Known as Long in Exif Specification
            return this.TryReadSpan(4, out ReadOnlySpan<byte> span)
                ? this.ConvertToUInt32(span)
                : default;
        }

        private ushort ReadUInt16()
        {
            return this.TryReadSpan(2, out ReadOnlySpan<byte> span)
                ? this.ConvertToShort(span)
                : default;
        }

        private void GetThumbnail(uint offset)
        {
            var values = new List<IExifValue>();
            this.AddValues(values, offset);

            foreach (ExifValue value in values)
            {
                if (value == ExifTag.JPEGInterchangeFormat)
                {
                    this.ThumbnailOffset = ((ExifLong)value).Value;
                }
                else if (value == ExifTag.JPEGInterchangeFormatLength)
                {
                    this.ThumbnailLength = ((ExifLong)value).Value;
                }
            }
        }

        private double ConvertToDouble(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length < 8)
            {
                return default;
            }

            long intValue = this.isBigEndian
                ? BinaryPrimitives.ReadInt64BigEndian(buffer)
                : BinaryPrimitives.ReadInt64LittleEndian(buffer);

            return Unsafe.As<long, double>(ref intValue);
        }

        private uint ConvertToUInt32(ReadOnlySpan<byte> buffer)
        {
            // Known as Long in Exif Specification
            if (buffer.Length < 4)
            {
                return default;
            }

            return this.isBigEndian
                ? BinaryPrimitives.ReadUInt32BigEndian(buffer)
                : BinaryPrimitives.ReadUInt32LittleEndian(buffer);
        }

        private ushort ConvertToShort(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length < 2)
            {
                return default;
            }

            return this.isBigEndian
                ? BinaryPrimitives.ReadUInt16BigEndian(buffer)
                : BinaryPrimitives.ReadUInt16LittleEndian(buffer);
        }

        private float ConvertToSingle(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length < 4)
            {
                return default;
            }

            int intValue = this.isBigEndian
                ? BinaryPrimitives.ReadInt32BigEndian(buffer)
                : BinaryPrimitives.ReadInt32LittleEndian(buffer);

            return Unsafe.As<int, float>(ref intValue);
        }

        private Rational ToRational(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length < 8)
            {
                return default;
            }

            uint numerator = this.ConvertToUInt32(buffer.Slice(0, 4));
            uint denominator = this.ConvertToUInt32(buffer.Slice(4, 4));

            return new Rational(numerator, denominator, false);
        }

        private sbyte ConvertToSignedByte(ReadOnlySpan<byte> buffer) => unchecked((sbyte)buffer[0]);

        private int ConvertToInt32(ReadOnlySpan<byte> buffer) // SignedLong in Exif Specification
        {
            if (buffer.Length < 4)
            {
                return default;
            }

            return this.isBigEndian
                ? BinaryPrimitives.ReadInt32BigEndian(buffer)
                : BinaryPrimitives.ReadInt32LittleEndian(buffer);
        }

        private SignedRational ToSignedRational(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length < 8)
            {
                return default;
            }

            int numerator = this.ConvertToInt32(buffer.Slice(0, 4));
            int denominator = this.ConvertToInt32(buffer.Slice(4, 4));

            return new SignedRational(numerator, denominator, false);
        }

        private short ConvertToSignedShort(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length < 2)
            {
                return default;
            }

            return this.isBigEndian
                ? BinaryPrimitives.ReadInt16BigEndian(buffer)
                : BinaryPrimitives.ReadInt16LittleEndian(buffer);
        }
    }
}
