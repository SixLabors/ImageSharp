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

    internal sealed class ExifReader
    {
        private delegate TDataType ConverterMethod<TDataType>(byte[] data);

        private byte[] data;
        private Collection<ExifTag> invalidTags = new Collection<ExifTag>();
        private uint index;
        private bool isLittleEndian;
        private uint exifOffset;
        private uint gpsOffset;
        private uint startIndex;

        public uint ThumbnailLength
        {
            get;
            private set;
        }

        public uint ThumbnailOffset
        {
            get;
            private set;
        }

        private int RemainingLength
        {
            get
            {
                if (this.index >= this.data.Length)
                    return 0;

                return this.data.Length - (int)this.index;
            }
        }

        public Collection<ExifValue> Read(byte[] data)
        {
            Collection<ExifValue> result = new Collection<ExifValue>();

            this.data = data;

            if (GetString(4) == "Exif")
            {
                if (GetShort() != 0)
                    return result;

                this.startIndex = 6;
            }
            else
            {
                this.index = 0;
            }

            this.isLittleEndian = GetString(2) == "II";

            if (GetShort() != 0x002A)
                return result;

            uint ifdOffset = GetLong();
            AddValues(result, ifdOffset);

            uint thumbnailOffset = GetLong();
            GetThumbnail(thumbnailOffset);

            if (this.exifOffset != 0)
                AddValues(result, this.exifOffset);

            if (this.gpsOffset != 0)
                AddValues(result, this.gpsOffset);

            return result;
        }

        public IEnumerable<ExifTag> InvalidTags
        {
            get
            {
                return this.invalidTags;
            }
        }

        private void AddValues(Collection<ExifValue> values, uint index)
        {
            this.index = this.startIndex + index;
            ushort count = GetShort();

            for (ushort i = 0; i < count; i++)
            {
                ExifValue value = CreateValue();
                if (value == null)
                    continue;

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
                    continue;

                if (value.Tag == ExifTag.SubIFDOffset)
                {
                    if (value.DataType == ExifDataType.Long)
                        this.exifOffset = (uint)value.Value;
                }
                else if (value.Tag == ExifTag.GPSIFDOffset)
                {
                    if (value.DataType == ExifDataType.Long)
                        this.gpsOffset = (uint)value.Value;
                }
                else
                    values.Add(value);
            }
        }

        private object ConvertValue(ExifDataType dataType, byte[] data, uint numberOfComponents)
        {
            if (data == null || data.Length == 0)
                return null;

            switch (dataType)
            {
                case ExifDataType.Unknown:
                    return null;
                case ExifDataType.Ascii:
                    return ToString(data);
                case ExifDataType.Byte:
                    if (numberOfComponents == 1)
                        return ToByte(data);
                    else
                        return data;
                case ExifDataType.DoubleFloat:
                    if (numberOfComponents == 1)
                        return ToDouble(data);
                    else
                        return ToArray(dataType, data, ToDouble);
                case ExifDataType.Long:
                    if (numberOfComponents == 1)
                        return ToLong(data);
                    else
                        return ToArray(dataType, data, ToLong);
                case ExifDataType.Rational:
                    if (numberOfComponents == 1)
                        return ToRational(data);
                    else
                        return ToArray(dataType, data, ToRational);
                case ExifDataType.Short:
                    if (numberOfComponents == 1)
                        return ToShort(data);
                    else
                        return ToArray(dataType, data, ToShort);
                case ExifDataType.SignedByte:
                    if (numberOfComponents == 1)
                        return ToSignedByte(data);
                    else
                        return ToArray(dataType, data, ToSignedByte);
                case ExifDataType.SignedLong:
                    if (numberOfComponents == 1)
                        return ToSignedLong(data);
                    else
                        return ToArray(dataType, data, ToSignedLong);
                case ExifDataType.SignedRational:
                    if (numberOfComponents == 1)
                        return ToSignedRational(data);
                    else
                        return ToArray(dataType, data, ToSignedRational);
                case ExifDataType.SignedShort:
                    if (numberOfComponents == 1)
                        return ToSignedShort(data);
                    else
                        return ToArray(dataType, data, ToSignedShort);
                case ExifDataType.SingleFloat:
                    if (numberOfComponents == 1)
                        return ToSingle(data);
                    else
                        return ToArray(dataType, data, ToSingle);
                case ExifDataType.Undefined:
                    if (numberOfComponents == 1)
                        return ToByte(data);
                    else
                        return data;
                default:
                    throw new NotImplementedException();
            }
        }

        private ExifValue CreateValue()
        {
            if (RemainingLength < 12)
                return null;

            ExifTag tag = ToEnum(GetShort(), ExifTag.Unknown);
            ExifDataType dataType = ToEnum(GetShort(), ExifDataType.Unknown);
            object value = null;

            if (dataType == ExifDataType.Unknown)
                return new ExifValue(tag, dataType, value, false);

            uint numberOfComponents = (uint)GetLong();

            uint size = numberOfComponents * ExifValue.GetSize(dataType);
            byte[] data = GetBytes(4);

            if (size > 4)
            {
                uint oldIndex = this.index;
                this.index = ToLong(data) + this.startIndex;
                if (RemainingLength < size)
                {
                    this.invalidTags.Add(tag);
                    this.index = oldIndex;
                    return null;
                }
                value = ConvertValue(dataType, GetBytes(size), numberOfComponents);
                this.index = oldIndex;
            }
            else
            {
                value = ConvertValue(dataType, data, numberOfComponents);
            }

            bool isArray = value != null && numberOfComponents > 1;
            return new ExifValue(tag, dataType, value, isArray);
        }

        private TEnum ToEnum<TEnum>(int value, TEnum defaultValue)
            where TEnum : struct
        {
            TEnum enumValue = (TEnum)(object)value;
            if (Enum.GetValues(typeof(TEnum)).Cast<TEnum>().Any(v => v.Equals(enumValue)))
                return enumValue;

            return defaultValue;
        }

        private byte[] GetBytes(uint length)
        {
            if (this.index + length > (uint)this.data.Length)
                return null;

            byte[] data = new byte[length];
            Array.Copy(this.data, (int)this.index, data, 0, (int)length);
            this.index += length;

            return data;
        }

        private uint GetLong()
        {
            return ToLong(GetBytes(4));
        }

        private ushort GetShort()
        {
            return ToShort(GetBytes(2));
        }

        private string GetString(uint length)
        {
            return ToString(GetBytes(length));
        }

        private void GetThumbnail(uint offset)
        {
            Collection<ExifValue> values = new Collection<ExifValue>();
            AddValues(values, offset);

            foreach (ExifValue value in values)
            {
                if (value.Tag == ExifTag.JPEGInterchangeFormat && (value.DataType == ExifDataType.Long))
                    ThumbnailOffset = (uint)value.Value + this.startIndex;
                else if (value.Tag == ExifTag.JPEGInterchangeFormatLength && value.DataType == ExifDataType.Long)
                    ThumbnailLength = (uint)value.Value;
            }
        }

        private static TDataType[] ToArray<TDataType>(ExifDataType dataType, Byte[] data,
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
            if (!ValidateArray(data, 8))
                return default(double);

            return BitConverter.ToDouble(data, 0);
        }

        private uint ToLong(byte[] data)
        {
            if (!ValidateArray(data, 4))
                return default(uint);

            return BitConverter.ToUInt32(data, 0);
        }

        private ushort ToShort(byte[] data)
        {

            if (!ValidateArray(data, 2))
                return default(ushort);

            return BitConverter.ToUInt16(data, 0);
        }

        private float ToSingle(byte[] data)
        {
            if (!ValidateArray(data, 4))
                return default(float);

            return BitConverter.ToSingle(data, 0);
        }

        private static string ToString(byte[] data)
        {
            string result = Encoding.UTF8.GetString(data, 0, data.Length);
            int nullCharIndex = result.IndexOf('\0');
            if (nullCharIndex != -1)
                result = result.Substring(0, nullCharIndex);

            return result;
        }

        private double ToRational(byte[] data)
        {
            if (!ValidateArray(data, 8, 4))
                return default(double);

            uint numerator = BitConverter.ToUInt32(data, 0);
            uint denominator = BitConverter.ToUInt32(data, 4);

            return numerator / (double)denominator;
        }

        private sbyte ToSignedByte(byte[] data)
        {
            return unchecked((sbyte)data[0]);
        }

        private int ToSignedLong(byte[] data)
        {
            if (!ValidateArray(data, 4))
                return default(int);

            return BitConverter.ToInt32(data, 0);
        }

        private double ToSignedRational(byte[] data)
        {
            if (!ValidateArray(data, 8, 4))
                return default(double);

            int numerator = BitConverter.ToInt32(data, 0);
            int denominator = BitConverter.ToInt32(data, 4);

            return numerator / (double)denominator;
        }

        private short ToSignedShort(byte[] data)
        {
            if (!ValidateArray(data, 2))
                return default(short);

            return BitConverter.ToInt16(data, 0);
        }

        private bool ValidateArray(byte[] data, int size)
        {
            return ValidateArray(data, size, size);
        }

        private bool ValidateArray(byte[] data, int size, int stepSize)
        {
            if (data == null || data.Length < size)
                return false;

            if (this.isLittleEndian == BitConverter.IsLittleEndian)
                return true;

            for (int i = 0; i < data.Length; i += stepSize)
            {
                Array.Reverse(data, i, stepSize);
            }

            return true;
        }
    }
}