// <copyright file="JFif.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats.Jpeg.Port.Components
{
    /// <summary>
    /// Provides information about the JFIF marker segment
    /// </summary>
    internal struct JFif
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

        // TODO: Thumbnail?
    }
}