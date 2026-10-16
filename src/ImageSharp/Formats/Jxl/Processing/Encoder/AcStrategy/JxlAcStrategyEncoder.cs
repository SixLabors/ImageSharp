// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.AcStrategy;

internal static class JxlAcStrategyEncoder
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

    public static bool EstimateEntropy(
        JxlAcStrategy acs,
        float entropyMultiplier,
        int x,
        int y,
        JxlAcStrategyConfiguration config,
        Span<float> cmapFactors,
        Span<float> block,
        Span<float> fullScratchSpace,
        out float entropy)
    {
        entropy = 0;
        Span<float> memory = fullScratchSpace;
        Span<float> scratchSpace = fullScratchSpace[JxlAcStrategy.MaximumCoefficientArea..];
        int size = (1 << acs.Log2CoveredBlocks) * JxlFrameDimensions.DctBlockSize;

        for (int c = 0; c < 3; c++)
        {
            Span<float> blockC = block[(size * c)..];
            JxlTransformsEncoder.TransformFromPixels(
                acs.Strategy,
                config.PixelSpan(c, x, y),
                config.SourceStride,
                blockC,
                scratchSpace);
        }

        int numBlocks = acs.CoveredBlocksX * acs.CoveredBlocksY;
        float quantNorm16 = 0;

        if (numBlocks == 1)
        {
            // There's only one 8x8. We don't need aggregation of values.
            quantNorm16 = config.Quant(x / 8, y / 8);
        }
        else if (numBlocks == 2)
        {
            if (acs.CoveredBlocksY == 2)
            {
                quantNorm16 = Math.Max(config.Quant(x / 8, y / 8), config.Quant(x / 8, (y / 8) + 1));
            }
            else
            {
                quantNorm16 = Math.Max(config.Quant(x / 8, y / 8), config.Quant((x / 8) + 1, y / 8));
            }
        }
        else
        {
            // Load quantizer field value, calculate empirical heuristic on masking field
            // for weighting the information loss. Information loss manifests
            // itself as ringing, and masking could hide it.
            for (int iy = 0; iy < acs.CoveredBlocksY; iy++)
            {
                int yPos = (y / 8) + iy;

                for (int ix = 0; ix < acs.CoveredBlocksX; ix++)
                {
                    float qval = config.Quant((x / 8) + ix, yPos);
                    qval *= qval;
                    qval *= qval;
                    qval *= qval;
                    quantNorm16 += qval * qval;
                }
            }

            quantNorm16 /= numBlocks;
            quantNorm16 = MathF.Pow(quantNorm16, 1.0f / 16.0f);
        }

        // Computation of entropy.
        // We have to process 8 coeffs at once. Not sure if we can use
        // Vector<T> here...
        Vector256<float> quant = Vector256.Create(quantNorm16);
        Vector256<float> loss = Vector256<float>.Zero;
        for (int c = 0; c < 3; c++)
        {
            Span<float> inverseMatrix = config.Dequant.GetInverseMatrix(acs.Strategy, c);
            Span<float> matrix = config.Dequant.GetMatrix(acs.Strategy, c);
            Vector256<float> cmapFactor = Vector256.Create(cmapFactors[c]);

            Vector256<float> entropyV = Vector256<float>.Zero;
            Vector256<float> numZerosV = Vector256<float>.Zero;

            for (int i = 0; i < (numBlocks * JxlFrameDimensions.DctBlockSize) - Vector<float>.Count; i += Vector<float>.Count)
            {
                Vector256<float> input = Vector256.Create<float>(block[((c * size) + i)..]);
                Vector256<float> inY = Vector256.Create<float>(block[(size + i)..]) * cmapFactor;
                Vector256<float> im = Vector256.Create<float>(inverseMatrix[i..]);
                Vector256<float> value = (input - inY) * (im * quant);
                Vector256<float> rvalue = Vector256.Round(value);
                Vector256<float> diff = value - rvalue;
                Vector256<float> m = Vector256.Create<float>(matrix[i..]);

                (m * diff).CopyTo(memory[i..]);

                Vector256<float> q = Vector256.Abs(rvalue);
                Vector256<float> qIsZero = Vector256.Equals(q, Vector256<float>.Zero);

                entropyV = Vector256.Sqrt(q) + entropyV;
                numZerosV += Vector256.ConditionalSelect(qIsZero, Vector256<float>.Zero, Vector256<float>.One);
            }

            {
                Span<float> maskULut = [12.0f, 0.0f, 4.0f];
                Vector256<float> maskUOffset = Vector256.Create(maskULut[c]);
                Vector256<float> lossC = Vector256<float>.Zero;

                JxlTransformsDecoder.TransformToPixels(
                    acs.Strategy,
                    memory,
                    block,
                    acs.CoveredBlocksX * 8,
                    scratchSpace);

                for (int iy = 0; iy < acs.CoveredBlocksY; iy++)
                {
                    for (int ix = 0; ix < acs.CoveredBlocksX; ix++)
                    {
                        for (int dy = 0; dy < JxlFrameDimensions.BlockDimensions; ++dy)
                        {
                            // This loop here is expected to be vector size-aligned.
                            // No need for <= N - VectorSize.
                            for (int dx = 0; dx < JxlFrameDimensions.BlockDimensions; dx += Vector256<float>.Count)
                            {
                                Vector256<float> input = Vector256.Create<float>(block[
                                      ((((iy * JxlFrameDimensions.BlockDimensions) + dy) * (acs.CoveredBlocksX * JxlFrameDimensions.BlockDimensions)) + (ix * JxlFrameDimensions.BlockDimensions) + dx)..]);

                                if (x + (ix * 8) + dx + Vector256<float>.Count <= config.Mask1x1Width)
                                {
                                    Vector256<float> masku =
                                        Vector256.LoadUnsafe(config.MaskingReference1x1(
                                            x + (ix * 8) + dx,
                                            y + (iy * 8) + dy)) * maskUOffset;
                                    input = masku * input;
                                    input *= input;
                                    input *= input;
                                    input *= input;
                                    lossC += input;
                                }
                            }
                        }
                    }
                }

                Span<float> channelMultipliers = [
                    MathF.Pow(8.2f, 8.0f),
                    MathF.Pow(1.0f, 8.0f),
                    MathF.Pow(1.03f, 8.0f)
                ];

                lossC *= Vector256.Create(channelMultipliers[c]);
                loss += lossC;
            }

            entropy += config.CostDelta * Vector256.Sum(entropyV);
            int numNZeroes = (int)Vector256.Sum(numZerosV);
            int nbits = JxlMath.CeilLog2Nonzero(numNZeroes + 1) + 1;
            entropy += config.ZerosMultiplier * (JxlMath.CeilLog2Nonzero(nbits + 17) + nbits);

            if (c == 0 && numBlocks >= 2)
            {
                // It is X channel (red-green) and we often see ringing
                // in the large blocks. Let's punish that more here.
                float w = 1.0f + MathF.Max(3.0f, numBlocks / 8.0f);
                entropy *= w;
                loss *= Vector256.Create(w);
            }
        }

        float lossScalar = MathF.Pow(Vector256.Sum(loss) / (numBlocks * JxlFrameDimensions.BlockDimensions), 1.0f / 8.0f) * (numBlocks * JxlFrameDimensions.DctBlockSize) / quantNorm16;

        entropy *= entropyMultiplier;
        entropy += config.InfoLossMultiplier * lossScalar;

        return true;
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

    public static void FindBest8x8Transform(
        int x,
        int y,
        int encodingSpeedTier,
        float butteraugliTarget,
        JxlAcStrategyConfiguration config,
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

    public static bool TryMergeAcStrategy(
        JxlAcStrategyType acsRaw,
        int bx,
        int by,
        int cx,
        int cy,
        JxlAcStrategyConfiguration config,
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

    public static bool FindBestFirstLevelDivisionForSquare(
        int blocks,
        bool allowSquareTransform,
        int bx,
        int by,
        int cx,
        int cy,
        JxlAcStrategyConfiguration config,
        Span<float> cmapFactors,
        JxlAcStrategyImage acStrategy,
        float entropyMulJXK,
        float entropyMulJXJ,
        Span<float> entropyEstimate,
        Span<float> block,
        Span<float> scratchSpace,
        ref int quantized)
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

    public static bool ProcessAcStrategyRectangle(
        JxlCompressionParameters cparams,
        JxlAcStrategyConfiguration config,
        Rectangle rect,
        JxlColorCorrelationMap cmap,
        Span<float> block,
        ref int quantized,
        JxlAcStrategyImage acStrategy)
    {
        float butteraugliTarget = cparams.ButteraugliDistance;
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
        cmapFactors[0] = cmap.Base.YToXRatio(cmap.YToXMap!.GetRow(ty)[tx]);
        cmapFactors[1] = 0f;
        cmapFactors[2] = cmap.Base.YToBRatio(cmap.YToBMap!.GetRow(ty)[tx]);

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
                    cmapFactors,
                    acStrategy,
                    block,
                    scratchSpace,
                    quantized,
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
            new(JxlAcStrategyType.DCT32X64, 6, 1, 3, entropy_mul64X32),
            default,
            default,
            default
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
                    if (cy + 7 < rect.Height && cx + 7 < rect.Width)
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
                                    cmapFactors,
                                    acStrategy,
                                    mt.EntropyMultiplier,
                                    entropy_mul64X64,
                                    entropyEstimate,
                                    block,
                                    scratchSpace,
                                    ref quantized))
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

                    if (cy + 3 < rect.Height && cx + 3 < rect.Width)
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
                                    cmapFactors,
                                    acStrategy,
                                    mt.EntropyMultiplier,
                                    entropy_mul32X32,
                                    entropyEstimate,
                                    block,
                                    scratchSpace,
                                    ref quantized))
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

                    if (cy + 1 < rect.Height && cx + 1 < rect.Width)
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
                                    cmapFactors,
                                    acStrategy,
                                    mt.EntropyMultiplier,
                                    entropy_mul16X16,
                                    entropyEstimate,
                                    block,
                                    scratchSpace,
                                    ref quantized))
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
                        cmapFactors,
                        acStrategy,
                        entropy_mul16X8,
                        entropy_mul16X16,
                        entropyEstimate,
                        block,
                        scratchSpace,
                        ref quantized))
                    {
                        return false;
                    }
                }
            }
        }

        int step = cparams.SpeedTier >= JxlSpeedTier.Tortoise ? 2 : 1;

        for (int cy = 0; cy + 3 < rect.Height; cy += step)
        {
            for (int cx = 0; cx + 3 < rect.Width; cx += step)
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
                    cmapFactors,
                    acStrategy,
                    entropy_mul16X32,
                    entropy_mul32X32,
                    entropyEstimate,
                    block,
                    scratchSpace,
                    ref quantized))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// For <see cref="FindBest8x8Transform"/>, represents a small
    /// model which the encoder uses to try all available 8x8
    /// transforms and see which one encodes best.
    /// </summary>
    /// <param name="Type">
    /// The transform type for this model.
    /// </param>
    /// <param name="EncodingSpeedTierMaxLimit">
    /// If the encoding speed tier is higher (indicating
    /// higher speeds but worse compression) than this
    /// value, the attempt shall be ignored as it'll be
    /// too slow.
    /// </param>
    /// <param name="EntropyMultiplier">
    /// Multiplication for the entropy.
    /// </param>
    private readonly record struct TransformAttempt8x8(
        JxlAcStrategyType Type,
        int EncodingSpeedTierMaxLimit,
        double EntropyMultiplier);

    private readonly record struct MergeAttempt(
        JxlAcStrategyType Type,
        byte Priority,
        byte DecodingSpeedTierMaxLimit,
        byte EncodingSpeedTierMaxLimit,
        float EntropyMultiplier);
}
