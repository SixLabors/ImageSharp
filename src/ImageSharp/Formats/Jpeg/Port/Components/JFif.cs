// <copyright file="JFif.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats.Jpeg.Port.Components
{
    using System;

    /// <summary>
    /// Provides information about the JFIF marker segment
    /// TODO: Thumbnail?
    /// </summary>
    internal struct JFif : IEquatable<JFif>
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
        public bool Equals(JFif other)
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

            return obj is JFif && this.Equals((JFif)obj);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = this.MajorVersion.GetHashCode();
                hashCode = (hashCode * 397) ^ this.MinorVersion.GetHashCode();
                hashCode = (hashCode * 397) ^ this.DensityUnits.GetHashCode();
                hashCode = (hashCode * 397) ^ this.XDensity.GetHashCode();
                hashCode = (hashCode * 397) ^ this.YDensity.GetHashCode();
                return hashCode;
            }
        }
    }
}