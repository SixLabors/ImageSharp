// <copyright file="QuantizationTables.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats.Jpeg.Port.Components
{
    using ImageSharp.Memory;

    /// <summary>
    /// Contains the quantization tables.
    /// TODO: This all needs optimizing for memory. I'm just stubbing out functionality for now.
    /// </summary>
    internal class QuantizationTables
    {
        /// <summary>
        /// Gets the ZigZag scan table
        /// </summary>
        public static byte[] DctZigZag { get; } =
        {
            0,
            1, 8,
            16, 9, 2,
            3, 10, 17, 24,
            32, 25, 18, 11, 4,
            5, 12, 19, 26, 33, 40,
            48, 41, 34, 27, 20, 13, 6,
            7, 14, 21, 28, 35, 42, 49, 56,
            57, 50, 43, 36, 29, 22, 15,
            23, 30, 37, 44, 51, 58,
            59, 52, 45, 38, 31,
            39, 46, 53, 60,
            61, 54, 47,
            55, 62,
            63
        };

        /// <summary>
        /// Gets or sets the quantization tables.
        /// </summary>
        public Fast2DArray<short> Tables { get; set; } = new Fast2DArray<short>(64, 4);
    }
}