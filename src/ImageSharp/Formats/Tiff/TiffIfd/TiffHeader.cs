// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Data structure for Tiff Header.
    /// </summary>
    internal struct TiffHeader
    {
        public TiffHeader(TiffByteOrder byteOrder, uint firstIfdOffset)
        {
            this.ByteOrder = byteOrder;
            this.FirstIfdOffset = firstIfdOffset;
        }

        public TiffByteOrder ByteOrder { get; }

        public uint FirstIfdOffset { get; }

        public static TiffHeader Read(TiffStream stream)
        {
            ushort magic = stream.ReadUInt16();
            if (magic != TiffConstants.HeaderMagicNumber)
            {
                throw new ImageFormatException("Invalid TIFF header magic number: " + magic);
            }

            uint firstIfdOffset = stream.ReadUInt32();
            if (firstIfdOffset == 0)
            {
                throw new ImageFormatException("Invalid TIFF file header.");
            }

            return new TiffHeader(stream.ByteOrder, firstIfdOffset);
        }
    }
}
