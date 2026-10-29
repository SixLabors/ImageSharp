// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.AcStrategy;

internal static partial class JxlAcStrategyEncoder
{
    private static readonly byte[][] TypeColors =
    [
        [0xFF, 0xFF, 0x00],  // DCT8       | yellow
        [0xFF, 0x80, 0x80],  // HORNUSS    | vivid tangerine
        [0xFF, 0x80, 0x80],  // DCT2x2     | vivid tangerine
        [0xFF, 0x80, 0x80],  // DCT4x4     | vivid tangerine
        [0x80, 0xFF, 0x00],  // DCT16x16   | chartreuse
        [0x00, 0xC0, 0x00],  // DCT32x32   | waystone green
        [0xC0, 0xFF, 0x00],  // DCT16x8    | lime
        [0xC0, 0xFF, 0x00],  // DCT8x16    | lime
        [0x00, 0xFF, 0x00],  // DCT32x8    | green
        [0x00, 0xFF, 0x00],  // DCT8x32    | green
        [0x00, 0xFF, 0x00],  // DCT32x16   | green
        [0x00, 0xFF, 0x00],  // DCT16x32   | green
        [0xFF, 0x80, 0x00],  // DCT4x8     | orange juice
        [0xFF, 0x80, 0x00],  // DCT8x4     | orange juice
        [0xFF, 0xFF, 0x80],  // AFV0       | butter
        [0xFF, 0xFF, 0x80],  // AFV1       | butter
        [0xFF, 0xFF, 0x80],  // AFV2       | butter
        [0xFF, 0xFF, 0x80],  // AFV3       | butter
        [0x00, 0xC0, 0xFF],  // DCT64x64   | capri
        [0x00, 0xFF, 0xFF],  // DCT64x32   | aqua
        [0x00, 0xFF, 0xFF],  // DCT32x64   | aqua
        [0x00, 0x40, 0xFF],  // DCT128x128 | rare blue
        [0x00, 0x80, 0xFF],  // DCT128x64  | magic ink
        [0x00, 0x80, 0xFF],  // DCT64x128  | magic ink
        [0x00, 0x00, 0xC0],  // DCT256x256 | keese blue
        [0x00, 0x00, 0xFF],  // DCT256x128 | blue
        [0x00, 0x00, 0xFF],  // DCT128x256 | blue
        [0x00, 0x00, 0x00] // invalid    | black
    ];

#pragma warning disable // Indentation warnings only

    private static readonly byte[][] Mask =
    [
        [
          0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
      ],                           // DCT8
      [
          0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 1, 0, 0, 1, 0, 0,  //
          0, 0, 1, 0, 0, 1, 0, 0,  //
          0, 0, 1, 1, 1, 1, 0, 0,  //
          0, 0, 1, 0, 0, 1, 0, 0,  //
          0, 0, 1, 0, 0, 1, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
      ],                           // HORNUSS
      [
        1, 1, 1, 1, 1, 1, 1, 1,  //
          1, 0, 1, 0, 1, 0, 1, 0,  //
          1, 1, 1, 1, 1, 1, 1, 1,  //
          1, 0, 1, 0, 1, 0, 1, 0,  //
          1, 1, 1, 1, 1, 1, 1, 1,  //
          1, 0, 1, 0, 1, 0, 1, 0,  //
          1, 1, 1, 1, 1, 1, 1, 1,  //
          1, 0, 1, 0, 1, 0, 1, 0,  //
      ],                           // 2x2
      [
        0, 0, 0, 0, 1, 0, 0, 0,  //
          0, 0, 0, 0, 1, 0, 0, 0,  //
          0, 0, 0, 0, 1, 0, 0, 0,  //
          0, 0, 0, 0, 1, 0, 0, 0,  //
          1, 1, 1, 1, 1, 1, 1, 1,  //
          0, 0, 0, 0, 1, 0, 0, 0,  //
          0, 0, 0, 0, 1, 0, 0, 0,  //
          0, 0, 0, 0, 1, 0, 0, 0,  //
      ],                           // 4x4
      [ ],                          // DCT16x16 (unused)
      [ ],                          // DCT32x32 (unused)
      [ ],                          // DCT16x8 (unused)
      [ ],                          // DCT8x16 (unused)
      [ ],                          // DCT32x8 (unused)
      [ ],                          // DCT8x32 (unused)
      [ ],                          // DCT32x16 (unused)
      [ ],                          // DCT16x32 (unused)
      [
        0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
          1, 1, 1, 1, 1, 1, 1, 1,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
      ],                           // DCT4x8
      [
        0, 0, 0, 0, 1, 0, 0, 0,  //
          0, 0, 0, 0, 1, 0, 0, 0,  //
          0, 0, 0, 0, 1, 0, 0, 0,  //
          0, 0, 0, 0, 1, 0, 0, 0,  //
          0, 0, 0, 0, 1, 0, 0, 0,  //
          0, 0, 0, 0, 1, 0, 0, 0,  //
          0, 0, 0, 0, 1, 0, 0, 0,  //
          0, 0, 0, 0, 1, 0, 0, 0,  //
      ],                           // DCT8x4
      [
        1, 1, 1, 1, 1, 0, 0, 0,  //
          1, 1, 1, 1, 0, 0, 0, 0,  //
          1, 1, 1, 0, 0, 0, 0, 0,  //
          1, 1, 0, 0, 0, 0, 0, 0,  //
          1, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
      ],                           // AFV0
      [
    0, 0, 0, 0, 1, 1, 1, 1,  //
          0, 0, 0, 0, 0, 1, 1, 1,  //
          0, 0, 0, 0, 0, 0, 1, 1,  //
          0, 0, 0, 0, 0, 0, 0, 1,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
      ],                           // AFV1
      [
    0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
          1, 0, 0, 0, 0, 0, 0, 0,  //
          1, 1, 0, 0, 0, 0, 0, 0,  //
          1, 1, 1, 0, 0, 0, 0, 0,  //
          1, 1, 1, 1, 0, 0, 0, 0,  //
      ],                           // AFV2
      [
    0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 0,  //
          0, 0, 0, 0, 0, 0, 0, 1,  //
          0, 0, 0, 0, 0, 0, 1, 1,  //
          0, 0, 0, 0, 0, 1, 1, 1,  //
      ],                           // AFV3
      [ ]                           // invalid
    ];

#pragma warning restore

