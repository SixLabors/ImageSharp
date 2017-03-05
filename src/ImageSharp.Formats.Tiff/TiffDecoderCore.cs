// <copyright file="TiffDecoderCore.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;
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
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
        }

        public uint ReadHeader()
        {
            byte[] headerBytes = new byte[8];
            ReadBytes(headerBytes, 8);

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

        private byte[] ReadBytes(byte[] buffer, int count)
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

            return buffer;
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

        private UInt32 ToUInt32(byte[] bytes, int offset)
        {
            return (uint)ToInt32(bytes, offset);
        }

        private UInt16 ToUInt16(byte[] bytes, int offset)
        {
            return (ushort)ToInt16(bytes, offset);
        }
    }
}
