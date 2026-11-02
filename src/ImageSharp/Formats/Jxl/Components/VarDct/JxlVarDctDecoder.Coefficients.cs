// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Components.Entropy;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.VarDct;

internal sealed partial class JxlVarDctDecoder
{
    private IMemoryOwner<byte>? coefficientStorage;
    private IMemoryOwner<byte>? nonzeroStorage;

    /// <summary>
    /// Owns the uint history reused by order setup and then coefficient decoding. Their entropy streams do not overlap.
    /// </summary>
    private IMemoryOwner<uint>? entropyHistory;

    private Memory<byte> coefficientMemory;
    private Memory<byte> nonzeroMemory;
    private Memory<uint> coefficientHistoryMemory;
    private InlineArray4<int> coefficientOffsets;
    private InlineArray4<int> coefficientCounts;
    private int acGroupColumns;

    /// <summary>
    /// Gets the format's frequency-position buckets, already multiplied by two for the previous-symbol context bit.
    /// </summary>
    private static ReadOnlySpan<byte> CoefficientFrequencyContexts =>
    [

        // libjxl ac_context.h defines these buckets. Index zero is unused: AC scanning starts after the LLF prefix.
        0, 0, 2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24, 26, 28,
        30, 30, 32, 32, 34, 34, 36, 36, 38, 38, 40, 40, 42, 42, 44, 44,
        46, 46, 46, 46, 48, 48, 48, 48, 50, 50, 50, 50, 52, 52, 52, 52,
        54, 54, 54, 54, 56, 56, 56, 56, 58, 58, 58, 58, 60, 60, 60, 60
    ];

    /// <summary>
    /// Gets the format's remaining-nonzero buckets, already multiplied by two for the previous-symbol context bit.
    /// </summary>
    private static ReadOnlySpan<ushort> CoefficientNonzeroContexts =>
    [
        0, 0, 62, 124, 124, 186, 186, 186, 186, 246, 246, 246, 246, 304, 304, 304,
        304, 304, 304, 304, 304, 360, 360, 360, 360, 360, 360, 360, 360, 360, 360, 360,
        360, 412, 412, 412, 412, 412, 412, 412, 412, 412, 412, 412, 412, 412, 412, 412,
        412, 412, 412, 412, 412, 412, 412, 412, 412, 412, 412, 412, 412, 412, 412, 412
    ];

    /// <summary>
    /// Borrows the current group's packed sixteen-bit coefficients for one component.
    /// </summary>
    /// <param name="component">The X, Y or B component index.</param>
    /// <returns>The accumulated coefficients after all passes read so far.</returns>
    /// <remarks>
    /// Call this method only when <see cref="Uses16BitCoefficients"/> is true. Borrow once before block traversal.
    /// Each active transform occupies its complete coefficient area, in transform-origin row order.
    /// Skip inactive subsampled components when advancing a component's offset. Lowest-frequency prefix slots remain zero.
    /// The next group's first pass overwrites this workspace, so reconstruction must finish before that pass starts.
    /// </remarks>
    public ReadOnlySpan<short> GetCoefficients16(int component)
        => MemoryMarshal.Cast<byte, short>(this.coefficientMemory.Span).Slice(this.coefficientOffsets[component], this.coefficientCounts[component]);

    /// <summary>
    /// Borrows the current group's packed thirty-two-bit coefficients for one component.
    /// </summary>
    /// <param name="component">The X, Y or B component index.</param>
    /// <returns>The accumulated coefficients after all passes read so far.</returns>
    /// <remarks>
    /// Call this method only when <see cref="Uses16BitCoefficients"/> is false. The packing and lifetime match <see cref="GetCoefficients16"/>.
    /// </remarks>
    public ReadOnlySpan<int> GetCoefficients32(int component)
        => MemoryMarshal.Cast<byte, int>(this.coefficientMemory.Span).Slice(this.coefficientOffsets[component], this.coefficientCounts[component]);

