// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc
{
    /// <summary>
    /// Colorant Encoding
    /// </summary>
    internal enum IccColorantEncoding : ushort
    {
        /// <summary>
        /// Unknown colorant encoding
        /// </summary>
        Unknown = 0x0000,

        /// <summary>
        /// ITU-R BT.709-2 colorant encoding
        /// </summary>
        ItuRBt709_2 = 0x0001,

        /// <summary>
        /// SMPTE RP145 colorant encoding
        /// </summary>
        SmpteRp145 = 0x0002,

        /// <summary>
        /// EBU Tech.3213-E colorant encoding
        /// </summary>
        EbuTech3213E = 0x0003,

        /// <summary>
        /// P22 colorant encoding
        /// </summary>
        P22 = 0x0004,
    }
}
