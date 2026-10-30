// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

/// <summary>
/// Defines the canonical coefficient orientation shared by transform storage and natural ordering.
/// </summary>
internal static class JxlForwardCoefficientOrder
{
    /// <summary>
    /// The number of coefficient-order families used by the transform strategies.
    /// </summary>
    public const byte OrderCount = 13;

    /// <summary>
    /// Returns the shorter axis for the canonical coefficient rows.
    /// </summary>
    /// <param name="rows">The original row extent, in the caller's coordinate units.</param>
    /// <param name="columns">The original column extent, in the same units.</param>
    /// <returns>The shorter extent.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CoefficientRows(int rows, int columns) => rows < columns ? rows : columns;

    /// <summary>
    /// Returns the longer axis for the canonical coefficient columns.
    /// </summary>
    /// <param name="rows">The original row extent, in the caller's coordinate units.</param>
    /// <param name="columns">The original column extent, in the same units.</param>
    /// <returns>The longer extent.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CoefficientColumns(int rows, int columns) => rows < columns ? columns : rows;

    /// <summary>
    /// Sorts both original extents into canonical coefficient orientation without losing the longer axis.
    /// </summary>
    /// <param name="rows">The original row extent, replaced with the shorter extent.</param>
    /// <param name="columns">The original column extent, replaced with the longer extent.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CoefficientLayout(ref int rows, ref int columns)
    {
        // Both outputs depend on the original dimensions. Sorting them in one assignment retains the longer axis.
        (rows, columns) = (CoefficientRows(rows, columns), CoefficientColumns(rows, columns));
    }
}