    /// <summary>
    /// Reads one group's selected pass and reconstructs each completed block during its final pass.
    /// </summary>
    /// <param name="reader">The bounded AC-group reader at its histogram selector.</param>
    /// <param name="group">The validated row-major AC group index.</param>
    /// <param name="pass">The progressive pass index, starting at zero for each group.</param>
    /// <param name="cancellationToken">The token checked between block rows and transform origins.</param>
    /// <remarks>
    /// Call <see cref="PrepareReconstruction"/> before reading groups. Read all passes of one group before starting another group.
    /// Pass zero clears only the active coefficient planes; later passes add their shifted values.
    /// Each final-pass block consumes all accumulated contributions before the same traversal advances to the next transform.
    /// Nonzero predictors and LZ77 positions start again for every group/pass, as prescribed by the entropy syntax.
    /// </remarks>
    public void ReadAcGroup(ref JxlBitReader reader, int group, int pass, CancellationToken cancellationToken)
    {
        Rectangle rectangle = this.GetAcGroupRectangle(group);
        if (pass == 0)
        {
            // Predictor counts restart for each pass. The second half instead records whether any pass
            // supplied AC for a block, so reconstruction can recognize constant blocks after all passes.
            this.nonzeroMemory.Span[(this.nonzeroMemory.Length / 2)..].Clear();
            for (int component = 0; component < 3; component++)
            {
                int width = rectangle.Width >> this.horizontalShifts[component];
                int height = rectangle.Height >> this.verticalShifts[component];
                this.coefficientCounts[component] = width * height * CoefficientBlockArea;
            }
        }

        // Dispatch once for the group. Generic integer operations then specialize to short or int stores,
        // without a precision branch, boxing or interface dispatch for each coefficient.
        if (this.Uses16BitCoefficients)
        {
            this.ReadAcGroup(ref reader, rectangle, pass, MemoryMarshal.Cast<byte, short>(this.coefficientMemory.Span), cancellationToken);
        }
        else
        {
            this.ReadAcGroup(ref reader, rectangle, pass, MemoryMarshal.Cast<byte, int>(this.coefficientMemory.Span), cancellationToken);
        }
    }

    /// <summary>
    /// Locates one AC group in the padded eight-pixel block grid shared by strategies and DC contexts.
    /// </summary>
    /// <param name="group">The validated row-major AC group index.</param>
    /// <returns>The group rectangle clipped to the padded block dimensions.</returns>
    private Rectangle GetAcGroupRectangle(int group)
    {
        int x = (group % this.acGroupColumns) * this.acGroupBlockDimension;
        int y = (group / this.acGroupColumns) * this.acGroupBlockDimension;
        return new Rectangle(x, y, Math.Min(this.acGroupBlockDimension, this.dcSize.Width - x), Math.Min(this.acGroupBlockDimension, this.dcSize.Height - y));
    }

