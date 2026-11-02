// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Components.Entropy;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.VarDct;

internal sealed partial class JxlVarDctDecoder
{
    /// <summary>
    /// The number of component-order offsets: thirteen transform families, each with X, Y and B components.
    /// </summary>
    public const int CoefficientOrderCount = 3 * OrderCount;

    private const int CoefficientBlockArea = BlockSize * BlockSize;

    // Poll cancellation before the first diagonal position, then after each 1024 positions visited.
    // The counter continues across both diagonal loops and includes positions discarded for rectangular transforms.
    // This avoids reading the token at every position; it does not define an elapsed-time response limit.
    // Keep this value a power of two: the loops select check positions with visited & (interval - 1).
    // This value controls cancellation responsiveness. JPEG XL does not prescribe it, and no benchmark determined it.
    private const int OrderCancellationInterval = 1024;

    private IMemoryOwner<uint>? orderWorkspace;

    /// <summary>
    /// Gets the coefficient-order family for each validated strategy. Transposed strategies share the same order.
    /// </summary>
    private static ReadOnlySpan<byte> StrategyOrderFamilies =>
    [
        0, 1, 1, 1, 2, 3, 4, 4, 5, 5, 6, 6, 1, 1, 1, 1, 1, 1, 7, 8, 8, 9, 10, 10, 11, 12, 12
    ];

