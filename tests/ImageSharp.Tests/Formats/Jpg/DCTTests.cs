// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
#if SUPPORTS_RUNTIME_INTRINSICS
using System.Runtime.Intrinsics.X86;
#endif
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
        public class FastFloatingPoint : JpegFixture
        {
            public FastFloatingPoint(ITestOutputHelper output)
                : base(output)
            {
            }

            // Reference tests
            [Theory]
            [InlineData(1)]
            [InlineData(2)]
            [InlineData(3)]
            public void LLM_TransformIDCT_CompareToNonOptimized(int seed)
            {
                float[] sourceArray = Create8x8RoundedRandomFloatData(-1000, 1000, seed);

                var srcBlock = Block8x8F.Load(sourceArray);

                Block8x8F expected = ReferenceImplementations.LLM_FloatingPoint_DCT.TransformIDCT(ref srcBlock);

                var temp = default(Block8x8F);
                FastFloatingPointDCT.TransformIDCT(ref srcBlock, ref temp);

                this.CompareBlocks(expected, srcBlock, 1f);
            }

            [Theory]
            [InlineData(1)]
            [InlineData(2)]
            [InlineData(3)]
            public void LLM_TransformIDCT_CompareToAccurate(int seed)
            {
                float[] sourceArray = Create8x8RoundedRandomFloatData(-1000, 1000, seed);

                var srcBlock = Block8x8F.Load(sourceArray);

                Block8x8F expected = ReferenceImplementations.AccurateDCT.TransformIDCT(ref srcBlock);

                var temp = default(Block8x8F);
                FastFloatingPointDCT.TransformIDCT(ref srcBlock, ref temp);

                this.CompareBlocks(expected, srcBlock, 1f);
            }

            // Inverse transform
            [Theory]
            [InlineData(1)]
            [InlineData(2)]
            public void IDCT8x4_LeftPart(int seed)
            {
                Span<float> src = Create8x8RoundedRandomFloatData(-200, 200, seed);
                var srcBlock = default(Block8x8F);
                srcBlock.LoadFrom(src);

                var destBlock = default(Block8x8F);

                var expectedDest = new float[64];

                // reference
                ReferenceImplementations.LLM_FloatingPoint_DCT.IDCT2D8x4_32f(src, expectedDest);

                // testee
                FastFloatingPointDCT.IDCT8x4_LeftPart(ref srcBlock, ref destBlock);

                var actualDest = new float[64];
                destBlock.ScaledCopyTo(actualDest);

                Assert.Equal(actualDest, expectedDest, new ApproximateFloatComparer(1f));
            }

            [Theory]
            [InlineData(1)]
            [InlineData(2)]
            public void IDCT8x4_RightPart(int seed)
            {
                Span<float> src = Create8x8RoundedRandomFloatData(-200, 200, seed);
                var srcBlock = default(Block8x8F);
                srcBlock.LoadFrom(src);

                var destBlock = default(Block8x8F);

                var expectedDest = new float[64];

                // reference
                ReferenceImplementations.LLM_FloatingPoint_DCT.IDCT2D8x4_32f(src.Slice(4), expectedDest.AsSpan(4));

                // testee
                FastFloatingPointDCT.IDCT8x4_RightPart(ref srcBlock, ref destBlock);

                var actualDest = new float[64];
                destBlock.ScaledCopyTo(actualDest);

                Assert.Equal(actualDest, expectedDest, new ApproximateFloatComparer(1f));
            }

            [Theory]
            [InlineData(1)]
            [InlineData(2)]
            public void IDCT8x8_Avx(int seed)
            {
#if SUPPORTS_RUNTIME_INTRINSICS
                if (!Avx.IsSupported)
                {
                    this.Output.WriteLine("No AVX present, skipping test!");
                }

                Span<float> src = Create8x8RoundedRandomFloatData(-200, 200, seed);
                Block8x8F srcBlock = default;
                srcBlock.LoadFrom(src);

                Block8x8F destBlock = default;

                float[] expectedDest = new float[64];

                // reference, left part
                ReferenceImplementations.LLM_FloatingPoint_DCT.IDCT2D8x4_32f(src, expectedDest);

                // reference, right part
                ReferenceImplementations.LLM_FloatingPoint_DCT.IDCT2D8x4_32f(src.Slice(4), expectedDest.AsSpan(4));

                // testee, whole 8x8
                FastFloatingPointDCT.IDCT8x8_Avx(ref srcBlock, ref destBlock);

                float[] actualDest = new float[64];
                destBlock.ScaledCopyTo(actualDest);

                Assert.Equal(actualDest, expectedDest, new ApproximateFloatComparer(1f));
#endif
            }

            [Theory]
            [InlineData(1)]
            [InlineData(2)]
            public void TransformIDCT(int seed)
            {
                static void RunTest(string serialized)
                {
                    int seed = FeatureTestRunner.Deserialize<int>(serialized);

                    Span<float> src = Create8x8RoundedRandomFloatData(-200, 200, seed);
                    var srcBlock = default(Block8x8F);
                    srcBlock.LoadFrom(src);

                    var expectedDest = new float[64];
                    var temp1 = new float[64];
                    var temp2 = default(Block8x8F);

                    // reference
                    ReferenceImplementations.LLM_FloatingPoint_DCT.IDCT2D_llm(src, expectedDest, temp1);

                    // testee
                    FastFloatingPointDCT.TransformIDCT(ref srcBlock, ref temp2);

                    var actualDest = new float[64];
                    srcBlock.ScaledCopyTo(actualDest);

                    Assert.Equal(actualDest, expectedDest, new ApproximateFloatComparer(1f));
                }

                // 3 paths:
                // 1. AllowAll - call avx/fma implementation
                // 2. DisableFMA - call avx implementation without fma acceleration
                // 3. DisableAvx - call fallback code of Vector4 implementation
                //
                // DisableSSE isn't needed because fallback Vector4 code will compile to either sse or fallback code with same result
                FeatureTestRunner.RunWithHwIntrinsicsFeature(
                    RunTest,
                    seed,
                    HwIntrinsics.AllowAll | HwIntrinsics.DisableFMA | HwIntrinsics.DisableAVX);
            }

            // Forward transform
            // This test covers entire FDCT conversions chain
            // This test checks all implementations: intrinsic and scalar fallback
            [Theory]
            [InlineData(1)]
            [InlineData(2)]
            public void TransformFDCT(int seed)
            {
                static void RunTest(string serialized)
                {
                    int seed = FeatureTestRunner.Deserialize<int>(serialized);

                    Span<float> src = Create8x8RoundedRandomFloatData(-200, 200, seed);
                    var block = default(Block8x8F);
                    block.LoadFrom(src);

                    float[] expectedDest = new float[64];
                    float[] temp1 = new float[64];

                    // reference
                    ReferenceImplementations.LLM_FloatingPoint_DCT.FDCT2D_llm(src, expectedDest, temp1, downscaleBy8: true);

                    // testee
                    // Part of the FDCT calculations is fused into the quantization step
                    // We must multiply transformed block with reciprocal values from FastFloatingPointDCT.ANN_DCT_reciprocalAdjustmen
                    FastFloatingPointDCT.TransformFDCT(ref block);
                    for (int i = 0; i < 64; i++)
                    {
                        block[i] = block[i] * FastFloatingPointDCT.DctReciprocalAdjustmentCoefficients[i];
                    }

                    float[] actualDest = block.ToArray();

                    Assert.Equal(expectedDest, actualDest, new ApproximateFloatComparer(1f));
                }

                // 3 paths:
                // 1. AllowAll - call avx/fma implementation
                // 2. DisableFMA - call avx implementation without fma acceleration
                // 3. DisableAvx - call sse implementation
                // 4. DisableHWIntrinsic - call scalar fallback implementation
                FeatureTestRunner.RunWithHwIntrinsicsFeature(
                    RunTest,
                    seed,
                    HwIntrinsics.AllowAll | HwIntrinsics.DisableFMA | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
            }
        }
    }
}
