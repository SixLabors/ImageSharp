// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Png
{
    /// <summary>
    /// Constants and helper methods for the Adam7 interlacing algorithm.
    /// </summary>
    internal static class Adam7
    {
        /// <summary>
        /// The amount to increment when processing each column per scanline for each interlaced pass.
        /// </summary>
        public static readonly int[] ColumnIncrement = { 8, 8, 4, 4, 2, 2, 1 };

        /// <summary>
        /// The index to start at when processing each column per scanline for each interlaced pass.
        /// </summary>
        public static readonly int[] FirstColumn = { 0, 4, 0, 2, 0, 1, 0 };

        /// <summary>
        /// The index to start at when processing each row per scanline for each interlaced pass.
        /// </summary>
        public static readonly int[] FirstRow = { 0, 0, 4, 0, 2, 0, 1 };

        /// <summary>
        /// The amount to increment when processing each row per scanline for each interlaced pass.
        /// </summary>
        public static readonly int[] RowIncrement = { 8, 8, 8, 4, 4, 2, 2 };

        /// <summary>
        /// Returns the correct number of columns for each interlaced pass.
        /// </summary>
        /// <param name="width">The line width.</param>
        /// <param name="passIndex">The current pass index.</param>
        /// <returns>The <see cref="int"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ComputeColumns(int width, int passIndex)
        {
            return passIndex switch
            {
                0 => (width + 7) / 8,
                1 => (width + 3) / 8,
                2 => (width + 3) / 4,
                3 => (width + 1) / 4,
                4 => (width + 1) / 2,
                5 => width / 2,
                6 => width,
                _ => throw new ArgumentException($"Not a valid pass index: {passIndex}")
            };
        }
    }
}