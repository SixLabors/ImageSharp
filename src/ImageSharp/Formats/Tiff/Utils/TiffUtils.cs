// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;

namespace SixLabors.ImageSharp.Formats.Tiff.Utils
{
    /// <summary>
    /// Helper methods for TIFF decoding.
    /// </summary>
    internal static class TiffUtils
    {
        public static ushort ConvertToShort(ReadOnlySpan<byte> buffer, bool isBigEndian) => isBigEndian
            ? BinaryPrimitives.ReadUInt16BigEndian(buffer)
            : BinaryPrimitives.ReadUInt16LittleEndian(buffer);
    }
}
