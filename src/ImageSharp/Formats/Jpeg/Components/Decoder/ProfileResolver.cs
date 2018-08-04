// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Text;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
    /// <summary>
    /// Provides methods for identifying metadata and color profiles within jpeg images.
    /// </summary>
    internal static class ProfileResolver
    {
        /// <summary>
        /// Describes the EXIF specific markers
        /// </summary>
        public static readonly byte[] JFifMarker = Encoding.UTF8.GetBytes("JFIF\0");

        /// <summary>
        /// Describes the EXIF specific markers
        /// </summary>
        public static readonly byte[] IccMarker = Encoding.UTF8.GetBytes("ICC_PROFILE\0");

        /// <summary>
        /// Describes the ICC specific markers
        /// </summary>
        public static readonly byte[] ExifMarker = Encoding.UTF8.GetBytes("Exif\0\0");

        /// <summary>
        /// Describes Adobe specific markers <see href="http://www.sno.phy.queensu.ca/~phil/exiftool/TagNames/JPEG.html#Adobe"/>
        /// </summary>
        public static readonly byte[] AdobeMarker = Encoding.UTF8.GetBytes("Adobe");

        /// <summary>
        /// Returns a value indicating whether the passed bytes are a match to the profile identifier
        /// </summary>
        /// <param name="bytesToCheck">The bytes to check</param>
        /// <param name="profileIdentifier">The profile identifier</param>
        /// <returns>The <see cref="bool"/></returns>
        public static bool IsProfile(ReadOnlySpan<byte> bytesToCheck, ReadOnlySpan<byte> profileIdentifier)
        {
            return bytesToCheck.Length >= profileIdentifier.Length
                   && bytesToCheck.Slice(0, profileIdentifier.Length).SequenceEqual(profileIdentifier);
        }
    }
}