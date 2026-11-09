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
    private const int CoefficientBlockArea = BlockSize * BlockSize;

    private IMemoryOwner<uint>? orderWorkspace;

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
        ReadOnlySpan<byte> families = JxlCoefficientOrder.StrategyOrderFamilies;
        for (int strategy = 0; strategy < JxlCoefficientOrder.StrategyCount; strategy++)
        {
            if ((usedStrategies & (1U << strategy)) != 0)
            {
                usedFamilies |= 1U << families[strategy];
            }
        }

        offsets.Fill(-1);
        ReadOnlySpan<byte> representatives = JxlCoefficientOrder.OrderFamilyStrategies;
        int retainedCount = 0;
        int maximumCustomSize = 0;
        uint symbolBudget = 0;
        for (int family = 0; family < JxlCoefficientOrder.OrderCount; family++)
        {
            int size = JxlCoefficientOrder.GetCoefficientOrderSize(representatives[family]);
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
            for (int family = 0; family < JxlCoefficientOrder.OrderCount; family++)
            {
                if ((usedFamilies & (1U << family)) != 0 && (encodedFamilies & (1U << family)) == 0)
                {
                    byte strategy = representatives[family];
                    JxlCoefficientOrder.WriteNaturalCoefficientOrder(
                        strategy,
                        orders.Slice(offsets[3 * family], JxlCoefficientOrder.GetCoefficientOrderSize(strategy)),
                        cancellationToken);
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
            for (int family = 0; family < JxlCoefficientOrder.OrderCount; family++)
            {
                if ((encodedFamilies & (1U << family)) == 0)
                {
                    continue;
                }

                cancellationToken.ThrowIfCancellationRequested();
                byte strategy = representatives[family];
                int size = JxlCoefficientOrder.GetCoefficientOrderSize(strategy);
                int skip = size / CoefficientBlockArea;
                bool retained = (usedFamilies & (1U << family)) != 0;
                Span<uint> natural = retained ? workspace[..size] : [];
                Span<uint> ranks = retained ? workspace.Slice(maximumCustomSize, size - skip) : [];
                if (retained)
                {
                    JxlCoefficientOrder.WriteNaturalCoefficientOrder(strategy, natural, cancellationToken);
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
    /// Releases permutation scratch after order setup or during failure cleanup. Completed pass orders have separate owners.
    /// </summary>
    private void DisposeOrderWorkspace()
    {
        this.orderWorkspace?.Dispose();
        this.orderWorkspace = null;
    }
}
