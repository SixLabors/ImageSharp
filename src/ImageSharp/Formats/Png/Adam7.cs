// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Png;

/// <summary>
/// Constants and helper methods for the Adam7 interlacing algorithm.
/// </summary>
internal static class Adam7
{
    /// <summary>
    /// The amount to increment when processing each column per scanline for each interlaced pass.
    /// </summary>
    public static readonly int[] ColumnIncrement = [8, 8, 4, 4, 2, 2, 1];

    /// <summary>
    /// The index to start at when processing each column per scanline for each interlaced pass.
    /// </summary>
    public static readonly int[] FirstColumn = [0, 4, 0, 2, 0, 1, 0];

    /// <summary>
    /// The index to start at when processing each row per scanline for each interlaced pass.
    /// </summary>
    public static readonly int[] FirstRow = [0, 0, 4, 0, 2, 0, 1];

    /// <summary>
    /// The amount to increment when processing each row per scanline for each interlaced pass.
    /// </summary>
    public static readonly int[] RowIncrement = [8, 8, 8, 4, 4, 2, 2];

    /// <summary>
    /// Gets the number of columns of an interlaced pass.
    /// </summary>
    /// <remarks>
    /// The pass takes each column from <see cref="FirstColumn"/> with the step <see cref="ColumnIncrement"/>, so the count is the ceiling of (width - first
    /// column) / step. A pass can have no columns when the width is less than 5. The calculation uses unsigned values, so a width up to 2^31-1 cannot wrap. See
    /// PNG Third Edition, section 8.1 "Interlace methods": https://www.w3.org/TR/png-3/#8InterlaceMethods.
    /// </remarks>
    /// <param name="width">The width of the image or frame.</param>
    /// <param name="pass">The pass, 0 to 6.</param>
    /// <returns>The number of columns.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ComputeBlockWidth(int width, int pass)
        => (int)(((uint)width + (uint)(ColumnIncrement[pass] - 1 - FirstColumn[pass])) / (uint)ColumnIncrement[pass]);
}