    private static readonly TransformAttempt8x8[] Transforms8x8 =
    [
        new(JxlAcStrategyType.DCT, 9, 0.8),
        new(JxlAcStrategyType.DCT4X4, 5, 1.08),
        new(JxlAcStrategyType.DCT2X2, 5, 0.95),
        new(JxlAcStrategyType.DCT4X8, 4, 0.85931637428340035),
        new(JxlAcStrategyType.DCT8X4, 4, 0.85931637428340035),
        new(JxlAcStrategyType.IDENTITY, 5, 1.0427542510634957),
        new(JxlAcStrategyType.AFV0, 4, 0.81779489591359944),
        new(JxlAcStrategyType.AFV1, 4, 0.81779489591359944),
        new(JxlAcStrategyType.AFV2, 4, 0.81779489591359944),
        new(JxlAcStrategyType.AFV3, 4, 0.81779489591359944),
    ];

    public static ReadOnlySpan<byte> TypeColor(int rawStrategy)
    {
        DebugGuard.IsTrue(JxlAcStrategy.IsRawStrategyValid(rawStrategy), $"Raw strategy must be a valid strategy: {rawStrategy}");
        rawStrategy = Math.Clamp(rawStrategy, 0, JxlAcStrategy.NumberOfValidStrategies);
        return TypeColors[rawStrategy];
    }

    public static ReadOnlySpan<byte> TypeMask(int rawStrategy)
    {
        DebugGuard.IsTrue(JxlAcStrategy.IsRawStrategyValid(rawStrategy), $"Raw strategy must be a valid strategy: {rawStrategy}");
        rawStrategy = Math.Clamp(rawStrategy, 0, JxlAcStrategy.NumberOfValidStrategies);
        return Mask[rawStrategy];
    }

    public static bool MultiBlockTransformCrossesHorizontalBoundary(JxlAcStrategyImage acStrategyImage, int startX, int y, int endX)
    {
        if (startX >= acStrategyImage.XSize || y >= acStrategyImage.YSize)
        {
            return false;
        }

        if (y % 8 == 0)
        {
            // Nothing crosses 64x64 boundaries, and the memory on the other side
            // of the 64x64 block may still uninitialized.
            return false;
        }

        endX = Math.Min(endX, acStrategyImage.XSize);

        // The first multiblock might be before the start_x, let's adjust it
        // to point to the first IsFirstBlock() == true block we find by backward
        // tracing.
        JxlAcStrategyRow row = acStrategyImage.GetRow(y);
        int startXLimit = startX & ~7;

        while (startX != startXLimit && !row[startX].IsFirstBlock)
        {
            startX--;
        }

        for (int x = startX; x < endX;)
        {
            if (row[x].IsFirstBlock)
            {
                x += row[x].CoveredBlocksX;
            }
            else
            {
                return true;
            }
        }

        return false;
    }

    public static bool MultiBlockTransformCrossesVerticalBoundary(JxlAcStrategyImage acStrategyImage, int x, int startY, int endY)
    {
        if (x >= acStrategyImage.XSize || startY >= acStrategyImage.YSize)
        {
            return false;
        }

        if (x % 8 == 0)
        {
            // Nothing crosses 64x64 boundaries, and the memory on the other side
            // of the 64x64 block may still uninitialized.
            return false;
        }

        endY = Math.Min(endY, acStrategyImage.YSize);

        // The first multiblock might be before the start_y, let's adjust it
        // to point to the first IsFirstBlock() == true block we find by backward
        // tracing.
        int startYLimit = startY & ~7;
        while (startY != startYLimit && !acStrategyImage.GetRow(startY)[x].IsFirstBlock)
        {
            startY--;
        }

        for (int y = startY; y < endY;)
        {
            JxlAcStrategyRow row = acStrategyImage.GetRow(y);

            if (row[x].IsFirstBlock)
            {
                y += row[x].CoveredBlocksY;
            }
            else
            {
                return true;
            }
        }

        return false;
    }

