// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives.JxlFrameDimensions;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;

[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct JxlAcStrategy
{
    public const int MaximumCoefficientBlocks = 32;
    public const int MaximumBlockDimension = BlockDimensions * MaximumCoefficientBlocks;
    public const int MaximumCoefficientArea = MaximumBlockDimension * MaximumBlockDimension;
    public const int NumberOfValidStrategies = 27;

    private static readonly int MultiblockBits =
        GetTypeBit(JxlAcStrategyType.DCT16X16) | GetTypeBit(JxlAcStrategyType.DCT32X32) |
        GetTypeBit(JxlAcStrategyType.DCT16X8) | GetTypeBit(JxlAcStrategyType.DCT8X16) |
        GetTypeBit(JxlAcStrategyType.DCT32X8) | GetTypeBit(JxlAcStrategyType.DCT8X32) |
        GetTypeBit(JxlAcStrategyType.DCT16X32) | GetTypeBit(JxlAcStrategyType.DCT32X16) |
        GetTypeBit(JxlAcStrategyType.DCT32X64) | GetTypeBit(JxlAcStrategyType.DCT64X32) |
        GetTypeBit(JxlAcStrategyType.DCT64X64) | GetTypeBit(JxlAcStrategyType.DCT64X128) |
        GetTypeBit(JxlAcStrategyType.DCT128X64) |
        GetTypeBit(JxlAcStrategyType.DCT128X128) |
        GetTypeBit(JxlAcStrategyType.DCT128X256) |
        GetTypeBit(JxlAcStrategyType.DCT256X128) |
        GetTypeBit(JxlAcStrategyType.DCT256X256);

    private readonly bool isFirst;

    public JxlAcStrategy(JxlAcStrategyType strategy, bool isFirst)
    {
        this.Strategy = strategy;
        this.isFirst = isFirst;
    }

    public JxlAcStrategy(JxlAcStrategyType strategy)
        : this(strategy, true)
    {
    }

    public JxlAcStrategy(int rawStrategy)
        : this((JxlAcStrategyType)rawStrategy)
    {
    }

    private static ReadOnlySpan<byte> CoveredBlocksXLookup =>
    [
        1, 1, 1, 1,  2, 4,  1,  2,  1,
        4, 2, 4, 1,  1, 1,  1,  1,  1,
        8, 4, 8, 16, 8, 16, 32, 16, 32
    ];

    private static ReadOnlySpan<byte> CoveredBlocksYLookup =>
    [
        1, 1, 1, 1,  2,  4, 2,  1,  4,
        1, 4, 2, 1,  1,  1, 1,  1,  1,
        8, 8, 4, 16, 16, 8, 32, 32, 16
    ];

    private static ReadOnlySpan<byte> Log2CoveredBlocksLookup =>
    [
        0, 0, 0, 0, 2, 4, 1,  1, 2,
        2, 3, 3, 0, 0, 0, 0,  0, 0,
        6, 5, 5, 8, 7, 7, 10, 9, 9
    ];

    public readonly bool IsMultiblock => ((1 << (int)this.Strategy) & MultiblockBits) != 0;

    public readonly int RawStrategy => (int)this.Strategy;

    public readonly int CoveredBlocksX => CoveredBlocksXLookup[(int)this.Strategy];

    public readonly int CoveredBlocksY => CoveredBlocksYLookup[(int)this.Strategy];

    public readonly int Log2CoveredBlocks => Log2CoveredBlocksLookup[(int)this.Strategy];

    public readonly bool IsFirstBlock => this.isFirst;

    public readonly JxlAcStrategyType Strategy { get; }

    public readonly void ComputeNaturalCoefficientOrder(Span<int> order) => CoefficientOrderAndLookup(this, false, order);

    public readonly void ComputeNaturalCoefficientOrderLookup(Span<int> lookup) => CoefficientOrderAndLookup(this, true, lookup);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetTypeBit(JxlAcStrategyType type) => 1 << (int)type;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsRawStrategyValid(int rawStrategy) => rawStrategy is < NumberOfValidStrategies and >= 0;

    private static void CoefficientOrderAndLookup(JxlAcStrategy strategy, bool isLookup, Span<int> output)
    {
        // TODO: CoefficientLayout
        int cx = strategy.CoveredBlocksX;
        int cy = strategy.CoveredBlocksY;

        JxlForwardCoefficientOrder.CoefficientLayout(ref cx, ref cy);

        int xs = cx / cy;
        int xsm = xs - 1;
        int xss = JxlMath.CeilLog2Nonzero(xs);
        int cur = cx * cy;

        for (int i = 0; i < cx * BlockDimensions; i++)
        {
            for (int j = 0; j <= i; j++)
            {
                int x = j;
                int y = i - j;

                if ((i & 1) == 0)
                {
                    // swap
                    (x, y) = (y, x);
                }

                if ((y & xsm) != 0)
                {
                    continue;
                }

                y >>= xss;
                int value = 0;

                if (x < cx && y < cy)
                {
                    value = (y * cx) + x;
                }
                else
                {
                    value = cur++;
                }

                if (isLookup)
                {
                    output[((y * cx) * BlockDimensions) + x] = value;
                }
                else
                {
                    output[value] = ((y * cx) * BlockDimensions) + x;
                }
            }
        }

        for (int ip = (cx * BlockDimensions) - 1; ip > 0; ip--)
        {
            int i = ip - 1;

            for (int j = 0; j <= i; j++)
            {
                int x = (cx * BlockDimensions) - 1 - (i - j);
                int y = (cx * BlockDimensions) - 1 - j;

                if ((i & 1) != 0)
                {
                    // swap
                    (x, y) = (y, x);
                }

                if ((y & xsm) != 0)
                {
                    continue;
                }

                y >>= xss;
                int value = cur++;

                if (isLookup)
                {
                    output[((y * cx) * BlockDimensions) + x] = value;
                }
                else
                {
                    output[value] = ((y * cx) * BlockDimensions) + x;
                }
            }
        }
    }
}
