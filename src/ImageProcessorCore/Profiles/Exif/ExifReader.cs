// <copyright file="ExifReader.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>
namespace ImageProcessorCore
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Reads and parses EXIF data from a byte array
    /// </summary>
    internal sealed class ExifReader
    {
        private delegate TDataType ConverterMethod<TDataType>(byte[] data);

        private readonly Collection<ExifTag> invalidTags = new Collection<ExifTag>();
        private byte[] exifData;
        private uint currentIndex;
        private bool isLittleEndian;
        private uint exifOffset;
        private uint gpsOffset;
        private uint startIndex;

        /// <summary>
        /// Gets the thumbnail length in the byte stream
        /// </summary>
        public uint ThumbnailLength
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the thumbnail offset position in the byte stream
        /// </summary>
        public uint ThumbnailOffset
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the remaining length.
        /// </summary>
        private int RemainingLength
        {
            get
            {
                if (this.currentIndex >= this.exifData.Length)
                {
                    return 0;
                }

                return this.exifData.Length - (int)this.currentIndex;
            }
        }

        /// <summary>
        /// Reads and returns the collection of EXIF values.
        /// </summary>
        /// <param name="data">The data.</param>
        /// <returns>
        /// The <see cref="Collection{ExifValue}"/>.
        /// </returns>
        public Collection<ExifValue> Read(byte[] data)
        {
            Collection<ExifValue> result = new Collection<ExifValue>();

            this.exifData = data;

            if (this.GetString(4) == "Exif")
            {
                if (this.GetShort() != 0)
                {
                    return result;
                }

                this.startIndex = 6;
            }
            else
            {
                this.currentIndex = 0;
            }

            this.isLittleEndian = this.GetString(2) == "II";

            if (this.GetShort() != 0x002A)
            {
                return result;
            }

            uint ifdOffset = this.GetLong();
            this.AddValues(result, ifdOffset);

            uint thumbnailOffset = this.GetLong();
            this.GetThumbnail(thumbnailOffset);

            if (this.exifOffset != 0)
            {
                this.AddValues(result, this.exifOffset);
            }

            if (this.gpsOffset != 0)
            {
                this.AddValues(result, this.gpsOffset);
            }

            return result;
        }

        public IEnumerable<ExifTag> InvalidTags => this.invalidTags;

        private void AddValues(Collection<ExifValue> values, uint index)
        {
            this.currentIndex = this.startIndex + index;
            ushort count = this.GetShort();

            for (ushort i = 0; i < count; i++)
            {
                ExifValue value = this.CreateValue();
                if (value == null)
                {
                    continue;
                }

                bool duplicate = false;
                foreach (ExifValue val in values)
                {
                    if (val.Tag == value.Tag)
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (duplicate)
                {
                    continue;
                }

                if (value.Tag == ExifTag.SubIFDOffset)
                {
                    if (value.DataType == ExifDataType.Long)
                    {
                        this.exifOffset = (uint)value.Value;
                    }
                }
                else if (value.Tag == ExifTag.GPSIFDOffset)
                {
                    if (value.DataType == ExifDataType.Long)
                    {
                        this.gpsOffset = (uint)value.Value;
                    }
                }
                else
                {
                    values.Add(value);
                }
            }
        }

        private object ConvertValue(ExifDataType dataType, byte[] data, uint numberOfComponents)
        {
            if (data == null || data.Length == 0)
            {
                return null;
            }

            switch (dataType)
            {
                case ExifDataType.Unknown:
                    return null;
                case ExifDataType.Ascii:
                    return ToString(data);
                case ExifDataType.Byte:
                    if (numberOfComponents == 1)
                    {
                        return ToByte(data);
                    }

                    return data;
                case ExifDataType.DoubleFloat:
                    if (numberOfComponents == 1)
                    {
                        return this.ToDouble(data);
                    }

                    return ToArray(dataType, data, this.ToDouble);
                case ExifDataType.Long:
                    if (numberOfComponents == 1)
                    {
                        return this.ToLong(data);
                    }

                    return ToArray(dataType, data, this.ToLong);
                case ExifDataType.Rational:
                    if (numberOfComponents == 1)
                    {
                        return this.ToRational(data);
                    }

                    return ToArray(dataType, data, this.ToRational);
                case ExifDataType.Short:
                    if (numberOfComponents == 1)
                    {
                        return this.ToShort(data);
                    }

                    return ToArray(dataType, data, this.ToShort);
                case ExifDataType.SignedByte:
                    if (numberOfComponents == 1)
                    {
                        return this.ToSignedByte(data);
                    }

                    return ToArray(dataType, data, this.ToSignedByte);
                case ExifDataType.SignedLong:
                    if (numberOfComponents == 1)
                    {
                        return this.ToSignedLong(data);
                    }

                    return ToArray(dataType, data, this.ToSignedLong);
                case ExifDataType.SignedRational:
                    if (numberOfComponents == 1)
                    {
                        return this.ToSignedRational(data);
                    }

                    return ToArray(dataType, data, this.ToSignedRational);
                case ExifDataType.SignedShort:
                    if (numberOfComponents == 1)
                    {
                        return this.ToSignedShort(data);
                    }

                    return ToArray(dataType, data, this.ToSignedShort);
                case ExifDataType.SingleFloat:
                    if (numberOfComponents == 1)
                    {
                        return this.ToSingle(data);
                    }

                    return ToArray(dataType, data, this.ToSingle);
                case ExifDataType.Undefined:
                    if (numberOfComponents == 1)
                    {
                        return ToByte(data);
                    }

                    return data;
                default:
                    throw new NotSupportedException();
            }
        }

        private ExifValue CreateValue()
        {
            if (this.RemainingLength < 12)
            {
                return null;
            }

            ExifTag tag = this.ToEnum(this.GetShort(), ExifTag.Unknown);
            ExifDataType dataType = this.ToEnum(this.GetShort(), ExifDataType.Unknown);
            object value;

            if (dataType == ExifDataType.Unknown)
            {
                return new ExifValue(tag, dataType, null, false);
            }

            uint numberOfComponents = this.GetLong();

            uint size = numberOfComponents * ExifValue.GetSize(dataType);
            byte[] data = this.GetBytes(4);

            if (size > 4)
            {
                uint oldIndex = this.currentIndex;
                this.currentIndex = this.ToLong(data) + this.startIndex;
                if (this.RemainingLength < size)
                {
                    this.invalidTags.Add(tag);
                    this.currentIndex = oldIndex;
                    return null;
                }

                value = this.ConvertValue(dataType, this.GetBytes(size), numberOfComponents);
                this.currentIndex = oldIndex;
            }
            else
            {
                value = this.ConvertValue(dataType, data, numberOfComponents);
            }

            bool isArray = value != null && numberOfComponents > 1;
            return new ExifValue(tag, dataType, value, isArray);
        }

        private TEnum ToEnum<TEnum>(int value, TEnum defaultValue)
            where TEnum : struct
        {
            TEnum enumValue = (TEnum)(object)value;
            if (Enum.GetValues(typeof(TEnum)).Cast<TEnum>().Any(v => v.Equals(enumValue)))
            {
                return enumValue;
            }

            return defaultValue;
        }

        private byte[] GetBytes(uint length)
        {
            if (this.currentIndex + length > (uint)this.exifData.Length)
            {
                return null;
            }

            byte[] data = new byte[length];
            Array.Copy(this.exifData, (int)this.currentIndex, data, 0, (int)length);
            this.currentIndex += length;

            return data;
        }

        private uint GetLong()
        {
            return this.ToLong(this.GetBytes(4));
        }

        private ushort GetShort()
        {
            return this.ToShort(this.GetBytes(2));
        }

        private string GetString(uint length)
        {
            return ToString(this.GetBytes(length));
        }

        private void GetThumbnail(uint offset)
        {
            Collection<ExifValue> values = new Collection<ExifValue>();
            this.AddValues(values, offset);

            foreach (ExifValue value in values)
            {
                if (value.Tag == ExifTag.JPEGInterchangeFormat && (value.DataType == ExifDataType.Long))
                {
                    this.ThumbnailOffset = (uint)value.Value + this.startIndex;
                }
                else if (value.Tag == ExifTag.JPEGInterchangeFormatLength && value.DataType == ExifDataType.Long)
                {
                    this.ThumbnailLength = (uint)value.Value;
                }
            }
        }

        private static TDataType[] ToArray<TDataType>(ExifDataType dataType, byte[] data,
          ConverterMethod<TDataType> converter)
        {
            int dataTypeSize = (int)ExifValue.GetSize(dataType);
            int length = data.Length / dataTypeSize;

            TDataType[] result = new TDataType[length];
            byte[] buffer = new byte[dataTypeSize];

            for (int i = 0; i < length; i++)
            {
                Array.Copy(data, i * dataTypeSize, buffer, 0, dataTypeSize);

                result.SetValue(converter(buffer), i);
            }

            return result;
        }

        private static byte ToByte(byte[] data)
        {
            return data[0];
        }

        private double ToDouble(byte[] data)
        {
            if (!this.ValidateArray(data, 8))
            {
                return default(double);
            }

            return BitConverter.ToDouble(data, 0);
        }

        private uint ToLong(byte[] data)
        {
            if (!this.ValidateArray(data, 4))
            {
                return default(uint);
            }

            return BitConverter.ToUInt32(data, 0);
        }

        private ushort ToShort(byte[] data)
        {

            if (!this.ValidateArray(data, 2))
            {
                return default(ushort);
            }

            return BitConverter.ToUInt16(data, 0);
        }

        private float ToSingle(byte[] data)
        {
            if (!this.ValidateArray(data, 4))
            {
                return default(float);
            }

            return BitConverter.ToSingle(data, 0);
        }

        private static string ToString(byte[] data)
        {
            string result = Encoding.UTF8.GetString(data, 0, data.Length);
            int nullCharIndex = result.IndexOf('\0');
            if (nullCharIndex != -1)
            {
                result = result.Substring(0, nullCharIndex);
            }

            return result;
        }

        private Rational ToRational(byte[] data)
        {
            if (!this.ValidateArray(data, 8, 4))
            {
                return Rational.Zero;
            }

            uint numerator = BitConverter.ToUInt32(data, 0);
            uint denominator = BitConverter.ToUInt32(data, 4);

            return new Rational(numerator, denominator);
        }

        private sbyte ToSignedByte(byte[] data)
        {
            return unchecked((sbyte)data[0]);
        }

        private int ToSignedLong(byte[] data)
        {
            if (!this.ValidateArray(data, 4))
            {
                return default(int);
            }

            return BitConverter.ToInt32(data, 0);
        }

        private Rational ToSignedRational(byte[] data)
        {
            if (!this.ValidateArray(data, 8, 4))
            {
                return Rational.Zero;
            }

            int numerator = BitConverter.ToInt32(data, 0);
            int denominator = BitConverter.ToInt32(data, 4);

            return new Rational(numerator, denominator);
        }

        private short ToSignedShort(byte[] data)
        {
            if (!this.ValidateArray(data, 2))
            {
                return default(short);
            }

            return BitConverter.ToInt16(data, 0);
        }

        private bool ValidateArray(byte[] data, int size)
        {
            return this.ValidateArray(data, size, size);
        }

        private bool ValidateArray(byte[] data, int size, int stepSize)
        {
            if (data == null || data.Length < size)
            {
                return false;
            }

            if (this.isLittleEndian == BitConverter.IsLittleEndian)
            {
                return true;
            }

            for (int i = 0; i < data.Length; i += stepSize)
            {
                Array.Reverse(data, i, stepSize);
            }

            return true;
        }
    }
}