    public static JxlAcStrategyType AcsSquare(int blocks)
    {
        if (blocks == 2)
        {
            return JxlAcStrategyType.DCT16X16;
        }
        else if (blocks == 4)
        {
            return JxlAcStrategyType.DCT32X32;
        }
        else
        {
            return JxlAcStrategyType.DCT64X64;
        }
    }

    public static JxlAcStrategyType AcsVerticalSplit(int blocks)
    {
        if (blocks == 2)
        {
            return JxlAcStrategyType.DCT16X8;
        }
        else if (blocks == 4)
        {
            return JxlAcStrategyType.DCT32X16;
        }
        else
        {
            return JxlAcStrategyType.DCT64X32;
        }
    }

    public static JxlAcStrategyType AcsHorizontalSplit(int blocks)
    {
        if (blocks == 2)
        {
            return JxlAcStrategyType.DCT8X16;
        }
        else if (blocks == 4)
        {
            return JxlAcStrategyType.DCT16X32;
        }
        else
        {
            return JxlAcStrategyType.DCT32X64;
        }
    }

    public static void SetEntropyForTransform(int cx, int cy, JxlAcStrategyType acsRaw, float entropy, Span<float> entropyEstimate)
    {
        JxlAcStrategy acs = new(acsRaw);

        for (int dy = 0; dy < acs.CoveredBlocksY; dy++)
        {
            for (int dx = 0; dx < acs.CoveredBlocksX; dx++)
            {
                entropyEstimate[((cy + dy) * 8) + cx + dx] = 0.0f;
            }
        }

        entropyEstimate[(cy * 8) + cx] = entropy;
    }

    /// <summary>Selects the lowest-cost transform for one eight-pixel square.</summary>
    /// <param name="x">The source pixel column.</param>
    /// <param name="y">The source pixel row.</param>
    /// <param name="encodingSpeedTier">The encoder search speed tier.</param>
    /// <param name="butteraugliTarget">The requested perceptual distance.</param>
    /// <param name="config">The borrowed source views and search weights.</param>
    /// <param name="matrixStorage">The coefficient tables borrowed before the rectangle search.</param>
    /// <param name="contextMapFactors">The X, Y and B color-correlation factors.</param>
    /// <param name="block">The reusable three-channel coefficient workspace.</param>
    /// <param name="scratchSpace">The reusable error and transform workspace.</param>
    /// <param name="entropyOut">The selected transform's cost.</param>
    /// <param name="bestTx">The selected transform strategy.</param>
    public static void FindBest8x8Transform(
        int x,
        int y,
        int encodingSpeedTier,
        float butteraugliTarget,
        JxlAcStrategyConfiguration config,
        ReadOnlySpan<float> matrixStorage,
        Span<float> contextMapFactors,
        Span<float> block,
        Span<float> scratchSpace,
        out float entropyOut,
        out JxlAcStrategyType bestTx)
    {
        double best = 1e30;
        bestTx = Transforms8x8[0].Type;

        foreach (TransformAttempt8x8 tx in Transforms8x8)
        {
            if (tx.EncodingSpeedTierMaxLimit < encodingSpeedTier)
            {
                // This transform is too slow. User settings
                // indicate higher speeds.
                continue;
            }

            JxlAcStrategy acs = new(tx.Type);
            float entropyMultiplier = (float)(tx.EntropyMultiplier / Transforms8x8[0].EntropyMultiplier);

            if (tx.Type is JxlAcStrategyType.DCT2X2 or JxlAcStrategyType.IDENTITY && butteraugliTarget < 5.0)
            {
                const float favor2X2AtHighQuality = 0.4f;
                float weight = MathF.Pow((5.0f - butteraugliTarget) / 5.0f, 2.0f);
                entropyMultiplier -= favor2X2AtHighQuality * weight;
            }

            if ((tx.Type != JxlAcStrategyType.DCT && tx.Type != JxlAcStrategyType.DCT2X2 && tx.Type != JxlAcStrategyType.IDENTITY) && butteraugliTarget > 4.0)
            {
                const float kAvoidEntropyOfTransforms = 0.5f;
                float mul = 1.0f;

                if (butteraugliTarget < 12.0)
                {
                    mul *= (float)((12.0f - 4.0) / (butteraugliTarget - 4.0f));
                }

                entropyMultiplier += kAvoidEntropyOfTransforms * mul;
            }

            if (!EstimateEntropy(
                acs,
                entropyMultiplier,
                x,
                y,
                config,
                matrixStorage,
                contextMapFactors,
                block,
                scratchSpace,
                out float entropy))
            {
                throw new InvalidOperationException("Failed to estimate entropy");
            }

            if (entropy < best)
            {
                bestTx = tx.Type;
                best = entropy;
            }
        }

        entropyOut = (float)best;
    }

