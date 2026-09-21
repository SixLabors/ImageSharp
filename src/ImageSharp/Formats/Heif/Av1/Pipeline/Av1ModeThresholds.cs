// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// Controls mode rejection using quantizer-scaled costs and recent block winners.
/// </summary>
internal static class Av1ModeThresholds
{
    /// <summary>
    /// The thirteen intra modes, twenty-eight single-reference modes, and 128 compound modes.
    /// </summary>
    public const int ModeCount = 169;

    /// <summary>
    /// The first compound entry, after intra and single-reference entries.
    /// </summary>
    private const int CompoundStart = 41;

    /// <summary>
    /// Gets the base cost multipliers in prediction-mode order within each reference combination.
    /// </summary>
    private static ReadOnlySpan<ushort> Multipliers =>
    [
        1000, 1800, 2000, 2500, 2500, 2500, 2500, 2000, 2000, 2200, 2000, 2000, 1000,

        // Single references: LAST, LAST2, LAST3, GOLDEN, BWDREF, ALTREF2, ALTREF.
        // Each group contains NEAREST, NEAR, GLOBAL, and NEW.
        300, 1000, 2200, 1000, 300, 1000, 2000, 1000, 300, 1000, 2000, 1000,
        300, 1000, 2000, 1000, 300, 1000, 2400, 1000, 300, 1000, 2000, 1100, 300, 1000, 2400, 1000,

        // Four forward references paired with BWDREF, ALTREF2, then ALTREF. Each group follows
        // NEAREST-NEAREST, NEAR-NEAR, NEAREST-NEW, NEW-NEAREST, NEAR-NEW, NEW-NEAR, GLOBAL-GLOBAL, NEW-NEW.
        1000, 1200, 1500, 1500, 1360, 1700, 2250, 2400,
        1000, 1200, 1500, 1500, 1700, 1700, 2500, 2000,
        1000, 1200, 1500, 1500, 1870, 1700, 2500, 2000,
        1000, 1200, 1500, 1500, 1700, 1700, 2500, 2000,
        1000, 1200, 1800, 1500, 1700, 1700, 2500, 2000,
        1000, 1200, 1500, 1500, 1700, 1700, 2500, 2000,
        1000, 1440, 1500, 1500, 1700, 1700, 2500, 2000,
        1000, 1200, 1500, 1500, 1700, 1700, 2750, 2000,
        1100, 1200, 1500, 1500, 1530, 1870, 2750, 2400,
        1000, 1200, 1500, 1500, 1870, 1700, 2500, 1800,
        800, 1200, 1500, 1500, 1700, 1700, 3000, 2000,
        900, 1320, 1500, 1500, 2040, 1700, 2250, 2000,

        // Unidirectional pairs: LAST/LAST2, LAST/LAST3, LAST/GOLDEN, BWDREF/ALTREF.
        2000, 1600, 2000, 2000, 2640, 2200, 3200, 2400,
        2000, 1600, 2000, 1800, 2200, 2200, 3200, 2400,
        2000, 1760, 2400, 2000, 1760, 2640, 3200, 2400,
        2000, 1600, 2000, 2000, 2200, 1980, 3200, 2640
    ];

    /// <summary>
    /// Gets the block-size factors in quarter units, with 8x8 as the unit baseline.
    /// </summary>
    private static ReadOnlySpan<byte> BlockFactors =>
    [
        2, 3, 3, 4, 6, 6, 8, 12, 12, 16, 24, 24, 32, 48, 48, 64, 4, 4, 8, 8, 16, 16
    ];

    /// <summary>
    /// Gets the history entry for a syntax mode and its ordered reference pair.
    /// </summary>
    public static int GetIndex(Av1PredictionMode mode, Av1ReferenceFrameType primary, Av1ReferenceFrameType secondary)
    {
        if (primary == Av1ReferenceFrameType.Intra)
        {
            return (int)mode;
        }

        if (secondary <= Av1ReferenceFrameType.Intra)
        {
            return (int)Av1PredictionMode.IntraModes + (((int)primary - 1) * 4) +
                (int)mode - (int)Av1PredictionMode.SingleInterModeStart;
        }

        // Syntax supplies only the twelve forward/backward pairs and four unidirectional pairs.
        // This compact index keeps a separate history for every legal combination without a sparse cube.
        int pair = primary <= Av1ReferenceFrameType.Golden && secondary > Av1ReferenceFrameType.Golden
            ? (((int)secondary - (int)Av1ReferenceFrameType.Golden - 1) * 4) + (int)primary - 1
            : 12 + (primary == Av1ReferenceFrameType.Last ? (int)secondary - 2 : 3);

        return CompoundStart + (pair * 8) + (int)mode - (int)Av1PredictionMode.CompoundInterModeStart;
    }

    /// <summary>
    /// Determines whether the current winner is cheaper than a mode's adaptive search threshold.
    /// </summary>
    public static bool ShouldSkip(
        ReadOnlySpan<int> factors,
        int quantizerFactor,
        int skippableMultiplier,
        Av1BlockSize blockSize,
        Av1PredictionMode mode,
        Av1ReferenceFrameType primary,
        Av1ReferenceFrameType secondary,
        long bestCost,
        bool bestSkippable)
    {
        // All nearest single-reference predictors establish the initial bound and are never threshold-pruned.
        if (mode == Av1PredictionMode.NearestMotionVector)
        {
            return false;
        }

        int index = GetIndex(mode, primary, secondary);
        int scale = quantizerFactor * BlockFactors[(int)blockSize];
        int multiplier = Multipliers[index];
        int threshold = multiplier < int.MaxValue / scale ? multiplier * scale / 4 : int.MaxValue;
        long adaptiveThreshold = ((long)threshold * factors[((int)blockSize * ModeCount) + index]) >> 5;
        if (bestSkippable)
        {
            adaptiveThreshold = (adaptiveThreshold * skippableMultiplier) >> 12;
        }

        return bestCost < adaptiveThreshold;
    }

