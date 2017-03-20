// <copyright file="TiffDecoderCore.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System;
    using System.IO;
    using System.Text;

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

        /// <summary>
        /// Initializes a new instance of the <see cref="TiffDecoderCore" /> class.
        /// </summary>
        /// <param name="stream">The input stream.</param>
        /// <param name="isLittleEndian">A flag indicating if the file is encoded in little-endian or big-endian format.</param>
        /// <param name="options">The decoder options.</param>
        public TiffDecoderCore(Stream stream, bool isLittleEndian, IDecoderOptions options)
            : this(options)
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
        public bool IsLittleEndian { get; private set; }

        /// <summary>
        /// Calculates the size (in bytes) of the data contained within an IFD entry.
        /// </summary>
        /// <param name="entry">The IFD entry to calculate the size for.</param>
        /// <returns>The size of the data (in bytes).</returns>
        public static uint GetSizeOfData(TiffIfdEntry entry) => SizeOfDataType(entry.Type) * entry.Count;

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

            uint firstIfdOffset = this.ReadHeader();
            TiffIfd firstIfd = this.ReadIfd(firstIfdOffset);
            this.DecodeImage(firstIfd, image);
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
        }

        /// <summary>
        /// Reads the TIFF header from the input stream.
        /// </summary>
        /// <returns>The byte offset to the first IFD in the file.</returns>
        /// <exception cref="ImageFormatException">
        /// Thrown if the TIFF file header is invalid.
        /// </exception>
        public uint ReadHeader()
        {
            byte[] headerBytes = new byte[TiffConstants.SizeOfTiffHeader];
            this.ReadBytes(headerBytes, TiffConstants.SizeOfTiffHeader);

            if (headerBytes[0] == TiffConstants.ByteOrderLittleEndian && headerBytes[1] == TiffConstants.ByteOrderLittleEndian)
            {
                this.IsLittleEndian = true;
            }
            else if (headerBytes[0] != TiffConstants.ByteOrderBigEndian && headerBytes[1] != TiffConstants.ByteOrderBigEndian)
            {
                throw new ImageFormatException("Invalid TIFF file header.");
            }

            if (this.ToUInt16(headerBytes, 2) != TiffConstants.HeaderMagicNumber)
            {
                throw new ImageFormatException("Invalid TIFF file header.");
            }

            uint firstIfdOffset = this.ToUInt32(headerBytes, 4);
            if (firstIfdOffset == 0)
            {
                throw new ImageFormatException("Invalid TIFF file header.");
            }

            return firstIfdOffset;
        }

        /// <summary>
        /// Reads a <see cref="TiffIfd"/> from the input stream.
        /// </summary>
        /// <param name="offset">The byte offset within the file to find the IFD.</param>
        /// <returns>A <see cref="TiffIfd"/> containing the retrieved data.</returns>
        public TiffIfd ReadIfd(uint offset)
        {
            this.InputStream.Seek(offset, SeekOrigin.Begin);

            byte[] buffer = new byte[TiffConstants.SizeOfIfdEntry];

            this.ReadBytes(buffer, 2);
            ushort entryCount = this.ToUInt16(buffer, 0);

            TiffIfdEntry[] entries = new TiffIfdEntry[entryCount];
            for (int i = 0; i < entryCount; i++)
            {
                this.ReadBytes(buffer, TiffConstants.SizeOfIfdEntry);

                ushort tag = this.ToUInt16(buffer, 0);
                TiffType type = (TiffType)this.ToUInt16(buffer, 2);
                uint count = this.ToUInt32(buffer, 4);
                byte[] value = new byte[] { buffer[8], buffer[9], buffer[10], buffer[11] };

                entries[i] = new TiffIfdEntry(tag, type, count, value);
            }

            this.ReadBytes(buffer, 4);
            uint nextIfdOffset = this.ToUInt32(buffer, 0);

            return new TiffIfd(entries, nextIfdOffset);
        }

        /// <summary>
        /// Decodes the image data from a specified IFD.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="ifd">The IFD to read the image from.</param>
        /// <param name="image">The image, where the data should be set to.</param>
        public void DecodeImage<TColor>(TiffIfd ifd, Image<TColor> image)
            where TColor : struct, IPixel<TColor>
        {
            if (!ifd.TryGetIfdEntry(TiffTags.ImageLength, out TiffIfdEntry imageLengthEntry)
                || !ifd.TryGetIfdEntry(TiffTags.ImageWidth, out TiffIfdEntry imageWidthEntry))
            {
                throw new ImageFormatException("The TIFF IFD does not specify the image dimensions.");
            }

            int width = (int)this.ReadUnsignedInteger(ref imageWidthEntry);
            int height = (int)this.ReadUnsignedInteger(ref imageLengthEntry);

            image.InitPixels(width, height);
        }

        /// <summary>
        /// Reads the data from a <see cref="TiffIfdEntry"/> as an array of bytes.
        /// </summary>
        /// <param name="entry">The <see cref="TiffIfdEntry"/> to read.</param>
        /// <returns>The data.</returns>
        public byte[] ReadBytes(ref TiffIfdEntry entry)
        {
            uint byteLength = GetSizeOfData(entry);

            if (entry.Value.Length < byteLength)
            {
                uint offset = this.ToUInt32(entry.Value, 0);
                this.InputStream.Seek(offset, SeekOrigin.Begin);

                byte[] data = new byte[byteLength];
                this.ReadBytes(data, (int)byteLength);
                entry.Value = data;
            }

            return entry.Value;
        }

        /// <summary>
        /// Reads the data from a <see cref="TiffIfdEntry"/> as an unsigned integer value.
        /// </summary>
        /// <param name="entry">The <see cref="TiffIfdEntry"/> to read.</param>
        /// <returns>The data.</returns>
        /// <exception cref="ImageFormatException">
        /// Thrown if the data-type specified by the file cannot be converted to a <see cref="uint"/>, or if
        /// there is an array of items.
        /// </exception>
        public uint ReadUnsignedInteger(ref TiffIfdEntry entry)
        {
            if (entry.Count != 1)
            {
                throw new ImageFormatException($"Cannot read a single value from an array of multiple items.");
            }

            switch (entry.Type)
            {
                case TiffType.Byte:
                    return (uint)this.ToByte(entry.Value, 0);
                case TiffType.Short:
                    return (uint)this.ToUInt16(entry.Value, 0);
                case TiffType.Long:
                    return this.ToUInt32(entry.Value, 0);
                default:
                    throw new ImageFormatException($"A value of type '{entry.Type}' cannot be converted to an unsigned integer.");
            }
        }

        /// <summary>
        /// Reads the data from a <see cref="TiffIfdEntry"/> as a signed integer value.
        /// </summary>
        /// <param name="entry">The <see cref="TiffIfdEntry"/> to read.</param>
        /// <returns>The data.</returns>
        /// <exception cref="ImageFormatException">
        /// Thrown if the data-type specified by the file cannot be converted to an <see cref="int"/>, or if
        /// there is an array of items.
        /// </exception>
        public int ReadSignedInteger(ref TiffIfdEntry entry)
        {
            if (entry.Count != 1)
            {
                throw new ImageFormatException($"Cannot read a single value from an array of multiple items.");
            }

            switch (entry.Type)
            {
                case TiffType.SByte:
                    return (int)this.ToSByte(entry.Value, 0);
                case TiffType.SShort:
                    return (int)this.ToInt16(entry.Value, 0);
                case TiffType.SLong:
                    return this.ToInt32(entry.Value, 0);
                default:
                    throw new ImageFormatException($"A value of type '{entry.Type}' cannot be converted to a signed integer.");
            }
        }

        /// <summary>
        /// Reads the data from a <see cref="TiffIfdEntry"/> as an array of unsigned integer values.
        /// </summary>
        /// <param name="entry">The <see cref="TiffIfdEntry"/> to read.</param>
        /// <returns>The data.</returns>
        /// <exception cref="ImageFormatException">
        /// Thrown if the data-type specified by the file cannot be converted to a <see cref="uint"/>.
        /// </exception>
        public uint[] ReadUnsignedIntegerArray(ref TiffIfdEntry entry)
        {
            byte[] bytes = this.ReadBytes(ref entry);
            uint[] result = new uint[entry.Count];

            switch (entry.Type)
            {
                case TiffType.Byte:
                    {
                        for (int i = 0; i < result.Length; i++)
                        {
                            result[i] = (uint)this.ToByte(bytes, i);
                        }

                        break;
                    }

                case TiffType.Short:
                    {
                        for (int i = 0; i < result.Length; i++)
                        {
                            result[i] = (uint)this.ToUInt16(bytes, i * TiffConstants.SizeOfShort);
                        }

                        break;
                    }

                case TiffType.Long:
                    {
                        for (int i = 0; i < result.Length; i++)
                        {
                            result[i] = this.ToUInt32(bytes, i * TiffConstants.SizeOfLong);
                        }

                        break;
                    }

                default:
                    throw new ImageFormatException($"A value of type '{entry.Type}' cannot be converted to an unsigned integer.");
            }

            return result;
        }

        /// <summary>
        /// Reads the data from a <see cref="TiffIfdEntry"/> as an array of signed integer values.
        /// </summary>
        /// <param name="entry">The <see cref="TiffIfdEntry"/> to read.</param>
        /// <returns>The data.</returns>
        /// <exception cref="ImageFormatException">
        /// Thrown if the data-type specified by the file cannot be converted to an <see cref="int"/>.
        /// </exception>
        public int[] ReadSignedIntegerArray(ref TiffIfdEntry entry)
        {
            byte[] bytes = this.ReadBytes(ref entry);
            int[] result = new int[entry.Count];

            switch (entry.Type)
            {
                case TiffType.SByte:
                    {
                        for (int i = 0; i < result.Length; i++)
                        {
                            result[i] = (int)this.ToSByte(bytes, i);
                        }

                        break;
                    }

                case TiffType.SShort:
                    {
                        for (int i = 0; i < result.Length; i++)
                        {
                            result[i] = (int)this.ToInt16(bytes, i * TiffConstants.SizeOfShort);
                        }

                        break;
                    }

                case TiffType.SLong:
                    {
                        for (int i = 0; i < result.Length; i++)
                        {
                            result[i] = this.ToInt32(bytes, i * TiffConstants.SizeOfLong);
                        }

                        break;
                    }

                default:
                    throw new ImageFormatException($"A value of type '{entry.Type}' cannot be converted to a signed integer.");
            }

            return result;
        }

        /// <summary>
        /// Reads the data from a <see cref="TiffIfdEntry"/> as a <see cref="string"/> value.
        /// </summary>
        /// <param name="entry">The <see cref="TiffIfdEntry"/> to read.</param>
        /// <returns>The data.</returns>
        /// <exception cref="ImageFormatException">
        /// Thrown if the data-type specified by the file cannot be converted to a <see cref="string"/>.
        /// </exception>
        public string ReadString(ref TiffIfdEntry entry)
        {
            if (entry.Type != TiffType.Ascii)
            {
                throw new ImageFormatException($"A value of type '{entry.Type}' cannot be converted to a string.");
            }

            byte[] bytes = this.ReadBytes(ref entry);

            if (bytes[entry.Count - 1] != 0)
            {
                throw new ImageFormatException("The retrieved string is not null terminated.");
            }

            return Encoding.UTF8.GetString(bytes, 0, (int)entry.Count - 1);
        }

        /// <summary>
        /// Reads the data from a <see cref="TiffIfdEntry"/> as a <see cref="Rational"/> value.
        /// </summary>
        /// <param name="entry">The <see cref="TiffIfdEntry"/> to read.</param>
        /// <returns>The data.</returns>
        /// <exception cref="ImageFormatException">
        /// Thrown if the data-type specified by the file cannot be converted to a <see cref="Rational"/>, or if
        /// there is an array of items.
        /// </exception>
        public Rational ReadUnsignedRational(ref TiffIfdEntry entry)
        {
            if (entry.Count != 1)
            {
                throw new ImageFormatException($"Cannot read a single value from an array of multiple items.");
            }

            return this.ReadUnsignedRationalArray(ref entry)[0];
        }

        /// <summary>
        /// Reads the data from a <see cref="TiffIfdEntry"/> as a <see cref="SignedRational"/> value.
        /// </summary>
        /// <param name="entry">The <see cref="TiffIfdEntry"/> to read.</param>
        /// <returns>The data.</returns>
        /// <exception cref="ImageFormatException">
        /// Thrown if the data-type specified by the file cannot be converted to a <see cref="SignedRational"/>, or if
        /// there is an array of items.
        /// </exception>
        public SignedRational ReadSignedRational(ref TiffIfdEntry entry)
        {
            if (entry.Count != 1)
            {
                throw new ImageFormatException($"Cannot read a single value from an array of multiple items.");
            }

            return this.ReadSignedRationalArray(ref entry)[0];
        }

        /// <summary>
        /// Reads the data from a <see cref="TiffIfdEntry"/> as an array of <see cref="Rational"/> values.
        /// </summary>
        /// <param name="entry">The <see cref="TiffIfdEntry"/> to read.</param>
        /// <returns>The data.</returns>
        /// <exception cref="ImageFormatException">
        /// Thrown if the data-type specified by the file cannot be converted to a <see cref="Rational"/>.
        /// </exception>
        public Rational[] ReadUnsignedRationalArray(ref TiffIfdEntry entry)
        {
            if (entry.Type != TiffType.Rational)
            {
                throw new ImageFormatException($"A value of type '{entry.Type}' cannot be converted to a Rational.");
            }

            byte[] bytes = this.ReadBytes(ref entry);
            Rational[] result = new Rational[entry.Count];

            for (int i = 0; i < result.Length; i++)
            {
                uint numerator = this.ToUInt32(bytes, i * TiffConstants.SizeOfRational);
                uint denominator = this.ToUInt32(bytes, (i * TiffConstants.SizeOfRational) + TiffConstants.SizeOfLong);
                result[i] = new Rational(numerator, denominator);
            }

            return result;
        }

        /// <summary>
        /// Reads the data from a <see cref="TiffIfdEntry"/> as an array of <see cref="SignedRational"/> values.
        /// </summary>
        /// <param name="entry">The <see cref="TiffIfdEntry"/> to read.</param>
        /// <returns>The data.</returns>
        /// <exception cref="ImageFormatException">
        /// Thrown if the data-type specified by the file cannot be converted to a <see cref="SignedRational"/>.
        /// </exception>
        public SignedRational[] ReadSignedRationalArray(ref TiffIfdEntry entry)
        {
            if (entry.Type != TiffType.SRational)
            {
                throw new ImageFormatException($"A value of type '{entry.Type}' cannot be converted to a SignedRational.");
            }

            byte[] bytes = this.ReadBytes(ref entry);
            SignedRational[] result = new SignedRational[entry.Count];

            for (int i = 0; i < result.Length; i++)
            {
                int numerator = this.ToInt32(bytes, i * TiffConstants.SizeOfRational);
                int denominator = this.ToInt32(bytes, (i * TiffConstants.SizeOfRational) + TiffConstants.SizeOfLong);
                result[i] = new SignedRational(numerator, denominator);
            }

            return result;
        }

        /// <summary>
        /// Reads the data from a <see cref="TiffIfdEntry"/> as a <see cref="float"/> value.
        /// </summary>
        /// <param name="entry">The <see cref="TiffIfdEntry"/> to read.</param>
        /// <returns>The data.</returns>
        /// <exception cref="ImageFormatException">
        /// Thrown if the data-type specified by the file cannot be converted to a <see cref="float"/>, or if
        /// there is an array of items.
        /// </exception>
        public float ReadFloat(ref TiffIfdEntry entry)
        {
            if (entry.Count != 1)
            {
                throw new ImageFormatException($"Cannot read a single value from an array of multiple items.");
            }

            if (entry.Type != TiffType.Float)
            {
                throw new ImageFormatException($"A value of type '{entry.Type}' cannot be converted to a float.");
            }

            return this.ToSingle(entry.Value, 0);
        }

        /// <summary>
        /// Reads the data from a <see cref="TiffIfdEntry"/> as a <see cref="double"/> value.
        /// </summary>
        /// <param name="entry">The <see cref="TiffIfdEntry"/> to read.</param>
        /// <returns>The data.</returns>
        /// <exception cref="ImageFormatException">
        /// Thrown if the data-type specified by the file cannot be converted to a <see cref="double"/>, or if
        /// there is an array of items.
        /// </exception>
        public double ReadDouble(ref TiffIfdEntry entry)
        {
            if (entry.Count != 1)
            {
                throw new ImageFormatException($"Cannot read a single value from an array of multiple items.");
            }

            return this.ReadDoubleArray(ref entry)[0];
        }

        /// <summary>
        /// Reads the data from a <see cref="TiffIfdEntry"/> as an array of <see cref="float"/> values.
        /// </summary>
        /// <param name="entry">The <see cref="TiffIfdEntry"/> to read.</param>
        /// <returns>The data.</returns>
        /// <exception cref="ImageFormatException">
        /// Thrown if the data-type specified by the file cannot be converted to a <see cref="float"/>.
        /// </exception>
        public float[] ReadFloatArray(ref TiffIfdEntry entry)
        {
            if (entry.Type != TiffType.Float)
            {
                throw new ImageFormatException($"A value of type '{entry.Type}' cannot be converted to a float.");
            }

            byte[] bytes = this.ReadBytes(ref entry);
            float[] result = new float[entry.Count];

            for (int i = 0; i < result.Length; i++)
            {
                result[i] = this.ToSingle(bytes, i * TiffConstants.SizeOfFloat);
            }

            return result;
        }

        /// <summary>
        /// Reads the data from a <see cref="TiffIfdEntry"/> as an array of <see cref="double"/> values.
        /// </summary>
        /// <param name="entry">The <see cref="TiffIfdEntry"/> to read.</param>
        /// <returns>The data.</returns>
        /// <exception cref="ImageFormatException">
        /// Thrown if the data-type specified by the file cannot be converted to a <see cref="double"/>.
        /// </exception>
        public double[] ReadDoubleArray(ref TiffIfdEntry entry)
        {
            if (entry.Type != TiffType.Double)
            {
                throw new ImageFormatException($"A value of type '{entry.Type}' cannot be converted to a double.");
            }

            byte[] bytes = this.ReadBytes(ref entry);
            double[] result = new double[entry.Count];

            for (int i = 0; i < result.Length; i++)
            {
                result[i] = this.ToDouble(bytes, i * TiffConstants.SizeOfDouble);
            }

            return result;
        }

        /// <summary>
        /// Calculates the size (in bytes) for the specified TIFF data-type.
        /// </summary>
        /// <param name="type">The data-type to calculate the size for.</param>
        /// <returns>The size of the data-type (in bytes).</returns>
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

        /// <summary>
        /// Reads a sequence of bytes from the input stream into a buffer.
        /// </summary>
        /// <param name="buffer">A buffer to store the retrieved data.</param>
        /// <param name="count">The number of bytes to read.</param>
        private void ReadBytes(byte[] buffer, int count)
        {
            int offset = 0;

            while (count > 0)
            {
                int bytesRead = this.InputStream.Read(buffer, offset, count);

                if (bytesRead == 0)
                {
                    break;
                }

                offset += bytesRead;
                count -= bytesRead;
            }
        }

        /// <summary>
        /// Converts buffer data into an <see cref="sbyte"/> using the correct endianness.
        /// </summary>
        /// <param name="bytes">The buffer.</param>
        /// <param name="offset">The byte offset within the buffer.</param>
        /// <returns>The converted value.</returns>
        private sbyte ToSByte(byte[] bytes, int offset)
        {
            return (sbyte)bytes[offset];
        }

        /// <summary>
        /// Converts buffer data into an <see cref="short"/> using the correct endianness.
        /// </summary>
        /// <param name="bytes">The buffer.</param>
        /// <param name="offset">The byte offset within the buffer.</param>
        /// <returns>The converted value.</returns>
        private short ToInt16(byte[] bytes, int offset)
        {
            if (this.IsLittleEndian)
            {
                return (short)(bytes[offset + 0] | (bytes[offset + 1] << 8));
            }
            else
            {
                return (short)((bytes[offset + 0] << 8) | bytes[offset + 1]);
            }
        }

        /// <summary>
        /// Converts buffer data into an <see cref="int"/> using the correct endianness.
        /// </summary>
        /// <param name="bytes">The buffer.</param>
        /// <param name="offset">The byte offset within the buffer.</param>
        /// <returns>The converted value.</returns>
        private int ToInt32(byte[] bytes, int offset)
        {
            if (this.IsLittleEndian)
            {
                return bytes[offset + 0] | (bytes[offset + 1] << 8) | (bytes[offset + 2] << 16) | (bytes[offset + 3] << 24);
            }
            else
            {
                return (bytes[offset + 0] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];
            }
        }

        /// <summary>
        /// Converts buffer data into a <see cref="byte"/> using the correct endianness.
        /// </summary>
        /// <param name="bytes">The buffer.</param>
        /// <param name="offset">The byte offset within the buffer.</param>
        /// <returns>The converted value.</returns>
        private byte ToByte(byte[] bytes, int offset)
        {
            return bytes[offset];
        }

        /// <summary>
        /// Converts buffer data into a <see cref="uint"/> using the correct endianness.
        /// </summary>
        /// <param name="bytes">The buffer.</param>
        /// <param name="offset">The byte offset within the buffer.</param>
        /// <returns>The converted value.</returns>
        private uint ToUInt32(byte[] bytes, int offset)
        {
            return (uint)this.ToInt32(bytes, offset);
        }

        /// <summary>
        /// Converts buffer data into a <see cref="ushort"/> using the correct endianness.
        /// </summary>
        /// <param name="bytes">The buffer.</param>
        /// <param name="offset">The byte offset within the buffer.</param>
        /// <returns>The converted value.</returns>
        private ushort ToUInt16(byte[] bytes, int offset)
        {
            return (ushort)this.ToInt16(bytes, offset);
        }

        /// <summary>
        /// Converts buffer data into a <see cref="float"/> using the correct endianness.
        /// </summary>
        /// <param name="bytes">The buffer.</param>
        /// <param name="offset">The byte offset within the buffer.</param>
        /// <returns>The converted value.</returns>
        private float ToSingle(byte[] bytes, int offset)
        {
            byte[] buffer = new byte[4];
            Array.Copy(bytes, offset, buffer, 0, 4);

            if (this.IsLittleEndian != BitConverter.IsLittleEndian)
            {
                Array.Reverse(buffer);
            }

            return BitConverter.ToSingle(buffer, 0);
        }

        /// <summary>
        /// Converts buffer data into a <see cref="double"/> using the correct endianness.
        /// </summary>
        /// <param name="bytes">The buffer.</param>
        /// <param name="offset">The byte offset within the buffer.</param>
        /// <returns>The converted value.</returns>
        private double ToDouble(byte[] bytes, int offset)
        {
            byte[] buffer = new byte[8];
            Array.Copy(bytes, offset, buffer, 0, 8);

            if (this.IsLittleEndian != BitConverter.IsLittleEndian)
            {
                Array.Reverse(buffer);
            }

            return BitConverter.ToDouble(buffer, 0);
        }
    }
}