    /// <summary>Replaces an allowed block region when the candidate transform lowers its estimated cost.</summary>
    /// <param name="acsRaw">The candidate strategy.</param>
    /// <param name="bx">The rectangle's frame block column.</param>
    /// <param name="by">The rectangle's frame block row.</param>
    /// <param name="cx">The candidate's relative block column.</param>
    /// <param name="cy">The candidate's relative block row.</param>
    /// <param name="config">The borrowed source views and search weights.</param>
    /// <param name="matrixStorage">The coefficient tables borrowed before the rectangle search.</param>
    /// <param name="cmapFactors">The X, Y and B color-correlation factors.</param>
    /// <param name="acStrategy">The destination strategy map.</param>
    /// <param name="entropyMul">The candidate's fixed cost multiplier.</param>
    /// <param name="candidatePriority">The priority used to prevent overlapping accepted transforms.</param>
    /// <param name="priority">The rectangle's existing block priorities.</param>
    /// <param name="entropyEstimate">The cost stored for each rectangle block.</param>
    /// <param name="block">The reusable three-channel coefficient workspace.</param>
    /// <param name="scratchSpace">The reusable error and transform workspace.</param>
    /// <returns>Whether candidate estimation completed.</returns>
    public static bool TryMergeAcStrategy(
        JxlAcStrategyType acsRaw,
        int bx,
        int by,
        int cx,
        int cy,
        JxlAcStrategyConfiguration config,
        ReadOnlySpan<float> matrixStorage,
        Span<float> cmapFactors,
        JxlAcStrategyImage acStrategy,
        float entropyMul,
        byte candidatePriority,
        Span<byte> priority,
        Span<float> entropyEstimate,
        Span<float> block,
        Span<float> scratchSpace)
    {
        JxlAcStrategy acs = new(acsRaw);
        float currentEntropy = 0;

        for (int iy = 0; iy < acs.CoveredBlocksY; ++iy)
        {
            for (int ix = 0; ix < acs.CoveredBlocksX; ++ix)
            {
                if (priority[((cy + iy) * 8) + (cx + ix)] >= candidatePriority)
                {
                    // Transform would reuse already allocated blocks and
                    // lead to invalid overlaps, for example DCT64X32 vs.
                    // DCT32X64.
                    return true;
                }

                currentEntropy += entropyEstimate[((cy + iy) * 8) + (cx + ix)];
            }
        }

        if (!EstimateEntropy(
            acs,
            entropyMul,
            (bx + cx) * 8,
            (by + cy) * 8,
            config,
            matrixStorage,
            cmapFactors,
            block,
            scratchSpace,
            out float entropyCandidate))
        {
            return false;
        }

        if (entropyCandidate >= currentEntropy)
        {
            return true;
        }

        for (int iy = 0; iy < acs.CoveredBlocksY; iy++)
        {
            for (int ix = 0; ix < acs.CoveredBlocksX; ix++)
            {
                entropyEstimate[((cy + iy) * 8) + cx + ix] = 0;
                priority[((cy + iy) * 8) + cx + ix] = candidatePriority;
            }
        }

        if (!acStrategy.Set(bx + cx, by + cy, acsRaw))
        {
            return false;
        }

        entropyEstimate[(cy * 8) + cx] = entropyCandidate;
        return true;
    }

