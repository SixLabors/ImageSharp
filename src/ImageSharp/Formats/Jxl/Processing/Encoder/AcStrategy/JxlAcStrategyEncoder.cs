// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
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
        Span<uint> quantized,
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
        Span<uint> quantized,
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
                quantized,
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
}
