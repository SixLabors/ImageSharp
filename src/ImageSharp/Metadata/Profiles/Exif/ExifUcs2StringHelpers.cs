// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Text;

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    internal static class ExifUcs2StringHelpers
    {
        public static Encoding Ucs2Encoding => Encoding.GetEncoding("UCS-2");

        public static bool IsUcs2Tag(ExifTagValue tag) => tag switch
        {
            ExifTagValue.XPAuthor or ExifTagValue.XPComment or ExifTagValue.XPKeywords or ExifTagValue.XPSubject or ExifTagValue.XPTitle => true,
            _ => false,
        };

        public static int Write(string value, Span<byte> destination) => ExifEncodedStringHelpers.Write(Ucs2Encoding, value, destination);
    }
}
