// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// The tiff data stream factory class.
    /// </summary>
    internal static class TiffStreamFactory
    {
        public static TiffStream CreateBySignature(Stream stream)
        {
            TiffByteOrder order = ReadByteOrder(stream);
            return Create(order, stream);
        }

        /// <summary>
        /// Creates the specified byte order.
        /// </summary>
        /// <param name="byteOrder">The byte order.</param>
        /// <param name="stream">The stream.</param>
        public static TiffStream Create(TiffByteOrder byteOrder, Stream stream)
        {
            if (byteOrder == TiffByteOrder.BigEndian)
            {
                return new TiffBigEndianStream(stream);
            }
            else if (byteOrder == TiffByteOrder.LittleEndian)
            {
                return new TiffLittleEndianStream(stream);
            }

            throw new ArgumentOutOfRangeException(nameof(byteOrder));
        }

        /// <summary>
        /// Reads the byte order of stream.
        /// </summary>
        /// <param name="stream">The stream.</param>
        private static TiffByteOrder ReadByteOrder(Stream stream)
        {
            byte[] headerBytes = new byte[2];
            stream.Read(headerBytes, 0, 2);
            if (headerBytes[0] == TiffConstants.ByteOrderLittleEndian && headerBytes[1] == TiffConstants.ByteOrderLittleEndian)
            {
                return TiffByteOrder.LittleEndian;
            }
            else if (headerBytes[0] == TiffConstants.ByteOrderBigEndian && headerBytes[1] == TiffConstants.ByteOrderBigEndian)
            {
                return TiffByteOrder.BigEndian;
            }

            throw new ImageFormatException("Invalid TIFF file header.");
        }
    }
}
