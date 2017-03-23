// <copyright file="IccColorantEncoding.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
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
        ITU_R_BT_709_2 = 0x0001,

        /// <summary>
        /// SMPTE RP145 colorant encoding
        /// </summary>
        SMPTE_RP145 = 0x0002,

        /// <summary>
        /// EBU Tech.3213-E colorant encoding
        /// </summary>
        EBU_Tech_3213_E = 0x0003,

        /// <summary>
        /// P22 colorant encoding
        /// </summary>
        P22 = 0x0004,
    }
}
