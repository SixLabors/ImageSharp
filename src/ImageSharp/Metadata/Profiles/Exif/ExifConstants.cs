// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    internal static class ExifConstants
    {
        public static ReadOnlySpan<byte> LittleEndianByteOrderMarker => new byte[]
        {
            (byte)'I',
            (byte)'I',
            0x2A,
            0x00,
        };

        public static ReadOnlySpan<byte> BigEndianByteOrderMarker => new byte[]
        {
            (byte)'M',
            (byte)'M',
            0x00,
            0x2A
        };
    }
}
