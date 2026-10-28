// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;

/// <summary>
/// Borrows a row of JPEG XL transform selections.
/// </summary>
/// <param name="row">The encoded selections for one logical block row.</param>
internal readonly ref struct JxlAcStrategyRow(ReadOnlySpan<byte> row)
{
    /// <summary>
    /// Gets the transform type and first-block flag at a column.
    /// </summary>
    /// <param name="x">The column in blocks of 8 by 8 pixels.</param>
    /// <returns>The selected transform and its position within a multi-block transform.</returns>
    public readonly JxlAcStrategy this[int x]
    {
        get
        {
            // One byte stores one block: bit zero marks the first block, and the remaining
            // bits store its transform type. Continuation blocks retain that type with bit zero clear.
            byte value = row[x];
            JxlAcStrategyType strategy = (JxlAcStrategyType)(value >> 1);
            bool isFirst = (value & 1) != 0;
            return new JxlAcStrategy(strategy, isFirst);
        }
    }
}