    /// <summary>
    /// Compares a square transform with its horizontal and vertical divisions and updates the selected strategies.
    /// </summary>
    /// <param name="blocks">The square's side length in eight-pixel blocks.</param>
    /// <param name="allowSquareTransform">Whether the full square transform is a candidate.</param>
    /// <param name="bx">The search rectangle's first block column.</param>
    /// <param name="by">The search rectangle's first block row.</param>
    /// <param name="cx">The square's block column within the search rectangle.</param>
    /// <param name="cy">The square's block row within the search rectangle.</param>
    /// <param name="config">The borrowed source views, matrices and search weights.</param>
    /// <param name="matrixStorage">The complete coefficient tables borrowed before the rectangle search.</param>
    /// <param name="cmapFactors">The three channel correlation factors.</param>
    /// <param name="acStrategy">The selected strategies to update.</param>
    /// <param name="entropyMulJXK">The existing entropy multiplier for divided candidates.</param>
    /// <param name="entropyMulJXJ">The existing entropy multiplier for the square candidate.</param>
    /// <param name="entropyEstimate">The rectangle's reusable per-block entropy estimates.</param>
    /// <param name="block">The borrowed candidate coefficient storage.</param>
    /// <param name="scratchSpace">The borrowed reconstruction-error and transform workspace.</param>
    /// <returns>Whether every required entropy estimate and strategy update succeeded.</returns>
    public static bool FindBestFirstLevelDivisionForSquare(
        int blocks,
        bool allowSquareTransform,
        int bx,
        int by,
        int cx,
        int cy,
        JxlAcStrategyConfiguration config,
        ReadOnlySpan<float> matrixStorage,
        Span<float> cmapFactors,
        JxlAcStrategyImage acStrategy,
        float entropyMulJXK,
        float entropyMulJXJ,
        Span<float> entropyEstimate,
        Span<float> block,
        Span<float> scratchSpace)
    {
        int blocksHalf = blocks / 2;
        JxlAcStrategyType acsRawJXK = AcsVerticalSplit(blocks);
        JxlAcStrategyType acsRawKXJ = AcsHorizontalSplit(blocks);
        JxlAcStrategyType acsRawJXJ = AcsSquare(blocks);
        JxlAcStrategy acsJXK = new(acsRawJXK);
        JxlAcStrategy acsKXJ = new(acsRawKXJ);
        JxlAcStrategy acsJXJ = new(acsRawJXJ);
        JxlAcStrategyRow row0 = acStrategy.GetRow(by + cy + 0);
        JxlAcStrategyRow row1 = acStrategy.GetRow(by + cy + blocksHalf);

        if (MultiBlockTransformCrossesHorizontalBoundary(acStrategy, bx + cx, by + cy, bx + cx + blocks) ||
            MultiBlockTransformCrossesHorizontalBoundary(acStrategy, bx + cx, by + cy + blocks, bx + cx + blocks) ||
            MultiBlockTransformCrossesVerticalBoundary(acStrategy, bx + cx, by + cy, by + cy + blocks) ||
            MultiBlockTransformCrossesVerticalBoundary(acStrategy, bx + cx + blocks, by + cy, by + cy + blocks))
        {
            return true;
        }

        bool allowJXK = !MultiBlockTransformCrossesVerticalBoundary(acStrategy, bx + cx + blocksHalf, by + cy, by + cy + blocks);
        bool allowKXJ = !MultiBlockTransformCrossesHorizontalBoundary(acStrategy, bx + cx, by + cy + blocksHalf, bx + cx + blocks);

        InlineArray2<InlineArray2<float>> entropy = default;

        for (int dy = 0; dy < blocks; ++dy)
        {
            for (int dx = 0; dx < blocks; ++dx)
            {
                entropy[dy / blocksHalf][dx / blocksHalf] += entropyEstimate[((cy + dy) * 8) + (cx + dx)];
            }
        }

        float entropyJXKLeft = float.MaxValue;
        float entropyJXKRight = float.MaxValue;
        float entropyKXJTop = float.MaxValue;
        float entropyKXJBottom = float.MaxValue;
        float entropyJXJ = float.MaxValue;

        if (allowJXK)
        {
            if (row0[bx + cx + 0].Strategy != acsRawJXK)
            {
                if (!EstimateEntropy(
                    acsJXK,
                    entropyMulJXK,
                    (bx + cx + 0) * 8,
                    (by + cy + 0) * 8,
                    config,
                    matrixStorage,
                    cmapFactors,
                    block,
                    scratchSpace,
                    out entropyJXKLeft))
                {
                    return false;
                }
            }

            if (row0[bx + cx + blocksHalf].Strategy != acsRawJXK)
            {
                if (!EstimateEntropy(
                    acsJXK,
                    entropyMulJXK,
                    (bx + cx + blocksHalf) * 8,
                    (by + cy + 0) * 8,
                    config,
                    matrixStorage,
                    cmapFactors,
                    block,
                    scratchSpace,
                    out entropyJXKRight))
                {
                    return false;
                }
            }
        }

        if (allowKXJ)
        {
            if (row0[bx + cx].Strategy != acsRawKXJ)
            {
                if (!EstimateEntropy(
                    acsKXJ,
                    entropyMulJXK,
                    (bx + cx + 0) * 8,
                    (by + cy + 0) * 8,
                    config,
                    matrixStorage,
                    cmapFactors,
                    block,
                    scratchSpace,
                    out entropyKXJTop))
                {
                    return false;
                }
            }

            if (row1[bx + cx].Strategy != acsRawKXJ)
            {
                if (!EstimateEntropy(
                    acsKXJ,
                    entropyMulJXK,
                    (bx + cx + 0) * 8,
                    (by + cy + blocksHalf) * 8,
                    config,
                    matrixStorage,
                    cmapFactors,
                    block,
                    scratchSpace,
                    out entropyKXJBottom))
                {
                    return false;
                }
            }
        }

        if (allowSquareTransform)
        {
            if (!EstimateEntropy(
                acsJXJ,
                entropyMulJXJ,
                (bx + cx + 0) * 8,
                (by + cy + 0) * 8,
                config,
                matrixStorage,
                cmapFactors,
                block,
                scratchSpace,
                out entropyJXJ))
            {
                return false;
            }
        }

        float costJxN = MathF.Min(entropyJXKLeft, entropy[0][0] + entropy[1][0]) +
                        MathF.Min(entropyJXKRight, entropy[0][1] + entropy[1][1]);

        float costNxJ = MathF.Min(entropyKXJTop, entropy[0][0] + entropy[0][1]) +
                        MathF.Min(entropyKXJBottom, entropy[1][0] + entropy[1][1]);

        if (entropyJXJ < costJxN && entropyJXJ < costNxJ)
        {
            if (!acStrategy.Set(bx + cx, by + cy, acsRawJXJ))
            {
                return false;
            }

            SetEntropyForTransform(cx, cy, acsRawJXJ, entropyJXJ, entropyEstimate);
        }
        else if (costJxN < costNxJ)
        {
            if (entropyJXKLeft < entropy[0][0] + entropy[1][0])
            {
                if (!acStrategy.Set(bx + cx, by + cy, acsRawJXK))
                {
                    return false;
                }

                SetEntropyForTransform(cx, cy, acsRawJXK, entropyJXKLeft, entropyEstimate);
            }

            if (entropyJXKRight < entropy[0][1] + entropy[1][1])
            {
                if (!acStrategy.Set(bx + cx + blocksHalf, by + cy, acsRawJXK))
                {
                    return false;
                }

                SetEntropyForTransform(cx + blocksHalf, cy, acsRawJXK, entropyJXKRight, entropyEstimate);
            }
        }
        else
        {
            if (entropyKXJTop < entropy[0][0] + entropy[0][1])
            {
                if (!acStrategy.Set(bx + cx, by + cy, acsRawKXJ))
                {
                    return false;
                }

                SetEntropyForTransform(cx, cy, acsRawKXJ, entropyKXJTop, entropyEstimate);
            }

            if (entropyKXJBottom < entropy[1][0] + entropy[1][1])
            {
                if (!acStrategy.Set(bx + cx, by + cy + blocksHalf, acsRawKXJ))
                {
                    return false;
                }

                SetEntropyForTransform(cx, cy + blocksHalf, acsRawKXJ, entropyKXJBottom, entropyEstimate);
            }
        }

        return true;
    }