    /// <summary>
    /// Determines whether a nonzero estimated motion candidate exceeds its adaptive search threshold.
    /// </summary>
    /// <param name="factors">The block-size and mode history factors.</param>
    /// <param name="quantizerFactor">The quantizer-dependent threshold scale.</param>
    /// <param name="blockSize">The current prediction block size.</param>
    /// <param name="mode">The single-reference prediction mode.</param>
    /// <param name="reference">The prediction reference slot.</param>
    /// <param name="zeroMotion">Whether the candidate has zero displacement.</param>
    /// <param name="framesSinceGolden">The number of frames since the golden reference was refreshed.</param>
    /// <param name="bestCost">The best complete candidate cost.</param>
    /// <param name="bestSkippable">Whether the current winner skips its residual.</param>
    /// <param name="aggressive">Whether the threshold receives an additional factor of two.</param>
    /// <returns>Whether to omit the candidate.</returns>
    public static bool ShouldSkipEstimated(
        ReadOnlySpan<int> factors,
        int quantizerFactor,
        Av1BlockSize blockSize,
        Av1PredictionMode mode,
        Av1ReferenceFrameType reference,
        bool zeroMotion,
        int framesSinceGolden,
        long bestCost,
        bool bestSkippable,
        bool aggressive)
    {
        if (zeroMotion)
        {
            return false;
        }

        int index = GetIndex(mode, reference, Av1ReferenceFrameType.None);
        int scale = quantizerFactor * BlockFactors[(int)blockSize];
        int multiplier = Multipliers[index];
        long threshold = multiplier < int.MaxValue / scale ? multiplier * scale / 4 : int.MaxValue;
        int extraShift = aggressive ? 1 : 0;
        threshold <<= extraShift + (bestSkippable ? 1 : 0);
        if (reference != Av1ReferenceFrameType.Last)
        {
            // Older secondary references must justify more search work. Apply their age adjustment
            // before multiplying by the mode history, preserving the threshold's integer rounding.
            threshold <<= 1;
            if (reference == Av1ReferenceFrameType.Golden && framesSinceGolden > 4)
            {
                threshold <<= extraShift + 1;
            }
        }

        return threshold == int.MaxValue || bestCost < ((threshold * factors[((int)blockSize * ModeCount) + index]) >> 5);
    }

    /// <summary>
    /// Updates estimated-mode thresholds for the winning reference and neighboring size classes.
    /// </summary>
    /// <param name="factors">The retained block-size and mode history.</param>
    /// <param name="blockSize">The selected coding-block size.</param>
    /// <param name="reference">The selected single reference, or intra.</param>
    /// <param name="winner">The selected prediction mode.</param>
    /// <param name="adaptation">The configured threshold adaptation level.</param>
    public static void UpdateEstimated(
        Span<int> factors,
        Av1BlockSize blockSize,
        Av1ReferenceFrameType reference,
        Av1PredictionMode winner,
        int adaptation)
    {
        ReadOnlySpan<Av1PredictionMode> modes = reference == Av1ReferenceFrameType.Intra
            ? [Av1PredictionMode.DC, Av1PredictionMode.Vertical, Av1PredictionMode.Horizontal, Av1PredictionMode.Smooth]
            : [Av1PredictionMode.NearestMotionVector, Av1PredictionMode.NearMotionVector,
                Av1PredictionMode.GlobalMotionVector, Av1PredictionMode.NewMotionVector];

        int winnerIndex = GetIndex(winner, reference, Av1ReferenceFrameType.None);
        int firstSize = Math.Max((int)blockSize - 3, (int)Av1BlockSize.Block4x4);
        int lastSize = Math.Min((int)blockSize + 6, (int)Av1BlockSize.Block128x128);
        foreach (Av1PredictionMode mode in modes)
        {
            int index = GetIndex(mode, reference, Av1ReferenceFrameType.None);
            for (int size = firstSize; size <= lastSize; size += 3)
            {
                // Three enum positions separate adjacent square size classes. Only the winning
                // reference's tested mode family contributes to this history update.
                ref int value = ref factors[(size * ModeCount) + index];
                value = index == winnerIndex ? value - (value >> 4) : Math.Min(value + 1, adaptation * 64);
            }
        }
    }

    /// <summary>
    /// Updates the winning mode and neighboring block sizes after one completed inter-picture block search.
    /// </summary>
    public static void Update(
        Span<int> factors,
        Av1BlockSize blockSize,
        Av1BlockSize superblockSize,
        int winner,
        bool singleReference,
        int adaptation)
    {
        // Ordinary shapes share evidence with the two neighboring size entries in each direction.
        // The trailing 1:4/4:1 shapes update only themselves; their enum neighbors are unrelated geometries.
        int firstSize = blockSize > superblockSize ? (int)blockSize : Math.Max(0, (int)blockSize - 2);
        int lastSize = blockSize > superblockSize ? (int)blockSize : Math.Min((int)superblockSize, (int)blockSize + 2);
        int modeEnd = singleReference ? CompoundStart : ModeCount;
        int maximum = adaptation * 64;
        for (int size = firstSize; size <= lastSize; size++)
        {
            Span<int> row = factors.Slice(size * ModeCount, modeEnd);
            for (int index = 0; index < row.Length; index++)
            {
                // Factors have five fractional bits. A winner decays by one sixteenth; other modes
                // rise by one thirty-second up to the configured cap, making repeated losses cheaper to reject.
                int value = row[index];
                row[index] = index == winner ? value - (value >> 4) : Math.Min(value + 1, maximum);
            }
        }
    }
}
