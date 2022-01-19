// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Text;

namespace SixLabors.ImageSharp.Metadata.Profiles.Exif
{
    internal static class ExifUcs2StringHelpers
    {
        public static Encoding Ucs2Encoding => Encoding.GetEncoding("UCS-2");

        public static bool IsUcs2Tag(ExifTagValue tag)
        {
            switch (tag)
            {
                case ExifTagValue.XPAuthor:
                case ExifTagValue.XPComment:
                case ExifTagValue.XPKeywords:
                case ExifTagValue.XPSubject:
                case ExifTagValue.XPTitle:
                    return true;
                default:
                    return false;
            }
        }

        public static string ConvertToString(ReadOnlySpan<byte> buffer) => Ucs2Encoding.GetString(buffer);
    }
}