    /// <summary>
    /// Gets one representative strategy per order family. Its dimensions come from the shared strategy tables.
    /// </summary>
    private static ReadOnlySpan<byte> OrderFamilyStrategies => [0, 1, 4, 5, 6, 8, 10, 18, 19, 21, 22, 24, 25];

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
    /// Reads one pass's coefficient orders while retaining only families used by its frame's transform strategies.
    /// </summary>
    /// <param name="reader">The bounded AC-global reader at the pass's used-order mask.</param>
    /// <param name="allocator">The frame's tracked allocator for retained orders and reusable permutation scratch.</param>
    /// <param name="usedStrategies">The nonzero mask established by validated AC metadata.</param>
    /// <param name="offsets">Space for thirty-nine component offsets. Unused entries receive minus one.</param>
    /// <param name="cancellationToken">The token checked during order generation and entropy traversal.</param>
    /// <returns>The completed order owner transferred to the pass. Default component orders can share an offset.</returns>
    /// <remarks>Read all pass orders during global-AC setup, before coefficient storage is prepared.</remarks>
    public IMemoryOwner<uint> ReadCoefficientOrders(
        ref JxlBitReader reader,
        MemoryAllocator allocator,
        uint usedStrategies,
        Span<int> offsets,
        CancellationToken cancellationToken)
    {
        uint encodedFamilies = reader.ReadU32((0, 0x5F), (0, 0x13), (0, 0), (13, 0));
        uint usedFamilies = 0;
        ReadOnlySpan<byte> families = StrategyOrderFamilies;
        for (int strategy = 0; strategy < StrategyCount; strategy++)
        {
            if ((usedStrategies & (1U << strategy)) != 0)
            {
                usedFamilies |= 1U << families[strategy];
            }
        }

        offsets.Fill(-1);
        ReadOnlySpan<byte> representatives = OrderFamilyStrategies;
        int retainedCount = 0;
        int maximumCustomSize = 0;
        uint symbolBudget = 0;
        for (int family = 0; family < OrderCount; family++)
        {
            int size = GetCoefficientOrderSize(representatives[family]);
            bool encoded = (encodedFamilies & (1U << family)) != 0;
            if (encoded)
            {
                // Each component encodes one length and at most size - lowestFrequencyCount digits. The history
                // spans all selected families, including those unused by this frame, without resetting between permutations.
                symbolBudget += (uint)(3 * (1 + size - (size / CoefficientBlockArea)));
            }

            if ((usedFamilies & (1U << family)) == 0)
            {
                continue;
            }

            for (int component = 0; component < 3; component++)
            {
                offsets[(3 * family) + component] = retainedCount + (encoded ? component * size : 0);
            }

            retainedCount += encoded ? 3 * size : size;
            if (encoded)
            {
                maximumCustomSize = Math.Max(maximumCustomSize, size);
            }
        }

        // A validated image has at least one used strategy. Retain only its used families; ordinary natural orders
        // share one copy across components instead of allocating the native full-size table for every pass.
        IMemoryOwner<uint> result = allocator.Allocate<uint>(retainedCount);
        try
        {
            Span<uint> orders = result.Memory.Span;
            for (int family = 0; family < OrderCount; family++)
            {
                if ((usedFamilies & (1U << family)) != 0 && (encodedFamilies & (1U << family)) == 0)
                {
                    byte strategy = representatives[family];
                    WriteNaturalCoefficientOrder(strategy, orders.Slice(offsets[3 * family], GetCoefficientOrderSize(strategy)), cancellationToken);
                }
            }

            // Without encoded orders there are no permutation entropy tables or symbols in the bitstream.
            if (encodedFamilies == 0)
            {
                return result;
            }

            using JxlEntropyDecoder codes = JxlEntropyDecoder.Read(ref reader, allocator, JxlPermutation.ContextCount, true);
            JxlEntropyDecoder.Reader symbols = codes.CreateReader(ref reader);
            int workspaceLength = 2 * maximumCustomSize;
            IMemoryOwner<uint>? workspaceOwner = this.orderWorkspace;
            if (workspaceLength != 0 && (workspaceOwner is null || workspaceOwner.Memory.Length < workspaceLength))
            {
                IMemoryOwner<uint> replacement = allocator.Allocate<uint>(workspaceLength);
                workspaceOwner?.Dispose();
                this.orderWorkspace = workspaceOwner = replacement;
            }

            int historyLength = codes.UsesLz77
                ? (int)BitOperations.RoundUpToPowerOf2(Math.Min(symbolBudget, JxlEntropyDecoder.MaximumLz77Distance))
                : 0;

            // Order streams finish before coefficient decoding. Reuse this ring across pass setup and retain
            // its allocation for that later phase. Each stream starts at position zero, so old values need no copy.
            IMemoryOwner<uint>? historyOwner = this.entropyHistory;
            if (historyLength != 0 && (historyOwner is null || historyOwner.Memory.Length < historyLength))
            {
                IMemoryOwner<uint> replacement = allocator.Allocate<uint>(historyLength);
                historyOwner?.Dispose();
                this.entropyHistory = historyOwner = replacement;
            }

            // Borrow tracked scratch once before the entropy loop. At the maximum 256-by-256 transform, the
            // natural-order and exact-size rank spans need 512 KiB together. No permutation rents its own buffers.
            Span<uint> workspace = workspaceOwner is null ? [] : workspaceOwner.Memory.Span[..workspaceLength];
            Span<uint> history = historyOwner is null ? [] : historyOwner.Memory.Span[..historyLength];
            uint position = 0;
            for (int family = 0; family < OrderCount; family++)
            {
                if ((encodedFamilies & (1U << family)) == 0)
                {
                    continue;
                }

                cancellationToken.ThrowIfCancellationRequested();
                byte strategy = representatives[family];
                int size = GetCoefficientOrderSize(strategy);
                int skip = size / CoefficientBlockArea;
                bool retained = (usedFamilies & (1U << family)) != 0;
                Span<uint> natural = retained ? workspace[..size] : [];
                Span<uint> ranks = retained ? workspace.Slice(maximumCustomSize, size - skip) : [];
                if (retained)
                {
                    WriteNaturalCoefficientOrder(strategy, natural, cancellationToken);
                }

                for (int component = 0; component < 3; component++)
                {
                    Span<uint> destination = retained ? orders.Slice(offsets[(3 * family) + component], size) : [];
                    uint length = JxlPermutation.ReadLength(ref reader, ref symbols, size, skip, history, ref position);
                    JxlPermutation.ReadDigits(
                        ref reader, ref symbols, size, skip, length, natural, destination, ranks, history, ref position, cancellationToken);
                }
            }

            // One reader covers all selected component permutations. LZ77 copies can continue across their boundaries.
            // Validate ANS once after the required symbols; the shared reader permits an unfinished terminal copy.
            symbols.CheckFinalState();
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

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

    /// <summary>
    /// Releases permutation scratch after order setup or during failure cleanup. Completed pass orders have separate owners.
    /// </summary>
    private void DisposeOrderWorkspace()
    {
        this.orderWorkspace?.Dispose();
        this.orderWorkspace = null;
    }
}
