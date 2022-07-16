// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using SixLabors.ImageSharp.Formats.Jpeg.Components;
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
        private const int MaxAllowedValue = short.MaxValue;
        private const int MinAllowedValue = short.MinValue;

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
                float[] sourceArray = Create8x8RoundedRandomFloatData(MinAllowedValue, MaxAllowedValue, seed);

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
                float[] sourceArray = Create8x8RoundedRandomFloatData(MinAllowedValue, MaxAllowedValue, seed);

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

                    Span<float> src = Create8x8RoundedRandomFloatData(MinAllowedValue, MaxAllowedValue, seed);
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
                // 4. DisableHWIntrinsic - call Vector4 fallback implementation
                FeatureTestRunner.RunWithHwIntrinsicsFeature(
                    RunTest,
                    seed,
                    HwIntrinsics.AllowAll | HwIntrinsics.DisableFMA | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
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

                    Span<float> src = Create8x8RoundedRandomFloatData(MinAllowedValue, MaxAllowedValue, seed);
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
                // 4. DisableHWIntrinsic - call scalar fallback implementation
                FeatureTestRunner.RunWithHwIntrinsicsFeature(
                    RunTest,
                    seed,
                    HwIntrinsics.AllowAll | HwIntrinsics.DisableFMA | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
            }
        }
    }
}
