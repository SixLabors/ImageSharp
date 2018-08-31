// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.MetaData.Profiles.Exif
{
    internal static class ExifConstants
    {
        public static readonly byte[] LittleEndianByteOrderMarker = {
            (byte)'I',
            (byte)'I',
            0x2A,
            0x00,
        };

        public static readonly byte[] BigEndianByteOrderMarker = {
            (byte)'M',
            (byte)'M',
            0x00,
            0x2A
        };
    }
}