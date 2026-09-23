// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

internal static class JxlForwardCoefficientOrder
{
    public const byte OrderCount = 13;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CoefficientRows(int rows, int columns) => rows < columns ? rows : columns;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CoefficientColumns(int rows, int columns) => rows < columns ? columns : rows;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CoefficientLayout(ref int rows, ref int columns)
    {
        rows = CoefficientRows(rows, columns);
        columns = CoefficientColumns(rows, columns);
    }
}
