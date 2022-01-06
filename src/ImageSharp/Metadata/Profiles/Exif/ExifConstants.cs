// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Text;

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    internal static class ExifConstants
    {
        private const ulong AsciiCode = 0x_41_53_43_49_49_00_00_00;
        private const ulong JISCode = 0x_4A_49_53_00_00_00_00_00;
        private const ulong UnicodeCode = 0x_55_4E_49_43_4F_44_45_00;
        private const ulong UndefinedCode = 0x_00_00_00_00_00_00_00_00;

        private static readonly byte[] AsciiCodeBytes = { 0x41, 0x53, 0x43, 0x49, 0x49, 0, 0, 0 };
        private static readonly byte[] JISCodeBytes = { 0x4A, 0x49, 0x53, 0, 0, 0, 0, 0 };
        private static readonly byte[] UnicodeCodeBytes = { 0x55, 0x4E, 0x49, 0x43, 0x4F, 0x44, 0x45, 0 };
        private static readonly byte[] UndefinedCodeBytes = { 0, 0, 0, 0, 0, 0, 0, 0 };

        public const int CharacterCodeBytesLength = 8;

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

        public static Encoding DefaultAsciiEncoding => Encoding.UTF8;

        public static Encoding JIS0208Encoding => Encoding.GetEncoding(932);

        public static bool TryDetect(ReadOnlySpan<byte> buffer, out EncodedStringCode code)
        {
            if (buffer.Length >= CharacterCodeBytesLength)
            {
                ulong test = BinaryPrimitives.ReadUInt64LittleEndian(buffer);
                switch (test)
                {
                    case AsciiCode:
                        code = EncodedStringCode.ASCII;
                        return true;
                    case JISCode:
                        code = EncodedStringCode.JIS;
                        return true;
                    case UnicodeCode:
                        code = EncodedStringCode.Unicode;
                        return true;
                    case UndefinedCode:
                        code = EncodedStringCode.Undefined;
                        return true;
                    default:
                        break;
                }
            }

            code = default;
            return false;
        }

        public static ReadOnlySpan<byte> GetCodeBytes(EncodedStringCode code) => code switch
        {
            EncodedStringCode.ASCII => AsciiCodeBytes,
            EncodedStringCode.JIS => JISCodeBytes,
            EncodedStringCode.Unicode => UnicodeCodeBytes,
            EncodedStringCode.Undefined => UndefinedCodeBytes,
            _ => UndefinedCodeBytes
        };

        public static Encoding GetEncoding(EncodedStringCode code) => code switch
        {
            EncodedStringCode.ASCII => Encoding.ASCII,
            EncodedStringCode.JIS => JIS0208Encoding,
            EncodedStringCode.Unicode => Encoding.Unicode,
            EncodedStringCode.Undefined => Encoding.UTF8,
            _ => Encoding.UTF8
        };
    }
}
