// <copyright file="TiffDecoderCore.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Performs the tiff decoding operation.
    /// </summary>
    internal class TiffDecoderCore : IDisposable
    {
        /// <summary>
        /// The decoder options.
        /// </summary>
        private readonly IDecoderOptions options;

        /// <summary>
        /// Initializes a new instance of the <see cref="TiffDecoderCore" /> class.
        /// </summary>
        /// <param name="options">The decoder options.</param>
        public TiffDecoderCore(IDecoderOptions options)
        {
            this.options = options ?? new DecoderOptions();
        }

        public TiffDecoderCore(Stream stream, bool isLittleEndian, IDecoderOptions options) : this(options)
        {
            this.InputStream = stream;
            this.IsLittleEndian = isLittleEndian;
        }

        /// <summary>
        /// Gets the input stream.
        /// </summary>
        public Stream InputStream { get; private set; }

        /// <summary>
        /// A flag indicating if the file is encoded in little-endian or big-endian format.
        /// </summary>
        public bool IsLittleEndian;

        /// <summary>
        /// Decodes the image from the specified <see cref="Stream"/>  and sets
        /// the data to image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="image">The image, where the data should be set to.</param>
        /// <param name="stream">The stream, where the image should be.</param>
        /// <param name="metadataOnly">Whether to decode metadata only.</param>
        public void Decode<TColor>(Image<TColor> image, Stream stream, bool metadataOnly)
            where TColor : struct, IPixel<TColor>
        {
            this.InputStream = stream;

            uint firstIfdOffset = ReadHeader();
            TiffIfd firstIfd = ReadIfd(firstIfdOffset);
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
        }

        public uint ReadHeader()
        {
            byte[] headerBytes = new byte[TiffConstants.SizeOfTiffHeader];
            ReadBytes(headerBytes, TiffConstants.SizeOfTiffHeader);

            if (headerBytes[0] == TiffConstants.ByteOrderLittleEndian && headerBytes[1] == TiffConstants.ByteOrderLittleEndian)
                IsLittleEndian = true;
            else if (headerBytes[0] != TiffConstants.ByteOrderBigEndian && headerBytes[1] != TiffConstants.ByteOrderBigEndian)
                throw new ImageFormatException("Invalid TIFF file header.");

            if (ToUInt16(headerBytes, 2) != TiffConstants.HeaderMagicNumber)
                throw new ImageFormatException("Invalid TIFF file header.");

            uint firstIfdOffset = ToUInt32(headerBytes, 4);
            if (firstIfdOffset == 0)
                throw new ImageFormatException("Invalid TIFF file header.");

            return firstIfdOffset;
        }

        public TiffIfd ReadIfd(uint offset)
        {
            InputStream.Seek(offset, SeekOrigin.Begin);
            
            byte[] buffer = new byte[TiffConstants.SizeOfIfdEntry];

            ReadBytes(buffer, 2);
            ushort entryCount = ToUInt16(buffer, 0);

            TiffIfdEntry[] entries = new TiffIfdEntry[entryCount];
            for (int i = 0 ; i<entryCount; i++)
            {
                ReadBytes(buffer, TiffConstants.SizeOfIfdEntry);

                ushort tag = ToUInt16(buffer, 0);
                TiffType type = (TiffType)ToUInt16(buffer, 2);
                uint count = ToUInt32(buffer, 4);
                byte[] value = new byte[] { buffer[8], buffer[9], buffer[10], buffer[11] };

                entries[i] = new TiffIfdEntry(tag, type, count, value);
            }

            ReadBytes(buffer, 4);
            uint nextIfdOffset = ToUInt32(buffer, 0);

            return new TiffIfd(entries, nextIfdOffset);
        }

        private void ReadBytes(byte[] buffer, int count)
        {
            int offset = 0;

            while (count > 0)
            {
                int bytesRead = InputStream.Read(buffer, offset, count);

                if (bytesRead == 0)
                    break;

                offset += bytesRead;
                count -= bytesRead;
            }
        }

        public byte[] ReadBytes(ref TiffIfdEntry entry)
        {
            uint byteLength = GetSizeOfData(entry);

            if (entry.Value.Length < byteLength)
            {
                uint offset = ToUInt32(entry.Value, 0);
                InputStream.Seek(offset, SeekOrigin.Begin);

                byte[] data = new byte[byteLength];
                ReadBytes(data, (int)byteLength);
                entry.Value = data;
            }

            return entry.Value;
        }

        public uint ReadUnsignedInteger(ref TiffIfdEntry entry)
        {
            if (entry.Count != 1)
                throw new ImageFormatException($"Cannot read a single value from an array of multiple items.");

            switch (entry.Type)
            {
                case TiffType.Byte:
                    return (uint)ToByte(entry.Value, 0);
                case TiffType.Short:
                    return (uint)ToUInt16(entry.Value, 0);
                case TiffType.Long:
                    return ToUInt32(entry.Value, 0);
                default:
                    throw new ImageFormatException($"A value of type '{entry.Type}' cannot be converted to an unsigned integer.");
            }
        }

        public int ReadSignedInteger(ref TiffIfdEntry entry)
        {
            if (entry.Count != 1)
                throw new ImageFormatException($"Cannot read a single value from an array of multiple items.");

            switch (entry.Type)
            {
                case TiffType.SByte:
                    return (int)ToSByte(entry.Value, 0);
                case TiffType.SShort:
                    return (int)ToInt16(entry.Value, 0);
                case TiffType.SLong:
                    return ToInt32(entry.Value, 0);
                default:
                    throw new ImageFormatException($"A value of type '{entry.Type}' cannot be converted to a signed integer.");
            }
        }

        public uint[] ReadUnsignedIntegerArray(ref TiffIfdEntry entry)
        {
            byte[] bytes = ReadBytes(ref entry);
            uint[] result = new uint[entry.Count];

            switch (entry.Type)
            {
                case TiffType.Byte:
                {
                    for (int i = 0 ; i < result.Length ; i++)
                        result[i] = (uint)ToByte(bytes, i);
                    break;
                }
                case TiffType.Short:
                {
                    for (int i = 0 ; i < result.Length ; i++)
                        result[i] = (uint)ToUInt16(bytes, i * TiffConstants.SizeOfShort);
                    break;
                }
                case TiffType.Long:
                {
                    for (int i = 0 ; i < result.Length ; i++)
                        result[i] = ToUInt32(bytes, i * TiffConstants.SizeOfLong);
                    break;
                }
                default:
                    throw new ImageFormatException($"A value of type '{entry.Type}' cannot be converted to an unsigned integer.");
            }

            return result;
        }

        public int[] ReadSignedIntegerArray(ref TiffIfdEntry entry)
        {
            byte[] bytes = ReadBytes(ref entry);
            int[] result = new int[entry.Count];

            switch (entry.Type)
            {
                case TiffType.SByte:
                {
                    for (int i = 0 ; i < result.Length ; i++)
                        result[i] = (int)ToSByte(bytes, i);
                    break;
                }
                case TiffType.SShort:
                {
                    for (int i = 0 ; i < result.Length ; i++)
                        result[i] = (int)ToInt16(bytes, i * TiffConstants.SizeOfShort);
                    break;
                }
                case TiffType.SLong:
                {
                    for (int i = 0 ; i < result.Length ; i++)
                        result[i] = ToInt32(bytes, i * TiffConstants.SizeOfLong);
                    break;
                }
                default:
                    throw new ImageFormatException($"A value of type '{entry.Type}' cannot be converted to a signed integer.");
            }

            return result;
        }

        public string ReadString(ref TiffIfdEntry entry)
        {
            if (entry.Type != TiffType.Ascii)
                throw new ImageFormatException($"A value of type '{entry.Type}' cannot be converted to a string.");

            byte[] bytes = ReadBytes(ref entry);
            
            if (bytes[entry.Count - 1] != 0)
                throw new ImageFormatException("The retrieved string is not null terminated.");

            return Encoding.UTF8.GetString(bytes, 0, (int)entry.Count - 1);
        }

        public Rational ReadUnsignedRational(ref TiffIfdEntry entry)
        {
            if (entry.Count != 1)
                throw new ImageFormatException($"Cannot read a single value from an array of multiple items.");

            return ReadUnsignedRationalArray(ref entry)[0];
        }

        public SignedRational ReadSignedRational(ref TiffIfdEntry entry)
        {
            if (entry.Count != 1)
                throw new ImageFormatException($"Cannot read a single value from an array of multiple items.");

            return ReadSignedRationalArray(ref entry)[0];
        }

        public Rational[] ReadUnsignedRationalArray(ref TiffIfdEntry entry)
        {
            if (entry.Type != TiffType.Rational)
                throw new ImageFormatException($"A value of type '{entry.Type}' cannot be converted to a Rational.");

            byte[] bytes = ReadBytes(ref entry);
            Rational[] result = new Rational[entry.Count];

            for (int i = 0 ; i < result.Length ; i++)
            {
                uint numerator = ToUInt32(bytes, i * TiffConstants.SizeOfRational);
                uint denominator = ToUInt32(bytes, i * TiffConstants.SizeOfRational + TiffConstants.SizeOfLong);
                result[i] = new Rational(numerator, denominator);
            }

            return result;
        }

        public SignedRational[] ReadSignedRationalArray(ref TiffIfdEntry entry)
        {
            if (entry.Type != TiffType.SRational)
                throw new ImageFormatException($"A value of type '{entry.Type}' cannot be converted to a SignedRational.");

            byte[] bytes = ReadBytes(ref entry);
            SignedRational[] result = new SignedRational[entry.Count];

            for (int i = 0 ; i < result.Length ; i++)
            {
                int numerator = ToInt32(bytes, i * TiffConstants.SizeOfRational);
                int denominator = ToInt32(bytes, i * TiffConstants.SizeOfRational + TiffConstants.SizeOfLong);
                result[i] = new SignedRational(numerator, denominator);
            }

            return result;
        }

        private SByte ToSByte(byte[] bytes, int offset)
        {
            return (sbyte)bytes[offset];
        }

        private Int16 ToInt16(byte[] bytes, int offset)
        {
            if (IsLittleEndian)
                return (short)(bytes[offset + 0] | (bytes[offset + 1] << 8));
            else
                return (short)((bytes[offset + 0] << 8) | bytes[offset + 1]);
        }

        private Int32 ToInt32(byte[] bytes, int offset)
        {
            if (IsLittleEndian)
                return bytes[offset + 0] | (bytes[offset + 1] << 8) | (bytes[offset + 2] << 16) | (bytes[offset + 3] << 24);
            else
                return (bytes[offset + 0] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];
        }

        private Byte ToByte(byte[] bytes, int offset)
        {
            return bytes[offset];
        }

        private UInt32 ToUInt32(byte[] bytes, int offset)
        {
            return (uint)ToInt32(bytes, offset);
        }

        private UInt16 ToUInt16(byte[] bytes, int offset)
        {
            return (ushort)ToInt16(bytes, offset);
        }

        public static uint GetSizeOfData(TiffIfdEntry entry) => SizeOfDataType(entry.Type) * entry.Count;

        private static uint SizeOfDataType(TiffType type)
        {
            switch (type)
            {
                case TiffType.Byte:
                case TiffType.Ascii:
                case TiffType.SByte:
                case TiffType.Undefined:
                    return 1u;
                case TiffType.Short:
                case TiffType.SShort:
                    return 2u;
                case TiffType.Long:
                case TiffType.SLong:
                case TiffType.Float:
                case TiffType.Ifd:
                    return 4u;
                case TiffType.Rational:
                case TiffType.SRational:
                case TiffType.Double:
                    return 8u;
                default:
                    return 0u;
            }
        }
    }
}
