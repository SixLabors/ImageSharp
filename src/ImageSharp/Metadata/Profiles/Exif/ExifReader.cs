// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    /// <summary>
    /// Reads and parses EXIF data from a stream.
    /// </summary>
    internal class ExifReader
    {
        private readonly Stream data;

        private readonly byte[] offsetBuffer = new byte[4];
        private readonly byte[] buf4 = new byte[4];
        private readonly byte[] buf2 = new byte[2];

        // used for sequential read big values (actual for multiframe big files)
        private readonly SortedList<uint, Action> lazyLoaders = new SortedList<uint, Action>();

        private bool isBigEndian;

        private List<ExifTag> invalidTags;

        public ExifReader(bool isBigEndian, Stream stream)
        {
            this.isBigEndian = isBigEndian;
            this.data = stream ?? throw new ArgumentNullException(nameof(stream));
        }

        public ExifReader(byte[] exifData) =>
            this.data = new MemoryStream(exifData ?? throw new ArgumentNullException(nameof(exifData)));

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

        protected uint? LazyStartOffset => this.lazyLoaders.Count > 0 ? this.lazyLoaders.Keys[0] : (uint?)null;

        private uint Length => (uint)this.data.Length;

        private int RemainingLength
        {
            get
            {
                if (this.data.Position >= this.data.Length)
                {
                    return 0;
                }

                return (int)(this.data.Length - this.data.Position);
            }
        }

        /// <summary>
        /// Reads and returns the collection of EXIF values.
        /// </summary>
        /// <returns>
        /// The <see cref="Collection{ExifValue}"/>.
        /// </returns>
        public virtual List<IExifValue> ReadValues()
        {
            var values = new List<IExifValue>();

            // Exif header: II == 0x4949
            this.isBigEndian = this.ReadUInt16() != 0x4949;

            if (this.ReadUInt16() != 0x002A)
            {
                return values;
            }

            uint ifdOffset = this.ReadUInt32();

            this.AddValues(values, ifdOffset);

            uint thumbnailOffset = this.ReadUInt32();
            this.GetThumbnail(thumbnailOffset);

            this.AddSubIfdValues(values);
            this.LazyLoad();

            return values;
        }

        protected void LazyLoad()
        {
            foreach (Action act in this.lazyLoaders.Values)
            {
                act();
            }
        }

        /// <summary>
        /// Adds the collection of EXIF values to the reader.
        /// </summary>
        /// <param name="values">The values.</param>
        /// <param name="index">The index.</param>
        protected void AddValues(List<IExifValue> values, uint index)
        {
            if (index > this.Length)
            {
                return;
            }

            this.Seek(index);
            int count = this.ReadUInt16();

            for (int i = 0; i < count; i++)
            {
                this.ReadValue(values);
            }
        }

        protected void AddSubIfdValues(List<IExifValue> values)
        {
            uint exifOffset = 0;
            uint gpsOffset = 0;
            foreach (IExifValue value in values)
            {
                if (value.Tag == ExifTag.SubIFDOffset)
                {
                    exifOffset = ((ExifLong)value).Value;
                }

                if (value.Tag == ExifTag.GPSIFDOffset)
                {
                    gpsOffset = ((ExifLong)value).Value;
                }
            }

            if (exifOffset != 0)
            {
                this.AddValues(values, exifOffset);
            }

            if (gpsOffset != 0)
            {
                this.AddValues(values, gpsOffset);
            }
        }

        private static bool IsDuplicate(IList<IExifValue> values, IExifValue value)
        {
            foreach (IExifValue val in values)
            {
                if (val == value)
                {
                    return true;
                }
            }

            return false;
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

        private void ReadValue(List<IExifValue> values)
        {
            // 2   | 2    | 4     | 4
            // tag | type | count | value offset
            if (this.RemainingLength < 12)
            {
                return;
            }

            var tag = (ExifTagValue)this.ReadUInt16();
            ExifDataType dataType = EnumUtils.Parse(this.ReadUInt16(), ExifDataType.Unknown);

            uint numberOfComponents = this.ReadUInt32();

            this.TryReadSpan(this.offsetBuffer);

            // Ensure that the data type is valid
            if (dataType == ExifDataType.Unknown)
            {
                return;
            }

            // Issue #132: ExifDataType == Undefined is treated like a byte array.
            // If numberOfComponents == 0 this value can only be handled as an inline value and must fallback to 4 (bytes)
            if (dataType == ExifDataType.Undefined && numberOfComponents == 0)
            {
                numberOfComponents = 4;
            }

            ExifValue exifValue = ExifValues.Create(tag) ?? ExifValues.Create(tag, dataType, numberOfComponents);

            if (exifValue is null)
            {
                this.AddInvalidTag(new UnkownExifTag(tag));
                return;
            }

            uint size = numberOfComponents * ExifDataTypes.GetSize(dataType);
            object value = null;
            if (size > 4)
            {
                uint newIndex = this.ConvertToUInt32(this.offsetBuffer);

                // Ensure that the new index does not overrun the data
                if (newIndex > int.MaxValue || newIndex + size > this.Length)
                {
                    this.AddInvalidTag(new UnkownExifTag(tag));
                    return;
                }

                this.lazyLoaders.Add(newIndex, () =>
                {
                    var dataBuffer = new byte[size];
                    this.Seek(newIndex);
                    if (this.TryReadSpan(dataBuffer))
                    {
                        value = this.ConvertValue(dataType, dataBuffer, numberOfComponents);
                    }

                    if (exifValue.TrySetValue(value) && !IsDuplicate(values, exifValue))
                    {
                        values.Add(exifValue);
                    }
                });
            }
            else
            {
                value = this.ConvertValue(dataType, this.offsetBuffer, numberOfComponents);
            }

            if (exifValue.TrySetValue(value) && !IsDuplicate(values, exifValue))
            {
                values.Add(exifValue);
            }
        }

        private void AddInvalidTag(ExifTag tag)
            => (this.invalidTags ??= new List<ExifTag>()).Add(tag);

        private void Seek(long pos)
            => this.data.Seek(pos, SeekOrigin.Begin);

        private bool TryReadSpan(Span<byte> span)
        {
            int length = span.Length;
            if (this.RemainingLength < length)
            {
                span = default;

                return false;
            }

            int readed = this.data.Read(span);

            return readed == length;
        }

        // Known as Long in Exif Specification
        protected uint ReadUInt32() =>
            this.TryReadSpan(this.buf4)
                ? this.ConvertToUInt32(this.buf4)
                : default;

        protected ushort ReadUInt16() => this.TryReadSpan(this.buf2)
                ? this.ConvertToShort(this.buf2)
                : default;

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
