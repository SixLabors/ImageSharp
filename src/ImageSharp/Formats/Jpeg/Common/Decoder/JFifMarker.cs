// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Formats.Jpeg.Common.Decoder
{
    /// <summary>
    /// Provides information about the JFIF marker segment
    /// TODO: Thumbnail?
    /// </summary>
    internal struct JFifMarker : IEquatable<JFifMarker>
    {
        /// <summary>
        /// The major version
        /// </summary>
        public byte MajorVersion;

        /// <summary>
        /// The minor version
        /// </summary>
        public byte MinorVersion;

        /// <summary>
        /// Units for the following pixel density fields
        ///  00 : No units; width:height pixel aspect ratio = Ydensity:Xdensity
        ///  01 : Pixels per inch (2.54 cm)
        ///  02 : Pixels per centimeter
        /// </summary>
        public byte DensityUnits;

        /// <summary>
        /// Horizontal pixel density. Must not be zero.
        /// </summary>
        public short XDensity;

        /// <summary>
        /// Vertical pixel density. Must not be zero.
        /// </summary>
        public short YDensity;

        /// <inheritdoc/>
        public bool Equals(JFifMarker other)
        {
            return this.MajorVersion == other.MajorVersion
                && this.MinorVersion == other.MinorVersion
                && this.DensityUnits == other.DensityUnits
                && this.XDensity == other.XDensity
                && this.YDensity == other.YDensity;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            return obj is JFifMarker && this.Equals((JFifMarker)obj);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return GetHashCode(this);
        }

        private static int GetHashCode(JFifMarker marker)
        {
            return HashHelpers.Combine(
                marker.MajorVersion.GetHashCode(),
                HashHelpers.Combine(
                    marker.MinorVersion.GetHashCode(),
                    HashHelpers.Combine(
                        marker.DensityUnits.GetHashCode(),
                        HashHelpers.Combine(marker.XDensity, marker.YDensity))));
        }
    }
}