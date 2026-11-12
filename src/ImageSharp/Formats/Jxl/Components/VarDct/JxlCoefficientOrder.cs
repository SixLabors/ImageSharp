// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.VarDct;

/// <summary>
/// Defines transform dimensions, shared order families and natural coefficient traversal for JPEG XL VarDCT.
/// </summary>
internal static class JxlCoefficientOrder
{
    /// <summary>
    /// The thirteen shared coefficient-order families from libjxl coeff_order_fwd.h kNumOrders.
    /// </summary>
    public const int OrderCount = 13;

    /// <summary>
    /// The 27 raw transform strategies in libjxl ac_strategy.h AcStrategy::Type, before coefficient-order family sharing.
    /// </summary>
    public const int StrategyCount = 27;

    /// <summary>
    /// The number of component-order offsets: thirteen transform families, each with X, Y and B components.
    /// </summary>
    public const int CoefficientOrderCount = 3 * OrderCount;

    /// <summary>
    /// The transform block width and height in coding pixels.
    /// </summary>
    private const int BlockSize = 8;

    /// <summary>
    /// The number of coefficient positions in one transform block.
    /// </summary>
    private const int CoefficientBlockArea = BlockSize * BlockSize;

    // Poll cancellation before the first diagonal position, then after each 1024 positions visited.
    // The counter continues across both diagonal loops and includes positions discarded for rectangular transforms.
    // This avoids reading the token at every position; it does not define an elapsed-time response limit.
    // Keep this value a power of two: the loops select check positions with visited & (interval - 1).
    // This value controls cancellation responsiveness. JPEG XL does not prescribe it, and no benchmark determined it.
    private const int OrderCancellationInterval = 1024;

    /// <summary>
    /// Gets the coefficient-order family for each validated strategy. Transposed strategies share the same order.
    /// </summary>
    public static ReadOnlySpan<byte> StrategyOrderFamilies =>
    [
        0, 1, 1, 1, 2, 3, 4, 4, 5, 5, 6, 6, 1, 1, 1, 1, 1, 1, 7, 8, 8, 9, 10, 10, 11, 12, 12
    ];

    /// <summary>
    /// Gets one representative strategy per order family. Its dimensions come from the shared strategy tables.
    /// </summary>
    public static ReadOnlySpan<byte> OrderFamilyStrategies => [0, 1, 4, 5, 6, 8, 10, 18, 19, 21, 22, 24, 25];

    /// <summary>
    /// Gets the number of horizontal DC blocks covered by each validated raw strategy code.
    /// </summary>
    public static ReadOnlySpan<byte> StrategyWidths =>
    [
        1, 1, 1, 1, 2, 4, 1, 2, 1, 4, 2, 4, 1, 1, 1, 1, 1, 1, 8, 4, 8, 16, 8, 16, 32, 16, 32
    ];

    /// <summary>
    /// Gets the number of vertical DC blocks covered by each validated raw strategy code.
    /// </summary>
    public static ReadOnlySpan<byte> StrategyHeights =>
    [
        1, 1, 1, 1, 2, 4, 2, 1, 4, 1, 4, 2, 1, 1, 1, 1, 1, 1, 8, 8, 4, 16, 16, 8, 32, 32, 16
    ];

    /// <summary>
    /// Selects the component entry in a pass's coefficient-order offset table.
    /// </summary>
    /// <param name="strategy">The validated raw transform strategy, from zero through twenty-six.</param>
    /// <param name="component">The component index in X, Y, B order.</param>
    /// <returns>The offset-table index, from zero through thirty-eight.</returns>
    public static int GetCoefficientOrderIndex(byte strategy, int component) => (3 * StrategyOrderFamilies[strategy]) + component;

    /// <summary>
    /// Gets the number of coefficient positions in one strategy's order.
    /// </summary>
    /// <param name="strategy">The validated raw transform strategy.</param>
    /// <returns>The complete coefficient count, including the lowest-frequency prefix.</returns>
    public static int GetCoefficientOrderSize(byte strategy) => CoefficientBlockArea * StrategyWidths[strategy] * StrategyHeights[strategy];

    /// <summary>
    /// Writes the format's natural coefficient order for one validated transform strategy.
    /// </summary>
    /// <param name="strategy">The validated raw strategy. Transposed shapes share the same normalized order.</param>
    /// <param name="destination">Space for every coefficient position in this strategy.</param>
    /// <param name="cancellationToken">The token checked while visiting large diagonal sets.</param>
    public static void WriteNaturalCoefficientOrder(byte strategy, Span<uint> destination, CancellationToken cancellationToken)
    {
        int width = StrategyWidths[strategy];
        int height = StrategyHeights[strategy];
        int columns = Math.Max(width, height);
        int rows = Math.Min(width, height);
        int ratio = columns / rows;
        int rowMask = ratio - 1;
        int rowShift = BitOperations.Log2((uint)ratio);
        int dimension = columns * BlockSize;
        int next = columns * rows;
        int visited = 0;

        // Visit alternating diagonals on the enclosing square. Keep only rows at the rectangle's sampling interval,
        // then divide their indices by that interval. This normalizes transposed strategies without sorting coefficients.
        // The lowest-frequency rectangle is placed first in raster order; AC frequencies follow the diagonal sequence.
        for (int diagonal = 0; diagonal < dimension; diagonal++)
        {
            for (int step = 0; step <= diagonal; step++)
            {
                if ((visited++ & (OrderCancellationInterval - 1)) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                int x = (diagonal & 1) != 0 ? diagonal - step : step;
                int y = (diagonal & 1) != 0 ? step : diagonal - step;
                if ((y & rowMask) == 0)
                {
                    y >>= rowShift;
                    int index = x < columns && y < rows ? (y * columns) + x : next++;
                    destination[index] = (uint)((y * dimension) + x);
                }
            }
        }

        for (int remaining = dimension - 1; remaining > 0; remaining--)
        {
            int diagonal = remaining - 1;
            for (int step = 0; step <= diagonal; step++)
            {
                if ((visited++ & (OrderCancellationInterval - 1)) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                int x = dimension - 1 - (diagonal - step);
                int y = dimension - 1 - step;
                if ((diagonal & 1) != 0)
                {
                    (x, y) = (y, x);
                }

                if ((y & rowMask) == 0)
                {
                    y >>= rowShift;
                    destination[next++] = (uint)((y * dimension) + x);
                }
            }
        }
    }
}
