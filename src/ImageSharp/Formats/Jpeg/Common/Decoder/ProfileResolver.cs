// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Formats.Jpeg.Common.Decoder
{
    /// <summary>
    /// Provides methods for identifying metadata and color profiles within jpeg images.
    /// </summary>
    internal static class ProfileResolver
    {
        /// <summary>
        /// Describes the EXIF specific markers
        /// </summary>
        public static readonly byte[] JFifMarker = ToAsciiBytes("JFIF\0");

        /// <summary>
        /// Describes the EXIF specific markers
        /// </summary>
        public static readonly byte[] IccMarker = ToAsciiBytes("ICC_PROFILE\0");

        /// <summary>
        /// Describes the ICC specific markers
        /// </summary>
        public static readonly byte[] ExifMarker = ToAsciiBytes("Exif\0\0");

        /// <summary>
        /// Describes Adobe specific markers <see href="http://www.sno.phy.queensu.ca/~phil/exiftool/TagNames/JPEG.html#Adobe"/>
        /// </summary>
        public static readonly byte[] AdobeMarker = ToAsciiBytes("Adobe");

        /// <summary>
        /// Returns a value indicating whether the passed bytes are a match to the profile identifer
        /// </summary>
        /// <param name="bytesToCheck">The bytes to check</param>
        /// <param name="profileIdentifier">The profile identifier</param>
        /// <returns>The <see cref="bool"/></returns>
        public static bool IsProfile(Span<byte> bytesToCheck, Span<byte> profileIdentifier)
        {
            return bytesToCheck.Length >= profileIdentifier.Length
                   && bytesToCheck.Slice(0, profileIdentifier.Length).SequenceEqual(profileIdentifier);
        }

        // No Encoding.ASCII nor Linq.Select on NetStandard 1.1
        private static byte[] ToAsciiBytes(string str)
        {
            int length = str.Length;
            byte[] bytes = new byte[length];
            char[] chars = str.ToCharArray();
            for (int i = 0; i < length; i++)
            {
                bytes[i] = (byte)chars[i];
            }

            return bytes;
        }
    }
}