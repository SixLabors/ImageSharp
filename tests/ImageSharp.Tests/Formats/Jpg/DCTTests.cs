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

                var source = Block8x8F.Load(sourceArray);

                Block8x8F expected = ReferenceImplementations.LLM_FloatingPoint_DCT.TransformIDCT(ref source);

                var temp = default(Block8x8F);
                var actual = default(Block8x8F);
                FastFloatingPointDCT.TransformIDCT(ref source, ref actual, ref temp);

                this.CompareBlocks(expected, actual, 1f);
            }

            [Theory]
            [InlineData(1)]
            [InlineData(2)]
            [InlineData(3)]
            public void LLM_TransformIDCT_CompareToAccurate(int seed)
            {
                float[] sourceArray = Create8x8RoundedRandomFloatData(-1000, 1000, seed);

                var source = Block8x8F.Load(sourceArray);

                Block8x8F expected = ReferenceImplementations.AccurateDCT.TransformIDCT(ref source);

                var temp = default(Block8x8F);
                var actual = default(Block8x8F);
                FastFloatingPointDCT.TransformIDCT(ref source, ref actual, ref temp);

                this.CompareBlocks(expected, actual, 1f);
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

            [RuntimeFeatureConditionalTheory(RuntimeFeature.Avx)]
            [InlineData(1)]
            [InlineData(2)]
            public void IDCT8x8_Avx(int seed)
            {
                static void RunTest(string serialized)
                {
                    int seed = FeatureTestRunner.Deserialize<int>(serialized);

                    TestImpl_IDCT8x8(seed);
                }

                FeatureTestRunner.RunWithHwIntrinsicsFeature(
                    RunTest,
                    seed,
                    HwIntrinsics.DisableFMA);
            }

            [RuntimeFeatureConditionalTheory(RuntimeFeature.Avx | RuntimeFeature.Fma)]
            [InlineData(1)]
            [InlineData(2)]
            public void IDCT8x8_Avx_Fma(int seed)
            {
                TestImpl_IDCT8x8(seed);
            }

            private static void TestImpl_IDCT8x8(int seed)
            {
                Span<float> src = Create8x8RoundedRandomFloatData(-200, 200, seed);
                var srcBlock = default(Block8x8F);
                srcBlock.LoadFrom(src);

                var destBlock = default(Block8x8F);

                var expectedDest = new float[64];

                // reference, left part
                ReferenceImplementations.LLM_FloatingPoint_DCT.IDCT2D8x4_32f(src, expectedDest);

                // reference, right part
                ReferenceImplementations.LLM_FloatingPoint_DCT.IDCT2D8x4_32f(src.Slice(4), expectedDest.AsSpan(4));

                // testee, whole 8x8
                FastFloatingPointDCT.IDCT8x8_Avx(ref srcBlock, ref destBlock);

                var actualDest = new float[64];
                destBlock.ScaledCopyTo(actualDest);

                Assert.Equal(actualDest, expectedDest, new ApproximateFloatComparer(1f));
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

                    var destBlock = default(Block8x8F);

                    var expectedDest = new float[64];
                    var temp1 = new float[64];
                    var temp2 = default(Block8x8F);

                    // reference
                    ReferenceImplementations.LLM_FloatingPoint_DCT.IDCT2D_llm(src, expectedDest, temp1);

                    // testee
                    FastFloatingPointDCT.TransformIDCT(ref srcBlock, ref destBlock, ref temp2);

                    var actualDest = new float[64];
                    destBlock.ScaledCopyTo(actualDest);

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
            [Theory]
            [InlineData(1)]
            [InlineData(2)]
            public void FDCT8x4_LeftPart(int seed)
            {
                Span<float> src = Create8x8RoundedRandomFloatData(-200, 200, seed);
                var srcBlock = default(Block8x8F);
                srcBlock.LoadFrom(src);

                var destBlock = default(Block8x8F);

                var expectedDest = new float[64];

                // reference
                ReferenceImplementations.LLM_FloatingPoint_DCT.FDCT2D8x4_32f(src, expectedDest);

                // testee
                FastFloatingPointDCT.FDCT8x4_LeftPart(ref srcBlock, ref destBlock);

                var actualDest = new float[64];
                destBlock.ScaledCopyTo(actualDest);

                Assert.Equal(actualDest, expectedDest, new ApproximateFloatComparer(1f));
            }

            [Theory]
            [InlineData(1)]
            [InlineData(2)]
            public void FDCT8x4_RightPart(int seed)
            {
                Span<float> src = Create8x8RoundedRandomFloatData(-200, 200, seed);
                var srcBlock = default(Block8x8F);
                srcBlock.LoadFrom(src);

                var destBlock = default(Block8x8F);

                var expectedDest = new float[64];

                // reference
                ReferenceImplementations.LLM_FloatingPoint_DCT.FDCT2D8x4_32f(src.Slice(4), expectedDest.AsSpan(4));

                // testee
                FastFloatingPointDCT.FDCT8x4_RightPart(ref srcBlock, ref destBlock);

                var actualDest = new float[64];
                destBlock.ScaledCopyTo(actualDest);

                Assert.Equal(actualDest, expectedDest, new ApproximateFloatComparer(1f));
            }

            [RuntimeFeatureConditionalTheory(RuntimeFeature.Avx)]
            [InlineData(1)]
            [InlineData(2)]
            public void FDCT8x8_Avx(int seed)
            {
                static void RunTest(string serialized)
                {
                    int seed = FeatureTestRunner.Deserialize<int>(serialized);

                    TestImpl_FDCT8x8(seed);
                }

                FeatureTestRunner.RunWithHwIntrinsicsFeature(
                    RunTest,
                    seed,
                    HwIntrinsics.DisableFMA);
            }

            [RuntimeFeatureConditionalTheory(RuntimeFeature.Avx | RuntimeFeature.Fma)]
            [InlineData(1)]
            [InlineData(2)]
            public void FDCT8x8_Avx_Fma(int seed)
            {
                TestImpl_FDCT8x8(seed);
            }

            private static void TestImpl_FDCT8x8(int seed)
            {
                Span<float> src = Create8x8RoundedRandomFloatData(-200, 200, seed);
                var srcBlock = default(Block8x8F);
                srcBlock.LoadFrom(src);

                var destBlock = default(Block8x8F);

                var expectedDest = new float[64];

                // reference, left part
                ReferenceImplementations.LLM_FloatingPoint_DCT.FDCT2D8x4_32f(src, expectedDest);

                // reference, right part
                ReferenceImplementations.LLM_FloatingPoint_DCT.FDCT2D8x4_32f(src.Slice(4), expectedDest.AsSpan(4));

                // testee, whole 8x8
                FastFloatingPointDCT.FDCT8x8_Avx(ref srcBlock, ref destBlock);

                var actualDest = new float[64];
                destBlock.ScaledCopyTo(actualDest);

                Assert.Equal(actualDest, expectedDest, new ApproximateFloatComparer(1f));
            }

            [Theory]
            [InlineData(1)]
            [InlineData(2)]
            public void TransformFDCT(int seed)
            {
                static void RunTest(string serialized)
                {
                    int seed = FeatureTestRunner.Deserialize<int>(serialized);

                    Span<float> src = Create8x8RoundedRandomFloatData(-200, 200, seed);
                    var srcBlock = default(Block8x8F);
                    srcBlock.LoadFrom(src);

                    var destBlock = default(Block8x8F);

                    var expectedDest = new float[64];
                    var temp1 = new float[64];
                    var temp2 = default(Block8x8F);

                    // reference
                    ReferenceImplementations.LLM_FloatingPoint_DCT.FDCT2D_llm(src, expectedDest, temp1, downscaleBy8: true);

                    // testee
                    FastFloatingPointDCT.TransformFDCT(ref srcBlock, ref destBlock, ref temp2, false);

                    var actualDest = new float[64];
                    destBlock.ScaledCopyTo(actualDest);

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
        }
    }
}
