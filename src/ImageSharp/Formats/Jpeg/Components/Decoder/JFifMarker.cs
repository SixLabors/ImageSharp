// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.MetaData;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
    /// <summary>
    /// Provides information about the JFIF marker segment
    /// TODO: Thumbnail?
    /// </summary>
    internal readonly struct JFifMarker : IEquatable<JFifMarker>
    {
        /// <summary>
        /// Gets the length of an JFIF marker segment.
        /// </summary>
        public const int Length = 13;

        /// <summary>
        /// Initializes a new instance of the <see cref="JFifMarker"/> struct.
        /// </summary>
        /// <param name="majorVersion">The major version</param>
        /// <param name="minorVersion">The minor version</param>
        /// <param name="densityUnits">The units for the density values</param>
        /// <param name="xDensity">The horizontal pixel density</param>
        /// <param name="yDensity">The veritcal pixel density</param>
        private JFifMarker(byte majorVersion, byte minorVersion, byte densityUnits, short xDensity, short yDensity)
        {
            Guard.MustBeGreaterThan(xDensity, 0, nameof(xDensity));
            Guard.MustBeGreaterThan(yDensity, 0, nameof(yDensity));
            Guard.MustBeBetweenOrEqualTo(densityUnits, 0, 2, nameof(densityUnits));

            this.MajorVersion = majorVersion;
            this.MinorVersion = minorVersion;
            this.DensityUnits = (PixelResolutionUnit)densityUnits;
            this.XDensity = xDensity;
            this.YDensity = yDensity;
        }

        /// <summary>
        /// Gets the major version
        /// </summary>
        public byte MajorVersion { get; }

        /// <summary>
        /// Gets the minor version
        /// </summary>
        public byte MinorVersion { get; }

        /// <summary>
        /// Gets the units for the following pixel density fields
        ///  00 : No units; width:height pixel aspect ratio = Ydensity:Xdensity
        ///  01 : Pixels per inch (2.54 cm)
        ///  02 : Pixels per centimeter
        /// </summary>
        public PixelResolutionUnit DensityUnits { get; }

        /// <summary>
        /// Gets the horizontal pixel density. Must not be zero.
        /// </summary>
        public short XDensity { get; }

        /// <summary>
        /// Gets the vertical pixel density. Must not be zero.
        /// </summary>
        public short YDensity { get; }

        /// <summary>
        /// Converts the specified byte array representation of an JFIF marker to its <see cref="JFifMarker"/> equivalent and
        /// returns a value that indicates whether the conversion succeeded.
        /// </summary>
        /// <param name="bytes">The byte array containing metadata to parse</param>
        /// <param name="marker">The marker to return.</param>
        public static bool TryParse(byte[] bytes, out JFifMarker marker)
        {
            if (ProfileResolver.IsProfile(bytes, ProfileResolver.JFifMarker))
            {
                byte majorVersion = bytes[5];
                byte minorVersion = bytes[6];
                byte densityUnits = bytes[7];
                short xDensity = (short)((bytes[8] << 8) | bytes[9]);
                short yDensity = (short)((bytes[10] << 8) | bytes[11]);

                if (xDensity > 0 && yDensity > 0)
                {
                    marker = new JFifMarker(majorVersion, minorVersion, densityUnits, xDensity, yDensity);
                    return true;
                }
            }

            marker = default;
            return false;
        }

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
            return obj is JFifMarker other && this.Equals(other);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return HashHelpers.Combine(
                this.MajorVersion.GetHashCode(),
                HashHelpers.Combine(
                    this.MinorVersion.GetHashCode(),
                    HashHelpers.Combine(
                        this.DensityUnits.GetHashCode(),
                        HashHelpers.Combine(this.XDensity, this.YDensity))));
        }
    }
}