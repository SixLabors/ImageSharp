// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SixLabors.ImageSharp.Formats.Tiff;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    /// <summary>
    /// A utility data structure to decode Tiff IFD entries in unit tests.
    /// </summary>
    internal static class TiffIfdParser
    {
        public static int? GetInteger(this List<TiffIfdEntry> entries, TiffTagId tag)
        {
            TiffIfdEntry entry = entries.FirstOrDefault(e => e.TagId == tag);

            if (entry.Tag == 0)
            {
                return null;
            }

            Assert.Equal(1u, entry.Count);

            switch (entry.Type)
            {
                case TiffTagType.Byte:
                    return entry.RawValue[0];
                case TiffTagType.SByte:
                    return (sbyte)entry.RawValue[0];
                case TiffTagType.Short:
                    return BitConverter.ToUInt16(entry.RawValue, 0);
                case TiffTagType.SShort:
                    return BitConverter.ToInt16(entry.RawValue, 0);
                case TiffTagType.Long:
                    return (int)BitConverter.ToUInt32(entry.RawValue, 0);
                case TiffTagType.SLong:
                    return BitConverter.ToInt32(entry.RawValue, 0);
                default:
                    Assert.True(1 == 1, "TIFF IFD entry is not convertable to an integer.");
                    return null;
            }
        }

        public static Rational? GetUnsignedRational(this List<TiffIfdEntry> entries, TiffTagId tag)
        {
            TiffIfdEntry entry = entries.FirstOrDefault(e => e.TagId == tag);

            if (entry.Tag == 0)
            {
                return null;
            }

            Assert.Equal(TiffTagType.Rational, entry.Type);
            Assert.Equal(1u, entry.Count);

            uint numerator = BitConverter.ToUInt32(entry.RawValue, 0);
            uint denominator = BitConverter.ToUInt32(entry.RawValue, 4);

            return new Rational(numerator, denominator);
        }

        public static string GetAscii(this List<TiffIfdEntry> entries, ushort tag)
        {
            TiffIfdEntry entry = entries.FirstOrDefault(e => e.Tag == tag);

            if (entry.Tag == 0)
            {
                return null;
            }

            Assert.Equal(TiffTagType.Ascii, entry.Type);

            return Encoding.UTF8.GetString(entry.RawValue, 0, (int)entry.Count);
        }
    }
}
