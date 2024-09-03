// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jpeg.Components;
using SixLabors.ImageSharp.Tests.Formats.Jpg.Utils;
using SixLabors.ImageSharp.Tests.TestUtilities;
using Xunit.Abstractions;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Jpg;

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

            Block8x8F srcBlock = Block8x8F.Load(sourceArray);

            // reference
            Block8x8F expected = ReferenceImplementations.LLM_FloatingPoint_DCT.TransformIDCT(ref srcBlock);

            // testee
            // Part of the IDCT calculations is fused into the quantization step
            // We must multiply input block with adjusted no-quantization matrix
            // before applying IDCT
            // Dequantization using unit matrix - no values are upscaled
            Block8x8F dequantMatrix = CreateBlockFromScalar(1);

            // This step is needed to apply adjusting multipliers to the input block
            FloatingPointDCT.AdjustToIDCT(ref dequantMatrix);

            // IDCT implementation tranforms blocks after transposition
            srcBlock.TransposeInplace();
            srcBlock.MultiplyInPlace(ref dequantMatrix);

            // IDCT calculation
            FloatingPointDCT.TransformIDCT(ref srcBlock);

            this.CompareBlocks(expected, srcBlock, 1f);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void LLM_TransformIDCT_CompareToAccurate(int seed)
        {
            float[] sourceArray = Create8x8RandomFloatData(MinInputValue, MaxInputValue, seed);

            Block8x8F srcBlock = Block8x8F.Load(sourceArray);

            // reference
            Block8x8F expected = ReferenceImplementations.AccurateDCT.TransformIDCT(ref srcBlock);

            // testee
            // Part of the IDCT calculations is fused into the quantization step
            // We must multiply input block with adjusted no-quantization matrix
            // before applying IDCT
            // Dequantization using unit matrix - no values are upscaled
            Block8x8F dequantMatrix = CreateBlockFromScalar(1);

            // This step is needed to apply adjusting multipliers to the input block
            FloatingPointDCT.AdjustToIDCT(ref dequantMatrix);

            // IDCT implementation tranforms blocks after transposition
            srcBlock.TransposeInplace();
            srcBlock.MultiplyInPlace(ref dequantMatrix);

            // IDCT calculation
            FloatingPointDCT.TransformIDCT(ref srcBlock);

            this.CompareBlocks(expected, srcBlock, 1f);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public void TransformIDCT(int seed)
        {
            static void RunTest(string serialized)
            {
                int seed = FeatureTestRunner.Deserialize<int>(serialized);

                Span<float> src = Create8x8RandomFloatData(MinInputValue, MaxInputValue, seed);
                Block8x8F srcBlock = Block8x8F.Load(src);

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
                FloatingPointDCT.AdjustToIDCT(ref dequantMatrix);
                srcBlock.MultiplyInPlace(ref dequantMatrix);

                // testee
                // IDCT implementation tranforms blocks after transposition
                srcBlock.TransposeInplace();
                FloatingPointDCT.TransformIDCT(ref srcBlock);

                float[] actualDest = srcBlock.ToArray();

                Assert.Equal(actualDest, expectedDest, new ApproximateFloatComparer(1f));
            }

            // 4 paths:
            // 1. AllowAll - call avx/fma implementation
            // 2. DisableFMA - call avx without fma implementation
            // 3. DisableAvx - call sse implementation
            // 4. DisableHWIntrinsic - call Vector4 fallback implementation
            FeatureTestRunner.RunWithHwIntrinsicsFeature(
                RunTest,
                seed,
                HwIntrinsics.AllowAll | HwIntrinsics.DisableFMA | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public void TranformIDCT_4x4(int seed)
        {
            Span<float> src = Create8x8RandomFloatData(MinInputValue, MaxInputValue, seed, 4, 4);
            Block8x8F srcBlock = Block8x8F.Load(src);

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
            ScaledFloatingPointDCT.AdjustToIDCT(ref dequantMatrix);

            // testee
            // IDCT implementation tranforms blocks after transposition
            srcBlock.TransposeInplace();
            ScaledFloatingPointDCT.TransformIDCT_4x4(ref srcBlock, ref dequantMatrix, NormalizationValue, MaxOutputValue);

            Span<float> expectedSpan = expectedDest.AsSpan();
            Span<float> actualSpan = srcBlock.ToArray().AsSpan();

            // resulting matrix is 4x4
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    AssertScaledElementEquality(expectedSpan.Slice((y * 16) + (x * 2)), actualSpan.Slice((y * 8) + x));
                }
            }

            static void AssertScaledElementEquality(Span<float> expected, Span<float> actual)
            {
                float average2x2 = 0f;
                for (int y = 0; y < 2; y++)
                {
                    int y8 = y * 8;
                    for (int x = 0; x < 2; x++)
                    {
                        float clamped = Numerics.Clamp(expected[y8 + x] + NormalizationValue, 0, MaxOutputValue);
                        average2x2 += clamped;
                    }
                }

                average2x2 = MathF.Round(average2x2 / 4f);

                Assert.Equal((int)average2x2, (int)actual[0]);
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public void TranformIDCT_2x2(int seed)
        {
            Span<float> src = Create8x8RandomFloatData(MinInputValue, MaxInputValue, seed, 2, 2);
            Block8x8F srcBlock = Block8x8F.Load(src);

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
            ScaledFloatingPointDCT.AdjustToIDCT(ref dequantMatrix);

            // testee
            // IDCT implementation tranforms blocks after transposition
            srcBlock.TransposeInplace();
            ScaledFloatingPointDCT.TransformIDCT_2x2(ref srcBlock, ref dequantMatrix, NormalizationValue, MaxOutputValue);

            Span<float> expectedSpan = expectedDest.AsSpan();
            Span<float> actualSpan = srcBlock.ToArray().AsSpan();

            // resulting matrix is 2x2
            for (int y = 0; y < 2; y++)
            {
                for (int x = 0; x < 2; x++)
                {
                    AssertScaledElementEquality(expectedSpan.Slice((y * 32) + (x * 4)), actualSpan.Slice((y * 8) + x));
                }
            }

            static void AssertScaledElementEquality(Span<float> expected, Span<float> actual)
            {
                float average4x4 = 0f;
                for (int y = 0; y < 4; y++)
                {
                    int y8 = y * 8;
                    for (int x = 0; x < 4; x++)
                    {
                        float clamped = Numerics.Clamp(expected[y8 + x] + NormalizationValue, 0, MaxOutputValue);
                        average4x4 += clamped;
                    }
                }

                average4x4 = MathF.Round(average4x4 / 16f);

                Assert.Equal((int)average4x4, (int)actual[0]);
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public void TranformIDCT_1x1(int seed)
        {
            Span<float> src = Create8x8RandomFloatData(MinInputValue, MaxInputValue, seed, 1, 1);
            Block8x8F srcBlock = Block8x8F.Load(src);

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
            ScaledFloatingPointDCT.AdjustToIDCT(ref dequantMatrix);

            // testee
            // IDCT implementation tranforms blocks after transposition
            // But DC lays on main diagonal which is not changed by transposition
            float actual = ScaledFloatingPointDCT.TransformIDCT_1x1(
                srcBlock[0],
                dequantMatrix[0],
                NormalizationValue,
                MaxOutputValue);

            float expected = MathF.Round(Numerics.Clamp(expectedDest[0] + NormalizationValue, 0, MaxOutputValue));

            Assert.Equal((int)actual, (int)expected);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public void TransformFDCT(int seed)
        {
            static void RunTest(string serialized)
            {
                int seed = FeatureTestRunner.Deserialize<int>(serialized);

                Span<float> src = Create8x8RandomFloatData(MinInputValue, MaxInputValue, seed);
                Block8x8F block = Block8x8F.Load(src);

                float[] expectedDest = new float[64];
                float[] temp1 = new float[64];

                // reference
                ReferenceImplementations.LLM_FloatingPoint_DCT.FDCT2D_llm(src, expectedDest, temp1, downscaleBy8: true);

                // testee
                // Second transpose call is done by Quantize step
                // Do this manually here just to be complient to the reference implementation
                FloatingPointDCT.TransformFDCT(ref block);
                block.TransposeInplace();

                // Part of the IDCT calculations is fused into the quantization step
                // We must multiply input block with adjusted no-quantization matrix
                // after applying FDCT
                Block8x8F quantMatrix = CreateBlockFromScalar(1);
                FloatingPointDCT.AdjustToFDCT(ref quantMatrix);
                block.MultiplyInPlace(ref quantMatrix);

                float[] actualDest = block.ToArray();

                Assert.Equal(expectedDest, actualDest, new ApproximateFloatComparer(1f));
            }

            // 4 paths:
            // 1. AllowAll - call avx/fma implementation
            // 2. DisableFMA - call avx without fma implementation
            // 3. DisableAvx - call Vector4 implementation
            // 4. DisableHWIntrinsic - call scalar fallback implementation
            FeatureTestRunner.RunWithHwIntrinsicsFeature(
                RunTest,
                seed,
                HwIntrinsics.AllowAll | HwIntrinsics.DisableFMA | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
        }
    }
}