    /// <summary>
    /// Prepares one largest active group after all pass entropy bounds have selected the coefficient precision.
    /// </summary>
    /// <param name="allocator">The tracked frame allocator.</param>
    private void PrepareCoefficientStorage(MemoryAllocator allocator)
    {
        this.acGroupColumns = ((this.dcSize.Width - 1) / this.acGroupBlockDimension) + 1;
        int width = Math.Min(this.dcSize.Width, this.acGroupBlockDimension);
        int height = Math.Min(this.dcSize.Height, this.acGroupBlockDimension);
        int count = 0;
        bool usesHistory = false;
        for (int component = 0; component < 3; component++)
        {
            this.coefficientOffsets[component] = count;
            count += (width >> this.horizontalShifts[component]) * (height >> this.verticalShifts[component]) * CoefficientBlockArea;
        }

        this.coefficientOffsets[3] = count;
        int elementSize = this.Uses16BitCoefficients ? sizeof(short) : sizeof(int);

        // One byte allocation owns the selected short or int layout. Cached Memory values retain that allocation
        // for typed span borrowing; they do not allocate buffers or bypass the tracked allocator.
        this.coefficientStorage = allocator.Allocate<byte>(count * elementSize);
        this.coefficientMemory = this.coefficientStorage.Memory;

        // Share one allocation between pass-local count predictors and group-wide AC-presence flags.
        // Both use the same component block grids; only the presence flags survive between progressive passes.
        this.nonzeroStorage = allocator.Allocate<byte>(2 * (count / CoefficientBlockArea));
        this.nonzeroMemory = this.nonzeroStorage.Memory;
        foreach (AcPass pass in this.acPasses)
        {
            usesHistory |= pass.Entropy.UsesLz77;
        }

        // Each active transform reads one count plus at most size - coveredBlocks coefficient values.
        // That is at most size symbols. The complete group therefore needs no more history than its coefficient capacity.
        // Reuse one power-of-two ring across groups and passes; a new logical position prevents reads of old contents.
        if (usesHistory)
        {
            int historyLength = (int)BitOperations.RoundUpToPowerOf2((uint)Math.Min(count, JxlEntropyDecoder.MaximumLz77Distance));
            IMemoryOwner<uint>? historyOwner = this.entropyHistory;
            if (historyOwner is null || historyOwner.Memory.Length < historyLength)
            {
                // Order decoding has ended. Grow before traversal, without copying previous stream values.
                // Allocate first so a failed rent leaves the preceding owner available to frame cleanup.
                IMemoryOwner<uint> replacement = allocator.Allocate<uint>(historyLength);
                historyOwner?.Dispose();
                this.entropyHistory = historyOwner = replacement;
            }

            // Each AC stream starts its logical decoded position at zero. Its distance bound prevents reads
            // of preceding order or group values, so retaining storage needs neither a copy nor a clear.
            this.coefficientHistoryMemory = historyOwner.Memory[..historyLength];
        }
        else
        {
            // Only order setup needed a history. No coefficient pass can consume it, so return it now.
            this.entropyHistory?.Dispose();
            this.entropyHistory = null;
        }
    }

    /// <summary>
    /// Reads one typed pass through the shared entropy reader, block contexts and scan-order tables.
    /// </summary>
    /// <typeparam name="TCoefficient">The selected short or int coefficient storage.</typeparam>
    /// <param name="reader">The bounded group reader at its histogram selector.</param>
    /// <param name="rectangle">The group in the full padded block grid.</param>
    /// <param name="pass">The selected progressive pass.</param>
    /// <param name="coefficients">The reused typed workspace containing all three component planes.</param>
    /// <param name="cancellationToken">The token checked between block rows and transform origins.</param>
    private void ReadAcGroup<TCoefficient>(
        ref JxlBitReader reader, Rectangle rectangle, int pass, Span<TCoefficient> coefficients, CancellationToken cancellationToken)
        where TCoefficient : unmanaged, IBinaryInteger<TCoefficient>
    {
        JxlEntropyDecoder entropy = this.acPasses[pass].Entropy;
        if (entropy.UsesPrefixCodes)
        {
            if (entropy.UsesLz77)
            {
                this.ReadAcGroup<TCoefficient, JxlEntropyDecoder.PrefixLz77Operator>(ref reader, rectangle, pass, coefficients, cancellationToken);
            }
            else
            {
                this.ReadAcGroup<TCoefficient, JxlEntropyDecoder.PrefixOperator>(ref reader, rectangle, pass, coefficients, cancellationToken);
            }
        }
        else if (entropy.UsesLz77)
        {
            this.ReadAcGroup<TCoefficient, JxlEntropyDecoder.AnsLz77Operator>(ref reader, rectangle, pass, coefficients, cancellationToken);
        }
        else if (entropy.CanUseAns420)
        {
            // The pass retains one histogram set. Its fixed 4/2/0 expansion applies to every coefficient context.
            this.ReadAcGroup<TCoefficient, JxlEntropyDecoder.Ans420Operator>(ref reader, rectangle, pass, coefficients, cancellationToken);
        }
        else
        {
            this.ReadAcGroup<TCoefficient, JxlEntropyDecoder.AnsOperator>(ref reader, rectangle, pass, coefficients, cancellationToken);
        }
    }

