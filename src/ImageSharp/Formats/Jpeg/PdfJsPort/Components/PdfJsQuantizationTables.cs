// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jpeg.PdfJsPort.Components
{
    /// <summary>
    /// Contains the quantization tables.
    /// </summary>
    internal sealed class PdfJsQuantizationTables : IDisposable
    {
        /// <summary>
        /// Gets the ZigZag scan table
        /// </summary>
        public static byte[] DctZigZag
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
        }

        =
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
        public Buffer2D<short> Tables
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get; set;
        }

        = new Buffer2D<short>(64, 4);

        /// <inheritdoc/>
        public void Dispose()
        {
            if (this.Tables != null)
            {
                this.Tables.Dispose();
                this.Tables = null;
            }
        }
    }
}