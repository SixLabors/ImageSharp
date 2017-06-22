// <copyright file="Adobe.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

// ReSharper disable InconsistentNaming
namespace ImageSharp.Formats.Jpeg.Port.Components
{
    /// <summary>
    /// Provides information about the Adobe marker segment
    /// </summary>
    internal struct Adobe
    {
        /// <summary>
        /// The DCT Encode Version
        /// </summary>
        public short DCTEncodeVersion;

        /// <summary>
        /// 0x0 : (none)
        /// Bit 15 : Encoded with Blend=1 downsampling
        /// </summary>
        public short APP14Flags0;

        /// <summary>
        /// 0x0 : (none)
        /// </summary>
        public short APP14Flags1;

        /// <summary>
        /// Determines the colorspace transform
        /// 00 : Unknown (RGB or CMYK)
        /// 01 : YCbCr
        /// 02 : YCCK
        /// </summary>
        public byte ColorTransform;
    }
}