    /// <summary>
    /// Reads one pass with coefficient storage and entropy mode selected outside the transform traversal.
    /// </summary>
    /// <typeparam name="TCoefficient">The selected short or int coefficient storage.</typeparam>
    /// <typeparam name="TOperator">The pass's ANS or prefix decoder, with or without LZ77.</typeparam>
    /// <param name="reader">The bounded group reader at its histogram selector.</param>
    /// <param name="rectangle">The group in the full padded block grid.</param>
    /// <param name="pass">The selected progressive pass.</param>
    /// <param name="coefficients">The reused typed workspace containing all three component planes.</param>
    /// <param name="cancellationToken">The token checked between block rows and transform origins.</param>
    private void ReadAcGroup<TCoefficient, TOperator>(
        ref JxlBitReader reader, Rectangle rectangle, int pass, Span<TCoefficient> coefficients, CancellationToken cancellationToken)
        where TCoefficient : unmanaged, IBinaryInteger<TCoefficient>
        where TOperator : struct, JxlEntropyDecoder.IEntropyOperator
    {
        AcPass passInfo = this.acPasses[pass];
        int histogramBits = 32 - BitOperations.LeadingZeroCount(this.AcHistogramSetCount - 1);
        uint histogram = reader.ReadBits(histogramBits);
        if (histogram >= this.AcHistogramSetCount)
        {
            // A non-power-of-two count leaves unused selector codes. They cannot index another pass's context map.
            throw new InvalidImageContentException("The JPEG XL AC histogram selector is outside its permitted range.");
        }

        int histogramOffset = (int)histogram * this.AcContextCount;
        int blockContextCount = this.BlockContextCount;
        int dcContextCount = this.DcContextCount;
        int coefficientShift = passInfo.Shift;
        JxlEntropyDecoder.Reader symbols = passInfo.Entropy.CreateReader(ref reader);
        ReadOnlySpan<uint> orders = passInfo.Orders.Memory.Span;
        ReadOnlySpan<int> orderOffsets = passInfo.Offsets;
        ReadOnlySpan<byte> blockContexts = this.BlockContextMap;
        ReadOnlySpan<uint> quantizerThresholds = this.QuantizationThresholds;

        // Native and Rust select the entropy mode before decoding coefficients. The constrained operator
        // removes unused coding and history paths from this loop while retaining one shared traversal.
        Span<uint> history = TOperator.UsesLz77 ? this.coefficientHistoryMemory.Span : [];
        ReadOnlySpan<byte> widths = StrategyWidths;
        ReadOnlySpan<byte> heights = StrategyHeights;
        ReadOnlySpan<byte> orderFamilies = StrategyOrderFamilies;
        Span<byte> nonzeroStorage = this.nonzeroMemory.Span;
        Span<byte> nonzeros = nonzeroStorage[..(nonzeroStorage.Length / 2)];
        Span<byte> hasAc = nonzeroStorage[nonzeros.Length..];
        bool reconstruct = pass == this.acPasses.Count - 1;
        Span<float> reconstruction = reconstruct ? this.reconstructionMemory.Span : [];
        ReadOnlySpan<float> matrices = reconstruct ? this.QuantizationMatrices : [];
        ReadOnlySpan<int> matrixOffsets = reconstruct ? this.QuantizationMatrixOffsets : [];
        InlineArray64<TCoefficient> zeroStorage = default;
        ReadOnlySpan<TCoefficient> zeros = zeroStorage;
        Buffer2DRegion<byte> strategies = this.Strategies.GetSubRegion(rectangle);
        Buffer2DRegion<int> quantization = this.Quantization.GetSubRegion(rectangle);
        Buffer2DRegion<byte> dcContexts = this.DcContexts.GetSubRegion(rectangle);
        InlineArray4<int> positionStorage = default;
        Span<int> positions = positionStorage;
        uint decodedCount = 0;

        // Predictor rows need no clear. Every preceding row/left cell is written by an already decoded transform;
        // the first cell uses 32, the first row uses left, and the first column uses top. Continuations are filled too.
        // Coefficients do need a clear before pass zero: unvisited AC slots and every LLF prefix must start at zero.
        for (int component = 0; component < 3; component++)
        {
            positions[component] = this.coefficientOffsets[component];
            if (pass == 0)
            {
                coefficients.Slice(positions[component], this.coefficientCounts[component]).Clear();
            }
        }

        for (int y = 0; y < rectangle.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadOnlySpan<byte> strategyRow = strategies.DangerousGetRowSpan(y);
            ReadOnlySpan<int> quantizerRow = quantization.DangerousGetRowSpan(y);
            ReadOnlySpan<byte> dcRow = dcContexts.DangerousGetRowSpan(y);
            int fullY = rectangle.Y + y;
            ReadOnlySpan<sbyte> correlationX = reconstruct ? this.CorrelationX.DangerousGetRowSpan(fullY >> ColorTileShift) : [];
            ReadOnlySpan<sbyte> correlationB = reconstruct ? this.CorrelationB.DangerousGetRowSpan(fullY >> ColorTileShift) : [];
            for (int x = 0; x < rectangle.Width; x++)
            {
                byte packedStrategy = strategyRow[x];
                if ((packedStrategy & FirstBlockFlag) == 0)
                {
                    continue;
                }

                cancellationToken.ThrowIfCancellationRequested();
                byte strategy = (byte)(packedStrategy >> 1);
                int coveredWidth = widths[strategy];
                int coveredHeight = heights[strategy];
                int coveredBlocks = coveredWidth * coveredHeight;
                int size = coveredBlocks * CoefficientBlockArea;
                int blockShift = BitOperations.Log2((uint)coveredBlocks);
                int orderFamily = orderFamilies[strategy];
                int active = 0;
                int acComponents = 0;

                // Entropy always visits Y, X, B. Subsampled components only have a block at their aligned full-grid origins.
                // AC metadata already forbids multiblock transforms with subsampling, so an active reduced component still has 64 coefficients.
                for (int encodedComponent = 0; encodedComponent < 3; encodedComponent++)
                {
                    int component = encodedComponent < 2 ? encodedComponent ^ 1 : encodedComponent;
                    int horizontalShift = this.horizontalShifts[component];
                    int verticalShift = this.verticalShifts[component];
                    int sampleX = x >> horizontalShift;
                    int sampleY = y >> verticalShift;
                    if ((sampleX << horizontalShift) != x || (sampleY << verticalShift) != y)
                    {
                        continue;
                    }

                    int rowWidth = rectangle.Width >> horizontalShift;
                    active |= 1 << component;
                    int predictorOffset = (this.coefficientOffsets[component] / CoefficientBlockArea) + (sampleY * rowWidth) + sampleX;
                    int predicted = sampleX == 0
                        ? sampleY == 0 ? 32 : nonzeros[predictorOffset - rowWidth]
                        : sampleY == 0
                            ? nonzeros[predictorOffset - 1]
                            : (nonzeros[predictorOffset - rowWidth] + nonzeros[predictorOffset - 1] + 1) / 2;

                    // Encoded block contexts use full-grid DC classification but component-scaled horizontal QF coordinates.
                    // libjxl's enc_entropy_coder.cc writes that combination; reconstruction selects its spatial quantizer separately.
                    int quantizerBucket = 0;
                    foreach (uint threshold in quantizerThresholds)
                    {
                        if ((uint)quantizerRow[sampleX] > threshold)
                        {
                            quantizerBucket++;
                        }
                    }

                    int mapComponent = component < 2 ? component ^ 1 : component;
                    int mapIndex = (((mapComponent * OrderCount) + orderFamily) * (quantizerThresholds.Length + 1)) + quantizerBucket;
                    int blockContext = blockContexts[(mapIndex * dcContextCount) + dcRow[x]];
                    int predictionBucket = predicted < 8 ? predicted : 4 + (predicted / 2);
                    int countContext = histogramOffset + (predictionBucket * blockContextCount) + blockContext;
                    uint count = symbols.ReadUInt32<TOperator>(ref reader, countContext, history, decodedCount++, 0);
                    if (count > size - coveredBlocks)
                    {
                        throw new InvalidImageContentException("The JPEG XL AC nonzero count exceeds its transform size.");
                    }

                    int remaining = (int)count;
                    byte normalizedCount = (byte)((remaining + coveredBlocks - 1) >> blockShift);
                    for (int row = 0; row < coveredHeight; row++)
                    {
                        nonzeros.Slice(predictorOffset + (row * rowWidth), coveredWidth).Fill(normalizedCount);
                        if (count != 0)
                        {
                            hasAc.Slice(predictorOffset + (row * rowWidth), coveredWidth).Fill(1);
                        }
                    }

                    if (hasAc[predictorOffset] != 0)
                    {
                        acComponents |= 1 << component;
                    }

                    int densityOffset = histogramOffset + (blockContextCount * NonzeroContextCount) + (blockContext * ZeroDensityContextCount);
                    ReadOnlySpan<uint> order = orders.Slice(orderOffsets[(orderFamily * 3) + component], size);
                    Span<TCoefficient> block = coefficients.Slice(positions[component], size);
                    positions[component] += size;
                    if (remaining != 0)
                    {
                        // Empty scans consume no coefficient symbols. Their positions and predictor cells still advance above.
                        ReadAcCoefficients<TCoefficient, TOperator>(
                            ref reader,
                            ref symbols,
                            block,
                            order,
                            coveredBlocks,
                            blockShift,
                            coefficientShift,
                            densityOffset,
                            remaining,
                            history,
                            ref decodedCount);
                    }
                }

                if (reconstruct)
                {
                    // Earlier passes already occupy these typed blocks. The final pass has now added all
                    // three components, so reconstruct while the strategy, sampling and quantizer are still known.
                    // Inactive subsampled components need only one zero block; multiblock subsampling was rejected in AC metadata.
                    ReadOnlySpan<TCoefficient> qx = (active & 1) != 0 ? coefficients.Slice(positions[0] - size, size) : zeros[..size];
                    ReadOnlySpan<TCoefficient> qy = (active & 2) != 0 ? coefficients.Slice(positions[1] - size, size) : zeros[..size];
                    ReadOnlySpan<TCoefficient> qb = (active & 4) != 0 ? coefficients.Slice(positions[2] - size, size) : zeros[..size];
                    int fullX = rectangle.X + x;
                    int matrixOffset = matrixOffsets[GetQuantizationMatrixIndex(strategy, 0)];
                    Vector2 correlation = new(
                        this.BaseCorrelationX + (correlationX[fullX >> ColorTileShift] * this.ColorScale),
                        this.BaseCorrelationB + (correlationB[fullX >> ColorTileShift] * this.ColorScale));

                    this.ReconstructBlock(
                        fullX,
                        fullY,
                        strategy,
                        coveredWidth,
                        coveredHeight,
                        active,
                        acComponents,
                        qx,
                        qy,
                        qb,
                        matrices.Slice(matrixOffset, 3 * size),
                        reconstruction,
                        this.InverseGlobalScale / quantizerRow[x],
                        correlation);
                }
            }
        }

        symbols.CheckFinalState();
    }