    /// <summary>
    /// Selects transform strategies for one rectangle using floating-point entropy and reconstruction estimates.
    /// </summary>
    /// <param name="cparams">The fixed compression settings.</param>
    /// <param name="config">The borrowed source views, matrices and search weights.</param>
    /// <param name="matrixStorage">The complete coefficient tables borrowed before the rectangle search.</param>
    /// <param name="rect">The rectangle in block coordinates, at most eight by eight blocks.</param>
    /// <param name="cmap">The frame's color-correlation maps.</param>
    /// <param name="block">The borrowed float allocation: three coefficient regions followed by error and transform scratch.</param>
    /// <param name="acStrategy">The destination strategy image.</param>
    /// <returns>Whether all strategy updates succeeded.</returns>
    public static bool ProcessAcStrategyRectangle(
        JxlCompressionParameters cparams,
        JxlAcStrategyConfiguration config,
        ReadOnlySpan<float> matrixStorage,
        Rectangle rect,
        JxlColorCorrelationMap cmap,
        Span<float> block,
        JxlAcStrategyImage acStrategy)
    {
        float butteraugliTarget = cparams.ButteraugliDistance;

        // EstimateEntropy places rounded coefficients in float vectors. It needs no separate integer coefficient buffer.
        // The first three maximum-area regions hold channel coefficients; the remaining regions hold error and transform scratch.
        Span<float> scratchSpace = block[(3 * JxlAcStrategy.MaximumCoefficientArea)..];
        int bx = rect.X0();
        int by = rect.Y0();

        if (rect.Width > 8 || rect.Height > 8)
        {
            throw new InvalidOperationException("Rectangle is too large");
        }

        int tx = bx / JxlChromaFromLuma.ColorTileDimensionInBlocks;
        int ty = by / JxlChromaFromLuma.ColorTileDimensionInBlocks;

        InlineArray3<float> cmapFactors = default;
        cmapFactors[0] = cmap.Base.YToXRatio(cmap.YToXMap.GetRow(ty)[tx]);
        cmapFactors[1] = 0f;
        cmapFactors[2] = cmap.Base.YToBRatio(cmap.YToBMap.GetRow(ty)[tx]);

        if (cparams.SpeedTier > JxlSpeedTier.Hare)
        {
            return true;
        }

        Span<float> entropyEstimate = stackalloc float[64];
        entropyEstimate.Clear();

        const float k8x8mul1 = -0.4f;
        const float k8x8mul2 = 1.0f;
        const float k8x8base = 1.4f;
        float mul8x8 = k8x8mul2 + (k8x8mul1 / (butteraugliTarget + k8x8base));

        for (int iy = 0; iy < rect.Height; iy++)
        {
            for (int ix = 0; ix < rect.Width; ix++)
            {
                FindBest8x8Transform(
                    8 * (bx + ix),
                    8 * (by + iy),
                    (int)cparams.SpeedTier,
                    butteraugliTarget,
                    config,
                    matrixStorage,
                    cmapFactors,
                    block,
                    scratchSpace,
                    out float entropy,
                    out JxlAcStrategyType bestOf8x8s);

                if (!acStrategy.Set(bx + ix, by + iy, bestOf8x8s))
                {
                    return false;
                }

                entropyEstimate[(iy * 8) + ix] = entropy * mul8x8;
            }
        }

        const float entropy_mul16X8 = 1.21f;
        const float entropy_mul16X16 = 1.34f;
        const float entropy_mul16X32 = 1.49f;
        const float entropy_mul32X32 = 1.48f;
        const float entropy_mul64X32 = 2.25f;
        const float entropy_mul64X64 = 2.25f;

        Span<MergeAttempt> transformsForMerge =
        [
            new(JxlAcStrategyType.DCT16X8, 2, 4, 5, entropy_mul16X8),
            new(JxlAcStrategyType.DCT8X16, 2, 4, 5, entropy_mul16X8),
            new(JxlAcStrategyType.DCT16X32, 4, 4, 4, entropy_mul16X32),
            new(JxlAcStrategyType.DCT32X16, 4, 4, 4, entropy_mul16X32),
            new(JxlAcStrategyType.DCT64X32, 6, 1, 3, entropy_mul64X32),
            new(JxlAcStrategyType.DCT32X64, 6, 1, 3, entropy_mul64X32)
        ];

        Span<byte> priority = stackalloc byte[64];
        priority.Clear();

        bool enable32x32 = cparams.DecodingSpeedTier < 4;
        foreach (MergeAttempt mt in transformsForMerge)
        {
            if (mt.DecodingSpeedTierMaxLimit < cparams.DecodingSpeedTier)
            {
                continue;
            }

            JxlAcStrategy acs = new(mt.Type);

            for (int cy = 0; cy + acs.CoveredBlocksY - 1 < rect.Height; cy += acs.CoveredBlocksY)
            {
                for (int cx = 0; cx + acs.CoveredBlocksX - 1 < rect.Width; cx += acs.CoveredBlocksX)
                {
                    if (cy < rect.Height - 7 && cx < rect.Width - 7)
                    {
                        if (cparams.DecodingSpeedTier < 4 && mt.Type == JxlAcStrategyType.DCT32X64)
                        {
                            if ((cy | cx) % 8 == 0)
                            {
                                if (!FindBestFirstLevelDivisionForSquare(
                                    8,
                                    true,
                                    bx,
                                    by,
                                    cx,
                                    cy,
                                    config,
                                    matrixStorage,
                                    cmapFactors,
                                    acStrategy,
                                    mt.EntropyMultiplier,
                                    entropy_mul64X64,
                                    entropyEstimate,
                                    block,
                                    scratchSpace))
                                {
                                    return false;
                                }
                            }

                            continue;
                        }
                        else if (mt.Type == JxlAcStrategyType.DCT32X16)
                        {
                            continue;
                        }
                    }

                    if ((mt.Type == JxlAcStrategyType.DCT16X32 && cy % 4 != 0) || (mt.Type == JxlAcStrategyType.DCT32X16 && cx % 4 != 0))
                    {
                        continue;
                    }

                    if (cy < rect.Height - 3 && cx < rect.Width - 3)
                    {
                        if (mt.Type == JxlAcStrategyType.DCT16X32)
                        {
                            if ((cy | cx) % 4 == 0)
                            {
                                if (!FindBestFirstLevelDivisionForSquare(
                                    4,
                                    enable32x32,
                                    bx,
                                    by,
                                    cx,
                                    cy,
                                    config,
                                    matrixStorage,
                                    cmapFactors,
                                    acStrategy,
                                    mt.EntropyMultiplier,
                                    entropy_mul32X32,
                                    entropyEstimate,
                                    block,
                                    scratchSpace))
                                {
                                    return false;
                                }
                            }

                            continue;
                        }
                        else if (mt.Type == JxlAcStrategyType.DCT32X16)
                        {
                            continue;
                        }
                    }

                    if ((mt.Type == JxlAcStrategyType.DCT16X32 && cy % 4 != 0) ||
                        (mt.Type == JxlAcStrategyType.DCT32X16 && cx % 4 != 0))
                    {
                        continue;
                    }

                    if (cy < rect.Height - 1 && cx < rect.Width - 1)
                    {
                        if (mt.Type == JxlAcStrategyType.DCT8X16)
                        {
                            if ((cy | cx) % 2 == 0)
                            {
                                if (!FindBestFirstLevelDivisionForSquare(
                                    2,
                                    true,
                                    bx,
                                    by,
                                    cx,
                                    cy,
                                    config,
                                    matrixStorage,
                                    cmapFactors,
                                    acStrategy,
                                    mt.EntropyMultiplier,
                                    entropy_mul16X16,
                                    entropyEstimate,
                                    block,
                                    scratchSpace))
                                {
                                    return false;
                                }
                            }

                            continue;
                        }
                        else if (mt.Type == JxlAcStrategyType.DCT16X8)
                        {
                            continue;
                        }
                    }

                    if ((mt.Type == JxlAcStrategyType.DCT8X16 && cy % 2 == 1) ||
                        (mt.Type == JxlAcStrategyType.DCT16X8 && cx % 2 == 1))
                    {
                        continue;
                    }

                    if (!TryMergeAcStrategy(
                        mt.Type,
                        bx,
                        by,
                        cx,
                        cy,
                        config,
                        matrixStorage,
                        cmapFactors,
                        acStrategy,
                        mt.EntropyMultiplier,
                        mt.Priority,
                        priority,
                        entropyEstimate,
                        block,
                        scratchSpace))
                    {
                        return false;
                    }
                }
            }
        }

        if (cparams.SpeedTier >= JxlSpeedTier.Hare)
        {
            return true;
        }

        for (int cy = 0; cy + 1 < rect.Height; ++cy)
        {
            for (int cx = 0; cx + 1 < rect.Width; ++cx)
            {
                if ((cy | cx) % 2 != 0)
                {
                    if (!FindBestFirstLevelDivisionForSquare(
                        2,
                        true,
                        bx,
                        by,
                        cx,
                        cy,
                        config,
                        matrixStorage,
                        cmapFactors,
                        acStrategy,
                        entropy_mul16X8,
                        entropy_mul16X16,
                        entropyEstimate,
                        block,
                        scratchSpace))
                    {
                        return false;
                    }
                }
            }
        }

        int step = cparams.SpeedTier >= JxlSpeedTier.Tortoise ? 2 : 1;

        for (int cy = 0; cy < rect.Height - 3; cy += step)
        {
            for (int cx = 0; cx < rect.Width - 3; cx += step)
            {
                if ((cy | cx) % 4 == 0)
                {
                    continue;
                }

                if (!FindBestFirstLevelDivisionForSquare(
                    4,
                    enable32x32,
                    bx,
                    by,
                    cx,
                    cy,
                    config,
                    matrixStorage,
                    cmapFactors,
                    acStrategy,
                    entropy_mul16X32,
                    entropy_mul32X32,
                    entropyEstimate,
                    block,
                    scratchSpace))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Describes one eight-by-eight transform candidate and its fixed search weights.
    /// </summary>
    private readonly struct TransformAttempt8x8
    {
        /// <summary>
        /// Initializes a candidate with the existing native search settings.
        /// </summary>
        /// <param name="type">The candidate transform.</param>
        /// <param name="encodingSpeedTierMaxLimit">The largest encoding speed tier that considers this candidate.</param>
        /// <param name="entropyMultiplier">The native entropy weight before normalization against the DCT candidate.</param>
        public TransformAttempt8x8(JxlAcStrategyType type, int encodingSpeedTierMaxLimit, double entropyMultiplier)
        {
            this.Type = type;
            this.EncodingSpeedTierMaxLimit = encodingSpeedTierMaxLimit;
            this.EntropyMultiplier = entropyMultiplier;
        }

        /// <summary>
        /// Gets the candidate transform.
        /// </summary>
        public JxlAcStrategyType Type { get; }

        /// <summary>
        /// Gets the largest encoding speed tier that considers this candidate.
        /// </summary>
        public int EncodingSpeedTierMaxLimit { get; }

        /// <summary>
        /// Gets the native entropy weight before normalization against the DCT candidate.
        /// </summary>
        public double EntropyMultiplier { get; }
    }

    /// <summary>
    /// Describes one multiblock merge candidate in the existing sequential search.
    /// </summary>
    private readonly struct MergeAttempt
    {
        /// <summary>
        /// Initializes a candidate with the existing native search settings.
        /// </summary>
        /// <param name="type">The candidate transform.</param>
        /// <param name="priority">The priority used to prevent competing merges from overlapping.</param>
        /// <param name="decodingSpeedTierMaxLimit">The largest decoding speed tier that considers this candidate.</param>
        /// <param name="encodingSpeedTierMaxLimit">The encoding speed limit stored in the native candidate table.</param>
        /// <param name="entropyMultiplier">The multiplier applied to the estimated entropy.</param>
        public MergeAttempt(JxlAcStrategyType type, byte priority, byte decodingSpeedTierMaxLimit, byte encodingSpeedTierMaxLimit, float entropyMultiplier)
        {
            this.Type = type;
            this.Priority = priority;
            this.DecodingSpeedTierMaxLimit = decodingSpeedTierMaxLimit;
            this.EncodingSpeedTierMaxLimit = encodingSpeedTierMaxLimit;
            this.EntropyMultiplier = entropyMultiplier;
        }

        /// <summary>
        /// Gets the candidate transform.
        /// </summary>
        public JxlAcStrategyType Type { get; }

        /// <summary>
        /// Gets the priority used to prevent competing merges from overlapping.
        /// </summary>
        public byte Priority { get; }

        /// <summary>
        /// Gets the largest decoding speed tier that considers this candidate.
        /// </summary>
        public byte DecodingSpeedTierMaxLimit { get; }

        /// <summary>
        /// Gets the encoding speed limit stored in the native candidate table.
        /// </summary>
        public byte EncodingSpeedTierMaxLimit { get; }

        /// <summary>
        /// Gets the multiplier applied to the estimated entropy.
        /// </summary>
        public float EntropyMultiplier { get; }
    }
}
