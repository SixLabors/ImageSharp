// ReSharper disable InconsistentNaming
using System;

using SixLabors.ImageSharp.Formats.Jpeg.Components;
using SixLabors.ImageSharp.Tests.Formats.Jpg.Utils;

using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    public static class DCTTests
    {
        public class FastFloatingPoint : JpegFixture
        {
            public FastFloatingPoint(ITestOutputHelper output)
                : base(output)
            {
            }

            [Fact]
            public void iDCT2D8x4_LeftPart()
            {
                float[] sourceArray = JpegFixture.Create8x8FloatData();
                float[] expectedDestArray = new float[64];

                ReferenceImplementations.LLM_FloatingPoint_DCT.iDCT2D8x4_32f(sourceArray, expectedDestArray);

                var source = new Block8x8F();
                source.LoadFrom(sourceArray);

                var dest = new Block8x8F();

                FastFloatingPointDCT.IDCT8x4_LeftPart(ref source, ref dest);

                float[] actualDestArray = new float[64];
                dest.CopyTo(actualDestArray);

                this.Print8x8Data(expectedDestArray);
                this.Output.WriteLine("**************");
                this.Print8x8Data(actualDestArray);

                Assert.Equal(expectedDestArray, actualDestArray);
            }

            [Fact]
            public void iDCT2D8x4_RightPart()
            {
                float[] sourceArray = JpegFixture.Create8x8FloatData();
                float[] expectedDestArray = new float[64];

                ReferenceImplementations.LLM_FloatingPoint_DCT.iDCT2D8x4_32f(sourceArray.AsSpan(4), expectedDestArray.AsSpan(4));

                var source = new Block8x8F();
                source.LoadFrom(sourceArray);

                var dest = new Block8x8F();

                FastFloatingPointDCT.IDCT8x4_RightPart(ref source, ref dest);

                float[] actualDestArray = new float[64];
                dest.CopyTo(actualDestArray);

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
                float[] sourceArray = JpegFixture.Create8x8RoundedRandomFloatData(-1000, 1000, seed);

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
                float[] sourceArray = JpegFixture.Create8x8RoundedRandomFloatData(-1000, 1000, seed);

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
                Span<float> src = JpegFixture.Create8x8RoundedRandomFloatData(-200, 200, seed);
                var srcBlock = new Block8x8F();
                srcBlock.LoadFrom(src);

                var destBlock = new Block8x8F();

                float[] expectedDest = new float[64];

                ReferenceImplementations.LLM_FloatingPoint_DCT.fDCT2D8x4_32f(src, expectedDest);
                FastFloatingPointDCT.FDCT8x4_LeftPart(ref srcBlock, ref destBlock);

                float[] actualDest = new float[64];
                destBlock.CopyTo(actualDest);

                Assert.Equal(actualDest, expectedDest, new ApproximateFloatComparer(1f));
            }

            [Theory]
            [InlineData(1)]
            [InlineData(2)]
            public void FDCT8x4_RightPart(int seed)
            {
                Span<float> src = JpegFixture.Create8x8RoundedRandomFloatData(-200, 200, seed);
                var srcBlock = new Block8x8F();
                srcBlock.LoadFrom(src);

                var destBlock = new Block8x8F();

                float[] expectedDest = new float[64];

                ReferenceImplementations.LLM_FloatingPoint_DCT.fDCT2D8x4_32f(src.Slice(4), expectedDest.AsSpan(4));
                FastFloatingPointDCT.FDCT8x4_RightPart(ref srcBlock, ref destBlock);

                float[] actualDest = new float[64];
                destBlock.CopyTo(actualDest);

                Assert.Equal(actualDest, expectedDest, new ApproximateFloatComparer(1f));
            }

            [Theory]
            [InlineData(1)]
            [InlineData(2)]
            public void TransformFDCT(int seed)
            {
                Span<float> src = JpegFixture.Create8x8RoundedRandomFloatData(-200, 200, seed);
                var srcBlock = new Block8x8F();
                srcBlock.LoadFrom(src);

                var destBlock = new Block8x8F();

                float[] expectedDest = new float[64];
                float[] temp1 = new float[64];
                var temp2 = new Block8x8F();

                ReferenceImplementations.LLM_FloatingPoint_DCT.fDCT2D_llm(src, expectedDest, temp1, downscaleBy8: true);
                FastFloatingPointDCT.TransformFDCT(ref srcBlock, ref destBlock, ref temp2, false);

                float[] actualDest = new float[64];
                destBlock.CopyTo(actualDest);

                Assert.Equal(actualDest, expectedDest, new ApproximateFloatComparer(1f));
            }

        }
    }
}