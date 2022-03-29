// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Formats.Jpeg.Components;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;
using SixLabors.ImageSharp.Tests.Formats.Jpg.Utils;
using SixLabors.ImageSharp.Tests.TestUtilities;
using Xunit;
using Xunit.Abstractions;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    [Trait("Format", "Jpg")]
    public static class DCTTests
    {
        // size of input values is 10 bit max
        private const float MaxInputValue = 1023;
        private const float MinInputValue = -1024;

        // output value range is 12 bit max
        private const float MaxOutputValue = 4096;
        private const float NormalizationValue = MaxOutputValue / 2;

        internal static Block8x8F CreateBlockFromScalar(float value)
        {
            Block8x8F result = default;
            for (int i = 0; i < Block8x8F.Size; i++)
            {
                result[i] = value;
            }

            return result;
        }

        public class FastFloatingPoint : JpegFixture
        {
            public FastFloatingPoint(ITestOutputHelper output)
                : base(output)
            {
            }

            [Theory]
            [InlineData(1)]
            [InlineData(2)]
            [InlineData(3)]
            public void LLM_TransformIDCT_CompareToNonOptimized(int seed)
            {
                float[] sourceArray = Create8x8RandomFloatData(MinInputValue, MaxInputValue, seed);

                var srcBlock = Block8x8F.Load(sourceArray);

                // reference
                Block8x8F expected = ReferenceImplementations.LLM_FloatingPoint_DCT.TransformIDCT(ref srcBlock);

                // testee
                // Part of the IDCT calculations is fused into the quantization step
                // We must multiply input block with adjusted no-quantization matrix
                // before applying IDCT
                // Dequantization using unit matrix - no values are upscaled
                Block8x8F dequantMatrix = CreateBlockFromScalar(1);

                // This step is needed to apply adjusting multipliers to the input block
                FastFloatingPointDCT.AdjustToIDCT(ref dequantMatrix);

                // IDCT implementation tranforms blocks after transposition
                srcBlock.TransposeInplace();
                srcBlock.MultiplyInPlace(ref dequantMatrix);

                // IDCT calculation
                FastFloatingPointDCT.TransformIDCT(ref srcBlock);

                this.CompareBlocks(expected, srcBlock, 1f);
            }

            [Theory]
            [InlineData(1)]
            [InlineData(2)]
            [InlineData(3)]
            public void LLM_TransformIDCT_CompareToAccurate(int seed)
            {
                float[] sourceArray = Create8x8RandomFloatData(MinInputValue, MaxInputValue, seed);

                var srcBlock = Block8x8F.Load(sourceArray);

                // reference
                Block8x8F expected = ReferenceImplementations.AccurateDCT.TransformIDCT(ref srcBlock);

                // testee
                // Part of the IDCT calculations is fused into the quantization step
                // We must multiply input block with adjusted no-quantization matrix
                // before applying IDCT
                // Dequantization using unit matrix - no values are upscaled
                Block8x8F dequantMatrix = CreateBlockFromScalar(1);

                // This step is needed to apply adjusting multipliers to the input block
                FastFloatingPointDCT.AdjustToIDCT(ref dequantMatrix);

                // IDCT implementation tranforms blocks after transposition
                srcBlock.TransposeInplace();
                srcBlock.MultiplyInPlace(ref dequantMatrix);

                // IDCT calculation
                FastFloatingPointDCT.TransformIDCT(ref srcBlock);

                this.CompareBlocks(expected, srcBlock, 1f);
            }

            // Inverse transform
            // This test covers entire IDCT conversion chain
            // This test checks all hardware implementations
            [Theory]
            [InlineData(1)]
            [InlineData(2)]
            public void TransformIDCT(int seed)
            {
                static void RunTest(string serialized)
                {
                    int seed = FeatureTestRunner.Deserialize<int>(serialized);

                    Span<float> src = Create8x8RandomFloatData(MinInputValue, MaxInputValue, seed);
                    var srcBlock = default(Block8x8F);
                    srcBlock.LoadFrom(src);

                    float[] expectedDest = new float[64];
                    float[] temp = new float[64];

                    // reference
                    ReferenceImplementations.LLM_FloatingPoint_DCT.IDCT2D_llm(src, expectedDest, temp);

                    // testee
                    // Part of the IDCT calculations is fused into the quantization step
                    // We must multiply input block with adjusted no-quantization matrix
                    // before applying IDCT
                    Block8x8F dequantMatrix = CreateBlockFromScalar(1);

                    // Dequantization using unit matrix - no values are upscaled
                    // as quant matrix is all 1's
                    // This step is needed to apply adjusting multipliers to the input block
                    FastFloatingPointDCT.AdjustToIDCT(ref dequantMatrix);
                    srcBlock.MultiplyInPlace(ref dequantMatrix);

                    // testee
                    // IDCT implementation tranforms blocks after transposition
                    srcBlock.TransposeInplace();
                    FastFloatingPointDCT.TransformIDCT(ref srcBlock);

                    float[] actualDest = srcBlock.ToArray();

                    Assert.Equal(actualDest, expectedDest, new ApproximateFloatComparer(1f));
                }

                // 4 paths:
                // 1. AllowAll - call avx/fma implementation
                // 2. DisableFMA - call avx without fma implementation
                // 3. DisableAvx - call sse implementation
                // 4. DisableSIMD - call Vector4 fallback implementation
                FeatureTestRunner.RunWithHwIntrinsicsFeature(
                    RunTest,
                    seed,
                    HwIntrinsics.AllowAll | HwIntrinsics.DisableFMA | HwIntrinsics.DisableAVX | HwIntrinsics.DisableSIMD);
            }

            //[Theory]
            //[InlineData(1)]
            //[InlineData(2)]
            //public void TranformIDCT_4x4(int seed)
            //{
            //    Span<float> src = Create8x8RandomFloatData(MinInputValue, MaxInputValue, seed, 4, 4);
            //    var srcBlock = default(Block8x8F);
            //    srcBlock.LoadFrom(src);

            //    float[] expectedDest = new float[64];
            //    float[] temp = new float[64];

            //    // reference
            //    ReferenceImplementations.LLM_FloatingPoint_DCT.IDCT2D_llm(src, expectedDest, temp);

            //    // testee
            //    // Part of the IDCT calculations is fused into the quantization step
            //    // We must multiply input block with adjusted no-quantization matrix
            //    // before applying IDCT
            //    Block8x8F dequantMatrix = CreateBlockFromScalar(1);

            //    // Dequantization using unit matrix - no values are upscaled
            //    // as quant matrix is all 1's
            //    // This step is needed to apply adjusting multipliers to the input block
            //    FastFloatingPointDCT.AdjustToIDCT(ref dequantMatrix);

            //    // testee
            //    // IDCT implementation tranforms blocks after transposition
            //    srcBlock.TransposeInplace();
            //    DownScalingComponentProcessor2.TransformIDCT_4x4(ref srcBlock, ref dequantMatrix, NormalizationValue, MaxOutputValue);

            //    var comparer = new ApproximateFloatComparer(1f);

            //    Span<float> expectedSpan = expectedDest.AsSpan();
            //    Span<float> actualSpan = srcBlock.ToArray().AsSpan();

            //    AssertEquality(expectedSpan, actualSpan, comparer);
            //    AssertEquality(expectedSpan.Slice(8), actualSpan.Slice(8), comparer);
            //    AssertEquality(expectedSpan.Slice(16), actualSpan.Slice(16), comparer);
            //    AssertEquality(expectedSpan.Slice(24), actualSpan.Slice(24), comparer);

            //    static void AssertEquality(Span<float> expected, Span<float> actual, ApproximateFloatComparer comparer)
            //    {
            //        for (int x = 0; x < 4; x++)
            //        {
            //            float expectedValue = (float)Math.Round(Numerics.Clamp(expected[x] + NormalizationValue, 0, MaxOutputValue));
            //            Assert.Equal(expectedValue, actual[x], comparer);
            //        }
            //    }
            //}

            [Theory]
            [InlineData(1)]
            [InlineData(2)]
            public void TranformIDCT_2x2(int seed)
            {
                Span<float> src = Create8x8RandomFloatData(MinInputValue, MaxInputValue, seed, 2, 2);
                var srcBlock = default(Block8x8F);
                srcBlock.LoadFrom(src);

                float[] expectedDest = new float[64];
                float[] temp = new float[64];

                // reference
                ReferenceImplementations.LLM_FloatingPoint_DCT.IDCT2D_llm(src, expectedDest, temp);

                // testee
                // Part of the IDCT calculations is fused into the quantization step
                // We must multiply input block with adjusted no-quantization matrix
                // before applying IDCT
                Block8x8F dequantMatrix = CreateBlockFromScalar(1);

                // Dequantization using unit matrix - no values are upscaled
                // as quant matrix is all 1's
                // This step is needed to apply adjusting multipliers to the input block
                FastFloatingPointDCT.AdjustToIDCT(ref dequantMatrix);

                // testee
                // IDCT implementation tranforms blocks after transposition
                srcBlock.TransposeInplace();
                DownScalingComponentProcessor4.TransformIDCT_2x2(ref srcBlock, ref dequantMatrix, NormalizationValue, MaxOutputValue);

                var comparer = new ApproximateFloatComparer(0.1f);

                // top-left
                float topLeftExpected = (float)Math.Round(Numerics.Clamp(expectedDest[0] + NormalizationValue, 0, MaxOutputValue));
                Assert.Equal(topLeftExpected, srcBlock[0], comparer);

                // top-right
                float topRightExpected = (float)Math.Round(Numerics.Clamp(expectedDest[7] + NormalizationValue, 0, MaxOutputValue));
                Assert.Equal(topRightExpected, srcBlock[1], comparer);

                // bot-left
                float botLeftExpected = (float)Math.Round(Numerics.Clamp(expectedDest[56] + NormalizationValue, 0, MaxOutputValue));
                Assert.Equal(botLeftExpected, srcBlock[8], comparer);

                // bot-right
                float botRightExpected = (float)Math.Round(Numerics.Clamp(expectedDest[63] + NormalizationValue, 0, MaxOutputValue));
                Assert.Equal(botRightExpected, srcBlock[9], comparer);
            }

            [Theory]
            [InlineData(1)]
            [InlineData(2)]
            public void TranformIDCT_1x1(int seed)
            {
                Span<float> src = Create8x8RandomFloatData(MinInputValue, MaxInputValue, seed, 1, 1);
                var srcBlock = default(Block8x8F);
                srcBlock.LoadFrom(src);

                float[] expectedDest = new float[64];
                float[] temp = new float[64];

                // reference
                ReferenceImplementations.LLM_FloatingPoint_DCT.IDCT2D_llm(src, expectedDest, temp);

                // testee
                // Part of the IDCT calculations is fused into the quantization step
                // We must multiply input block with adjusted no-quantization matrix
                // before applying IDCT
                Block8x8F dequantMatrix = CreateBlockFromScalar(1);

                // Dequantization using unit matrix - no values are upscaled
                // as quant matrix is all 1's
                // This step is needed to apply adjusting multipliers to the input block
                FastFloatingPointDCT.AdjustToIDCT(ref dequantMatrix);

                // testee
                // IDCT implementation tranforms blocks after transposition
                srcBlock.TransposeInplace();
                float actual = DownScalingComponentProcessor8.TransformIDCT_1x1(
                    srcBlock[0],
                    dequantMatrix[0],
                    NormalizationValue,
                    MaxOutputValue);

                float expected = (float)Math.Round(Numerics.Clamp(expectedDest[0] + NormalizationValue, 0, MaxOutputValue));

                Assert.Equal(actual, expected, new ApproximateFloatComparer(0.1f));
            }

            // Forward transform
            // This test covers entire FDCT conversion chain
            // This test checks all hardware implementations
            [Theory]
            [InlineData(1)]
            [InlineData(2)]
            public void TransformFDCT(int seed)
            {
                static void RunTest(string serialized)
                {
                    int seed = FeatureTestRunner.Deserialize<int>(serialized);

                    Span<float> src = Create8x8RandomFloatData(MinInputValue, MaxInputValue, seed);
                    var block = default(Block8x8F);
                    block.LoadFrom(src);

                    float[] expectedDest = new float[64];
                    float[] temp1 = new float[64];

                    // reference
                    ReferenceImplementations.LLM_FloatingPoint_DCT.FDCT2D_llm(src, expectedDest, temp1, downscaleBy8: true);

                    // testee
                    // Second transpose call is done by Quantize step
                    // Do this manually here just to be complient to the reference implementation
                    FastFloatingPointDCT.TransformFDCT(ref block);
                    block.TransposeInplace();

                    // Part of the IDCT calculations is fused into the quantization step
                    // We must multiply input block with adjusted no-quantization matrix
                    // after applying FDCT
                    Block8x8F quantMatrix = CreateBlockFromScalar(1);
                    FastFloatingPointDCT.AdjustToFDCT(ref quantMatrix);
                    block.MultiplyInPlace(ref quantMatrix);

                    float[] actualDest = block.ToArray();

                    Assert.Equal(expectedDest, actualDest, new ApproximateFloatComparer(1f));
                }

                // 4 paths:
                // 1. AllowAll - call avx/fma implementation
                // 2. DisableFMA - call avx without fma implementation
                // 3. DisableAvx - call Vector4 implementation
                // 4. DisableSIMD - call scalar fallback implementation
                FeatureTestRunner.RunWithHwIntrinsicsFeature(
                    RunTest,
                    seed,
                    HwIntrinsics.AllowAll | HwIntrinsics.DisableFMA | HwIntrinsics.DisableAVX | HwIntrinsics.DisableSIMD);
            }
        }
    }
}
