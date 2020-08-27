// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    internal class EntryReader
    {
        private readonly TiffStream stream;

        private readonly SortedDictionary<uint, Action> extValueLoaders = new SortedDictionary<uint, Action>();

        /// <summary>
        /// Initializes a new instance of the <see cref="EntryReader" /> class.
        /// </summary>
        /// <param name="stream">The stream.</param>
        public EntryReader(TiffStream stream)
        {
            this.stream = stream;
        }

        public IExifValue ReadNext()
        {
            var tagId = (ExifTagValue)this.stream.ReadUInt16();
            var dataType = (ExifDataType)EnumUtils.Parse(this.stream.ReadUInt16(), ExifDataType.Unknown);
            uint count = this.stream.ReadUInt32();

            ExifDataType rawDataType = dataType;
            dataType = LongOrShortFiltering(tagId, dataType);
            bool isArray = GetIsArray(tagId, count);

            ExifValue entry = ExifValues.Create(tagId, dataType, isArray);
            if (rawDataType == ExifDataType.Undefined && count == 0)
            {
                // todo: investgate
                count = 4;
            }

            if (this.ReadValueOrOffset(entry, rawDataType, count))
            {
                return entry;
            }

            return null; // new UnkownExifTag(tagId);
        }

        public void LoadExtendedData()
        {
            foreach (Action action in this.extValueLoaders.Values)
            {
                action();
            }
        }

        private static bool HasExtData(ExifValue tag, uint count) => ExifDataTypes.GetSize(tag.DataType) * count > 4;

        private static bool SetValue(ExifValue entry, object value)
        {
            if (!entry.IsArray && entry.DataType != ExifDataType.Ascii)
            {
                DebugGuard.IsTrue(((Array)value).Length == 1, "Expected a length is 1");

                var single = ((Array)value).GetValue(0);
                return entry.TrySetValue(single);
            }

            return entry.TrySetValue(value);
        }

        private static ExifDataType LongOrShortFiltering(ExifTagValue tagId, ExifDataType dataType)
        {
            switch (tagId)
            {
                case ExifTagValue.ImageWidth:
                case ExifTagValue.ImageLength:
                case ExifTagValue.StripOffsets:
                case ExifTagValue.RowsPerStrip:
                case ExifTagValue.StripByteCounts:
                case ExifTagValue.TileWidth:
                case ExifTagValue.TileLength:
                case ExifTagValue.TileOffsets:
                case ExifTagValue.TileByteCounts:
                case ExifTagValue.OldSubfileType: // by spec SHORT, but can be LONG
                    return ExifDataType.Long;

                default:
                    return dataType;
            }
        }

        private static bool GetIsArray(ExifTagValue tagId, uint count)
        {
            switch (tagId)
            {
                case ExifTagValue.BitsPerSample:
                case ExifTagValue.StripOffsets:
                case ExifTagValue.StripByteCounts:
                case ExifTagValue.TileOffsets:
                case ExifTagValue.TileByteCounts:
                case ExifTagValue.ColorMap:
                case ExifTagValue.ExtraSamples:
                case ExifTagValue.SampleFormat:
                    return true;

                default:
                    return count > 1;
            }
        }

        private bool ReadValueOrOffset(ExifValue entry, ExifDataType rawDataType, uint count)
        {
            if (HasExtData(entry, count))
            {
                uint offset = this.stream.ReadUInt32();
                this.extValueLoaders.Add(offset, () =>
                {
                    this.ReadExtValue(entry, rawDataType, offset, count);
                });

                return true;
            }

            long pos = this.stream.Position;
            object value = this.ReadData(entry.DataType, rawDataType, count);
            if (value == null)
            {
                // read unknown type value
                value = this.stream.ReadBytes(4);
            }
            else
            {
                int leftBytes = 4 - (int)(this.stream.Position - pos);
                if (leftBytes > 0)
                {
                    this.stream.Skip(leftBytes);
                }
                else if (leftBytes < 0)
                {
                    throw new InvalidDataException("Out of range of IFD entry structure.");
                }
            }

            return SetValue(entry, value);
        }

        private void ReadExtValue(ExifValue entry, ExifDataType rawDataType, uint offset, uint count)
        {
            DebugGuard.IsTrue(HasExtData(entry, count), "Excepted extended data");
            DebugGuard.MustBeGreaterThanOrEqualTo(offset, (uint)TiffConstants.SizeOfTiffHeader, nameof(offset));

            this.stream.Seek(offset);
            var value = this.ReadData(entry.DataType, rawDataType, count);

            SetValue(entry, value);

            DebugGuard.IsTrue(entry.DataType == ExifDataType.Ascii || count > 1 ^ !entry.IsArray, "Invalid tag");
            DebugGuard.IsTrue(entry.GetValue() != null, "Invalid tag");
        }

        private object ReadData(ExifDataType entryDataType, ExifDataType rawDataType, uint count)
        {
            switch (rawDataType)
            {
                case ExifDataType.Byte:
                case ExifDataType.Undefined:
                {
                    return this.stream.ReadBytes(count);
                }

                case ExifDataType.SignedByte:
                {
                    sbyte[] res = new sbyte[count];
                    byte[] buf = this.stream.ReadBytes(count);
                    Array.Copy(buf, res, buf.Length);
                    return res;
                }

                case ExifDataType.Short:
                {
                    if (entryDataType == ExifDataType.Long)
                    {
                        uint[] buf = new uint[count];
                        for (int i = 0; i < buf.Length; i++)
                        {
                            buf[i] = this.stream.ReadUInt16();
                        }

                        return buf;
                    }
                    else
                    {
                        ushort[] buf = new ushort[count];
                        for (int i = 0; i < buf.Length; i++)
                        {
                            buf[i] = this.stream.ReadUInt16();
                        }

                        return buf;
                    }
                }

                case ExifDataType.SignedShort:
                {
                    short[] buf = new short[count];
                    for (int i = 0; i < buf.Length; i++)
                    {
                        buf[i] = this.stream.ReadInt16();
                    }

                    return buf;
                }

                case ExifDataType.Long:
                {
                    uint[] buf = new uint[count];
                    for (int i = 0; i < buf.Length; i++)
                    {
                        buf[i] = this.stream.ReadUInt32();
                    }

                    return buf;
                }

                case ExifDataType.SignedLong:
                {
                    int[] buf = new int[count];
                    for (int i = 0; i < buf.Length; i++)
                    {
                        buf[i] = this.stream.ReadInt32();
                    }

                    return buf;
                }

                case ExifDataType.Ascii:
                {
                    byte[] buf = this.stream.ReadBytes(count);

                    if (buf[buf.Length - 1] != 0)
                    {
                        throw new ImageFormatException("The retrieved string is not null terminated.");
                    }

                    return Encoding.UTF8.GetString(buf, 0, buf.Length - 1);
                }

                case ExifDataType.SingleFloat:
                {
                    float[] buf = new float[count];
                    for (int i = 0; i < buf.Length; i++)
                    {
                        buf[i] = this.stream.ReadSingle();
                    }

                    return buf;
                }

                case ExifDataType.DoubleFloat:
                {
                    double[] buf = new double[count];
                    for (int i = 0; i < buf.Length; i++)
                    {
                        buf[i] = this.stream.ReadDouble();
                    }

                    return buf;
                }

                case ExifDataType.Rational:
                {
                    var buf = new Rational[count];
                    for (int i = 0; i < buf.Length; i++)
                    {
                        uint numerator = this.stream.ReadUInt32();
                        uint denominator = this.stream.ReadUInt32();
                        buf[i] = new Rational(numerator, denominator);
                    }

                    return buf;
                }

                case ExifDataType.SignedRational:
                {
                    var buf = new SignedRational[count];
                    for (int i = 0; i < buf.Length; i++)
                    {
                        int numerator = this.stream.ReadInt32();
                        int denominator = this.stream.ReadInt32();
                        buf[i] = new SignedRational(numerator, denominator);
                    }

                    return buf;
                }

                case ExifDataType.Ifd:
                {
                    return this.stream.ReadUInt32();
                }

                default:
                    return null;
            }
        }
    }
}
