// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

using SixLabors.ImageSharp.Formats.Jpeg.Components;
using SixLabors.ImageSharp.Tests.Formats.Jpg.Utils;

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

            [Fact]
            public void IDCT2D8x4_LeftPart()
            {
                float[] sourceArray = Create8x8FloatData();
                var expectedDestArray = new float[64];

                ReferenceImplementations.LLM_FloatingPoint_DCT.IDCT2D8x4_32f(sourceArray, expectedDestArray);

                var source = default(Block8x8F);
                source.LoadFrom(sourceArray);

                var dest = default(Block8x8F);

                FastFloatingPointDCT.IDCT8x4_LeftPart(ref source, ref dest);

                var actualDestArray = new float[64];
                dest.ScaledCopyTo(actualDestArray);

                this.Print8x8Data(expectedDestArray);
                this.Output.WriteLine("**************");
                this.Print8x8Data(actualDestArray);

                Assert.Equal(expectedDestArray, actualDestArray);
            }

            [Fact]
            public void IDCT2D8x4_RightPart()
            {
                float[] sourceArray = Create8x8FloatData();
                var expectedDestArray = new float[64];

                ReferenceImplementations.LLM_FloatingPoint_DCT.IDCT2D8x4_32f(sourceArray.AsSpan(4), expectedDestArray.AsSpan(4));

                var source = default(Block8x8F);
                source.LoadFrom(sourceArray);

                var dest = default(Block8x8F);

                FastFloatingPointDCT.IDCT8x4_RightPart(ref source, ref dest);

                var actualDestArray = new float[64];
                dest.ScaledCopyTo(actualDestArray);

                this.Print8x8Data(expectedDestArray);
                this.Output.WriteLine("**************");
                this.Print8x8Data(actualDestArray);

                Assert.Equal(expectedDestArray, actualDestArray);
            }

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

                ReferenceImplementations.LLM_FloatingPoint_DCT.FDCT2D8x4_32f(src, expectedDest);
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

                ReferenceImplementations.LLM_FloatingPoint_DCT.FDCT2D8x4_32f(src.Slice(4), expectedDest.AsSpan(4));
                FastFloatingPointDCT.FDCT8x4_RightPart(ref srcBlock, ref destBlock);

                var actualDest = new float[64];
                destBlock.ScaledCopyTo(actualDest);

                Assert.Equal(actualDest, expectedDest, new ApproximateFloatComparer(1f));
            }

            [Theory]
            [InlineData(1)]
            [InlineData(2)]
            public void TransformFDCT(int seed)
            {
                Span<float> src = Create8x8RoundedRandomFloatData(-200, 200, seed);
                var srcBlock = default(Block8x8F);
                srcBlock.LoadFrom(src);

                var destBlock = default(Block8x8F);

                var expectedDest = new float[64];
                var temp1 = new float[64];
                var temp2 = default(Block8x8F);

                ReferenceImplementations.LLM_FloatingPoint_DCT.FDCT2D_llm(src, expectedDest, temp1, downscaleBy8: true);
                FastFloatingPointDCT.TransformFDCT(ref srcBlock, ref destBlock, ref temp2, false);

                var actualDest = new float[64];
                destBlock.ScaledCopyTo(actualDest);

                Assert.Equal(actualDest, expectedDest, new ApproximateFloatComparer(1f));
            }
        }
    }
}
