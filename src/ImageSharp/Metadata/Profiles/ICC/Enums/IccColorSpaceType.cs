// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc
{
    /// <summary>
    /// Color Space Type
    /// </summary>
    public enum IccColorSpaceType : uint
    {
        /// <summary>
        /// CIE XYZ
        /// </summary>
        CieXyz = 0x58595A20,        // XYZ

        /// <summary>
        /// CIE Lab
        /// </summary>
        CieLab = 0x4C616220,        // Lab

        /// <summary>
        /// CIE Luv
        /// </summary>
        CieLuv = 0x4C757620,        // Luv

        /// <summary>
        /// YCbCr
        /// </summary>
        YCbCr = 0x59436272,         // YCbr

        /// <summary>
        /// CIE Yxy
        /// </summary>
        CieYxy = 0x59787920,        // Yxy

        /// <summary>
        /// RGB
        /// </summary>
        Rgb = 0x52474220,           // RGB

        /// <summary>
        /// Gray
        /// </summary>
        Gray = 0x47524159,          // GRAY

        /// <summary>
        /// HSV
        /// </summary>
        Hsv = 0x48535620,           // HSV

        /// <summary>
        /// HLS
        /// </summary>
        Hls = 0x484C5320,           // HLS

        /// <summary>
        /// CMYK
        /// </summary>
        Cmyk = 0x434D594B,          // CMYK

        /// <summary>
        /// CMY
        /// </summary>
        Cmy = 0x434D5920,           // CMY

        /// <summary>
        /// Generic 2 channel color
        /// </summary>
        Color2 = 0x32434C52,        // 2CLR

        /// <summary>
        /// Generic 3 channel color
        /// </summary>
        Color3 = 0x33434C52,        // 3CLR

        /// <summary>
        /// Generic 4 channel color
        /// </summary>
        Color4 = 0x34434C52,        // 4CLR

        /// <summary>
        /// Generic 5 channel color
        /// </summary>
        Color5 = 0x35434C52,        // 5CLR

        /// <summary>
        /// Generic 6 channel color
        /// </summary>
        Color6 = 0x36434C52,        // 6CLR

        /// <summary>
        /// Generic 7 channel color
        /// </summary>
        Color7 = 0x37434C52,        // 7CLR

        /// <summary>
        /// Generic 8 channel color
        /// </summary>
        Color8 = 0x38434C52,        // 8CLR

        /// <summary>
        /// Generic 9 channel color
        /// </summary>
        Color9 = 0x39434C52,        // 9CLR

        /// <summary>
        /// Generic 10 channel color
        /// </summary>
        Color10 = 0x41434C52,       // ACLR

        /// <summary>
        /// Generic 11 channel color
        /// </summary>
        Color11 = 0x42434C52,       // BCLR

        /// <summary>
        /// Generic 12 channel color
        /// </summary>
        Color12 = 0x43434C52,       // CCLR

        /// <summary>
        /// Generic 13 channel color
        /// </summary>
        Color13 = 0x44434C52,       // DCLR

        /// <summary>
        /// Generic 14 channel color
        /// </summary>
        Color14 = 0x45434C52,       // ECLR

        /// <summary>
        /// Generic 15 channel color
        /// </summary>
        Color15 = 0x46434C52,       // FCLR
    }
}
