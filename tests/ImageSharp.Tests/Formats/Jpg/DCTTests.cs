// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    using System;

    using SixLabors.ImageSharp.Formats.Jpeg.Common;
    using SixLabors.ImageSharp.Formats.Jpeg.GolangPort.Components;
    using SixLabors.ImageSharp.Tests.Formats.Jpg.Utils;

    using Xunit;
    using Xunit.Abstractions;

    public static class DCTTests
    {
        public class FastFloatingPoint : JpegUtilityTestFixture
        {
            public FastFloatingPoint(ITestOutputHelper output)
                : base(output)
            {
            }


            [Fact]
            public void iDCT2D8x4_LeftPart()
            {
                float[] sourceArray = JpegUtilityTestFixture.Create8x8FloatData();
                float[] expectedDestArray = new float[64];

                ReferenceImplementations.FastFloatingPointDCT.iDCT2D8x4_32f(sourceArray, expectedDestArray);

                Block8x8F source = new Block8x8F();
                source.LoadFrom(sourceArray);

                Block8x8F dest = new Block8x8F();

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
                float[] sourceArray = JpegUtilityTestFixture.Create8x8FloatData();
                float[] expectedDestArray = new float[64];

                ReferenceImplementations.FastFloatingPointDCT.iDCT2D8x4_32f(sourceArray.AsSpan().Slice(4), expectedDestArray.AsSpan().Slice(4));

                Block8x8F source = new Block8x8F();
                source.LoadFrom(sourceArray);

                Block8x8F dest = new Block8x8F();

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
            public void TransformIDCT(int seed)
            {
                Span<float> sourceArray = JpegUtilityTestFixture.Create8x8RoundedRandomFloatData(-200, 200, seed);
                float[] expectedDestArray = new float[64];
                float[] tempArray = new float[64];

                ReferenceImplementations.FastFloatingPointDCT.iDCT2D_llm(sourceArray, expectedDestArray, tempArray);

                // ReferenceImplementations.iDCT8x8_llm_sse(sourceArray, expectedDestArray, tempArray);
                Block8x8F source = new Block8x8F();
                source.LoadFrom(sourceArray);

                Block8x8F dest = new Block8x8F();
                Block8x8F tempBuffer = new Block8x8F();

                FastFloatingPointDCT.TransformIDCT(ref source, ref dest, ref tempBuffer);

                float[] actualDestArray = new float[64];
                dest.CopyTo(actualDestArray);

                this.Print8x8Data(expectedDestArray);
                this.Output.WriteLine("**************");
                this.Print8x8Data(actualDestArray);
                Assert.Equal(expectedDestArray, actualDestArray, new ApproximateFloatComparer(1f));
                Assert.Equal(expectedDestArray, actualDestArray, new ApproximateFloatComparer(1f));
            }


            [Theory]
            [InlineData(1)]
            [InlineData(2)]
            public void FDCT8x4_LeftPart(int seed)
            {
                Span<float> src = JpegUtilityTestFixture.Create8x8RoundedRandomFloatData(-200, 200, seed);
                Block8x8F srcBlock = new Block8x8F();
                srcBlock.LoadFrom(src);

                Block8x8F destBlock = new Block8x8F();

                float[] expectedDest = new float[64];

                ReferenceImplementations.FastFloatingPointDCT.fDCT2D8x4_32f(src, expectedDest);
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
                Span<float> src = JpegUtilityTestFixture.Create8x8RoundedRandomFloatData(-200, 200, seed);
                Block8x8F srcBlock = new Block8x8F();
                srcBlock.LoadFrom(src);

                Block8x8F destBlock = new Block8x8F();

                float[] expectedDest = new float[64];

                ReferenceImplementations.FastFloatingPointDCT.fDCT2D8x4_32f(src.Slice(4), expectedDest.AsSpan().Slice(4));
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
                Span<float> src = JpegUtilityTestFixture.Create8x8RoundedRandomFloatData(-200, 200, seed);
                Block8x8F srcBlock = new Block8x8F();
                srcBlock.LoadFrom(src);

                Block8x8F destBlock = new Block8x8F();

                float[] expectedDest = new float[64];
                float[] temp1 = new float[64];
                Block8x8F temp2 = new Block8x8F();

                ReferenceImplementations.FastFloatingPointDCT.fDCT2D_llm(src, expectedDest, temp1, downscaleBy8: true);
                FastFloatingPointDCT.TransformFDCT(ref srcBlock, ref destBlock, ref temp2, false);

                float[] actualDest = new float[64];
                destBlock.CopyTo(actualDest);

                Assert.Equal(actualDest, expectedDest, new ApproximateFloatComparer(1f));
            }

        }
    }
}