    /// <summary>
    /// Reads one component's bounded AC scan and accumulates its contribution to the retained transform block.
    /// </summary>
    /// <typeparam name="TCoefficient">The selected short or int coefficient storage.</typeparam>
    /// <typeparam name="TOperator">The selected entropy and LZ77 mode.</typeparam>
    /// <param name="reader">The current bounded group/pass reader.</param>
    /// <param name="symbols">The current section's entropy state and borrowed tables.</param>
    /// <param name="block">The complete retained coefficient block, with 64 times coveredBlocks entries.</param>
    /// <param name="order">The validated permutation of this block's frequency positions, with the same length as block.</param>
    /// <param name="coveredBlocks">The transform's positive power-of-two area in 8 by 8 pixel blocks.</param>
    /// <param name="blockShift">The base-two logarithm of coveredBlocks.</param>
    /// <param name="coefficientShift">The current pass's unsigned coefficient shift.</param>
    /// <param name="densityOffset">The histogram and block-context offset of the 458-entry zero-density region.</param>
    /// <param name="remaining">The validated positive nonzero count, no greater than block.Length minus coveredBlocks.</param>
    /// <param name="history">The retained history ring, or an empty span without LZ77.</param>
    /// <param name="decodedCount">The decoded-value position, including counts and coefficients preceding this scan.</param>
    /// <exception cref="InvalidImageContentException">The scan cannot contain its declared nonzero coefficients or its entropy data is invalid.</exception>
    private static void ReadAcCoefficients<TCoefficient, TOperator>(
        ref JxlBitReader reader,
        ref JxlEntropyDecoder.Reader symbols,
        Span<TCoefficient> block,
        ReadOnlySpan<uint> order,
        int coveredBlocks,
        int blockShift,
        int coefficientShift,
        int densityOffset,
        int remaining,
        Span<uint> history,
        ref uint decodedCount)
        where TCoefficient : unmanaged, IBinaryInteger<TCoefficient>
        where TOperator : struct, JxlEntropyDecoder.IEntropyOperator
    {
        int size = block.Length;
        int previous = remaining > size / 16 ? 0 : 1;
        ref byte frequencyContexts = ref MemoryMarshal.GetReference(CoefficientFrequencyContexts);
        ref ushort nonzeroContexts = ref MemoryMarshal.GetReference(CoefficientNonzeroContexts);
        ref uint firstOrder = ref MemoryMarshal.GetReference(order);
        ref TCoefficient firstCoefficient = ref MemoryMarshal.GetReference(block);

        // The caller validates remaining <= size-coveredBlocks, and size=64*coveredBlocks. Ceiling division
        // therefore gives a nonzero bucket in 0..63. Only decrementing remaining preserves that bound throughout
        // the scan, including the final zero bucket. Both fixed context tables contain exactly 64 entries.
        int countBucket = Unsafe.Add(ref nonzeroContexts, (remaining + coveredBlocks - 1) >> blockShift);

        // The first coveredBlocks order entries are LLF coefficients restored from the DC plane later.
        // With remaining>0, k<=size-remaining gives coveredBlocks<=k<size. Thus k>>blockShift is in 0..63,
        // and order[k] lies within this complete block. ReadCoefficientOrders generates natural indices in 0..size-1;
        // JxlPermutation validates the Lehmer digits and only permutes those indices. The selected coefficient stays inside the block.
        // Borrow managed references once instead of rechecking those established bounds for each symbol.
        for (int k = coveredBlocks; remaining != 0 && k <= size - remaining; k++)
        {
            int context = densityOffset + countBucket + Unsafe.Add(ref frequencyContexts, k >> blockShift) + previous;
            uint value = symbols.ReadUInt32<TOperator>(ref reader, context, history, decodedCount++, 0);
            if (value == 0)
            {
                // Zero changes only the previous-symbol context. The coefficient and remaining-count bucket stay unchanged.
                previous = 0;
                continue;
            }

            // Unsigned zigzag maps 0, 1, 2, 3 to 0, -1, 1, -2. Shift unsigned bits before converting to signed storage.
            // Retain generic truncation and unchecked addition so short/int progressive accumulation keeps its original wrapping.
            uint signedBits = unchecked((value >> 1) ^ (0U - (value & 1)));
            int coefficient = unchecked((int)(signedBits << coefficientShift));
            int index = (int)Unsafe.Add(ref firstOrder, k);
            ref TCoefficient target = ref Unsafe.Add(ref firstCoefficient, index);
            target = unchecked(target + TCoefficient.CreateTruncating(coefficient));

            // Count the encoded nonzero even when progressive shifting wraps its stored coefficient to zero.
            previous = 1;
            remaining--;
            countBucket = Unsafe.Add(ref nonzeroContexts, (remaining + coveredBlocks - 1) >> blockShift);
        }

        // Keep the remaining-count loop bound above: malformed runs must stop before their contexts can
        // leave this block's zero-density region. They still fail here with the original content error.
        if (remaining != 0)
        {
            throw new InvalidImageContentException("The JPEG XL AC scan ends before all declared nonzero coefficients.");
        }
    }

    /// <summary>
    /// Releases the one-group coefficient and predictor allocations, and the history shared with order setup.
    /// </summary>
    private void DisposeCoefficientStorage()
    {
        this.entropyHistory?.Dispose();
        this.entropyHistory = null;
        this.nonzeroStorage?.Dispose();
        this.coefficientStorage?.Dispose();
        this.coefficientMemory = default;
        this.nonzeroMemory = default;
        this.coefficientHistoryMemory = default;
    }
}
