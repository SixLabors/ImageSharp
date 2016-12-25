// <copyright file="UnzigData.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>
namespace ImageSharp.Formats
{
    using System;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Holds the Jpeg UnZig array in a value/stack type.
    /// Unzig maps from the zigzag ordering to the natural ordering. For example,
    /// unzig[3] is the column and row of the fourth element in zigzag order. The
    /// value is 16, which means first column (16%8 == 0) and third row (16/8 == 2).
    /// </summary>
    internal unsafe struct UnzigData
    {
        /// <summary>
        /// Copy of <see cref="Unzig"/> in a value type
        /// </summary>
        public fixed int Data[64];

        /// <summary>
        /// Unzig maps from the zigzag ordering to the natural ordering. For example,
        /// unzig[3] is the column and row of the fourth element in zigzag order. The
        /// value is 16, which means first column (16%8 == 0) and third row (16/8 == 2).
        /// </summary>
        private static readonly int[] Unzig =
            {
                0, 1, 8, 16, 9, 2, 3, 10, 17, 24, 32, 25, 18, 11, 4, 5, 12, 19, 26, 33,
                40, 48, 41, 34, 27, 20, 13, 6, 7, 14, 21, 28, 35, 42, 49, 56, 57, 50,
                43, 36, 29, 22, 15, 23, 30, 37, 44, 51, 58, 59, 52, 45, 38, 31, 39, 46,
                53, 60, 61, 54, 47, 55, 62, 63,
            };

        /// <summary>
        /// Creates and fills an instance of <see cref="UnzigData"/> with Jpeg unzig indices
        /// </summary>
        /// <returns>The new instance</returns>
        public static UnzigData Create()
        {
            UnzigData result = default(UnzigData);
            int* unzigPtr = result.Data;
            Marshal.Copy(Unzig, 0, (IntPtr)unzigPtr, 64);
            return result;
        }
    }
}