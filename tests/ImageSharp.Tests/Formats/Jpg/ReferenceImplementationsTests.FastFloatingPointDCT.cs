// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    using System;

    using SixLabors.ImageSharp.Formats.Jpeg.Common;
    using SixLabors.ImageSharp.Tests.Formats.Jpg.Utils;

    using Xunit;
    using Xunit.Abstractions;

    public partial class ReferenceImplementationsTests
    {
        public class FastFloatingPointDCT : JpegUtilityTestFixture
        {
            public FastFloatingPointDCT(ITestOutputHelper output)
                : base(output)
            {
            }

            [Theory]
            [InlineData(42, 0)]
            [InlineData(1, 0)]
            [InlineData(2, 0)]
            public void ForwardThenInverse(int seed, int startAt)
            {
                int[] data = JpegUtilityTestFixture.Create8x8RandomIntData(-1000, 1000, seed);
                float[] src = data.ConvertAllToFloat();
                float[] dest = new float[64];
                float[] temp = new float[64];

                ReferenceImplementations.FastFloatingPointDCT.fDCT2D_llm(src, dest, temp, true);
                ReferenceImplementations.FastFloatingPointDCT.iDCT2D_llm(dest, src, temp);

                this.CompareBlocks(data.ConvertAllToFloat(), src, 2f);
            }

            [Theory]
            [InlineData(42)]
            [InlineData(1)]
            [InlineData(2)]
            public void IDCT_IsEquivalentTo_StandardIntegerImplementation(int seed)
            {
                int[] intData = JpegUtilityTestFixture.Create8x8RandomIntData(-1000, 1000, seed);
                Span<float> floatSrc = intData.ConvertAllToFloat();

                ReferenceImplementations.StandardIntegerDCT.TransformIDCTInplace(intData);

                float[] dest = new float[64];
                float[] temp = new float[64];

                ReferenceImplementations.FastFloatingPointDCT.iDCT2D_llm(floatSrc, dest, temp);

                this.CompareBlocks(intData.ConvertAllToFloat(), dest, 1f);
            }

            [Theory]
            [InlineData(42)]
            [InlineData(1)]
            [InlineData(2)]
            public void IDCT_IsEquivalentTo_AccurateImplementation(int seed)
            {
                int[] intData = JpegUtilityTestFixture.Create8x8RandomIntData(-1000, 1000, seed);
                float[] floatSrc = intData.ConvertAllToFloat();

                ReferenceImplementations.AccurateDCT.TransformIDCTInplace(intData);

                float[] dest = new float[64];
                float[] temp = new float[64];

                ReferenceImplementations.FastFloatingPointDCT.iDCT2D_llm(floatSrc, dest, temp);

                this.CompareBlocks(intData.ConvertAllToFloat(), dest, 1f);
            }
            
            [Theory]
            [InlineData(42)]
            [InlineData(1)]
            [InlineData(2)]
            public void FDCT_IsEquivalentTo_StandardIntegerImplementation(int seed)
            {
                int[] intData = JpegUtilityTestFixture.Create8x8RandomIntData(-1000, 1000, seed);
                float[] floatSrc = intData.ConvertAllToFloat();

                ReferenceImplementations.StandardIntegerDCT.TransformFDCTInplace(intData);

                float[] dest = new float[64];
                float[] temp = new float[64];

                ReferenceImplementations.FastFloatingPointDCT.fDCT2D_llm(floatSrc, dest, temp, offsetSourceByNeg128: true);

                this.CompareBlocks(intData.ConvertAllToFloat(), dest, 2f);
            }

            [Theory]
            [InlineData(42)]
            [InlineData(1)]
            [InlineData(2)]
            public void FDCT_IsEquivalentTo_AccurateImplementation(int seed)
            {
                float[] floatData = JpegUtilityTestFixture.Create8x8RandomFloatData(-1000, 1000);

                Block8x8F source = default(Block8x8F);
                source.LoadFrom(floatData);

                Block8x8F expected = ReferenceImplementations.AccurateDCT.TransformFDCT(ref source);
                Block8x8F actual = ReferenceImplementations.FastFloatingPointDCT.TransformFDCT(ref source);

                this.CompareBlocks(expected, actual, 1f);

                //int[] intData = JpegUtilityTestFixture.Create8x8RandomIntData(-1000, 1000, seed);
                //float[] floatSrc = intData.ConvertAllToFloat();

                //ReferenceImplementations.AccurateDCT.TransformFDCTInplace(intData);

                //float[] dest = new float[64];
                //float[] temp = new float[64];

                //ReferenceImplementations.FastFloatingPointDCT.fDCT2D_llm(floatSrc, dest, temp, offsetSourceByNeg128: false);

                //this.CompareBlocks(intData.ConvertAllToFloat(), dest, 1f);
            }


        }
    }
}