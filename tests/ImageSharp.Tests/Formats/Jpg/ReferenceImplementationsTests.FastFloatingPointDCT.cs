// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    using System;

    using SixLabors.ImageSharp.Tests.Formats.Jpg.Utils;

    using Xunit;

    public partial class ReferenceImplementationsTests
    {
        public class FastFloatingPointDCT
        {
            [Theory]
            [InlineData(42, 0)]
            [InlineData(1, 0)]
            [InlineData(2, 0)]
            public void ForwardThenInverse(int seed, int startAt)
            {
                int[] data = JpegUtilityTestFixture.Create8x8RandomIntData(-200, 200, seed);
                float[] src = data.ConvertAllToFloat();
                float[] dest = new float[64];
                float[] temp = new float[64];

                ReferenceImplementations.FastFloatingPointDCT.fDCT2D_llm(src, dest, temp, true);
                ReferenceImplementations.FastFloatingPointDCT.iDCT2D_llm(dest, src, temp);

                for (int i = startAt; i < 64; i++)
                {
                    float expected = data[i];
                    float actual = (float)src[i];

                    Assert.Equal(expected, actual, new ApproximateFloatComparer(2f));
                }
            }

            [Theory]
            [InlineData(42)]
            [InlineData(1)]
            [InlineData(2)]
            public void IDCT_IsEquivalentTo_StandardIntegerImplementation(int seed)
            {
                int[] intData = JpegUtilityTestFixture.Create8x8RandomIntData(-200, 200, seed);
                Span<float> floatSrc = intData.ConvertAllToFloat();

                ReferenceImplementations.StandardIntegerDCT.TransformIDCTInplace(intData);

                float[] dest = new float[64];
                float[] temp = new float[64];

                ReferenceImplementations.FastFloatingPointDCT.iDCT2D_llm(floatSrc, dest, temp);

                for (int i = 0; i < 64; i++)
                {
                    float expected = intData[i];
                    float actual = dest[i];

                    Assert.Equal(expected, actual, new ApproximateFloatComparer(1f));
                }
            }

            [Theory]
            [InlineData(42)]
            [InlineData(1)]
            [InlineData(2)]
            public void IDCT_IsEquivalentTo_AccurateImplementation(int seed)
            {
                int[] intData = JpegUtilityTestFixture.Create8x8RandomIntData(-200, 200, seed);
                float[] floatSrc = intData.ConvertAllToFloat();

                ReferenceImplementations.AccurateDCT.TransformIDCTInplace(intData);

                float[] dest = new float[64];
                float[] temp = new float[64];

                ReferenceImplementations.FastFloatingPointDCT.iDCT2D_llm(floatSrc, dest, temp);

                for (int i = 0; i < 64; i++)
                {
                    float expected = intData[i];
                    float actual = dest[i];

                    Assert.Equal(expected, actual, new ApproximateFloatComparer(1f));
                }
            }
            
            [Theory]
            [InlineData(42)]
            [InlineData(1)]
            [InlineData(2)]
            public void FDCT_IsEquivalentTo_StandardIntegerImplementation(int seed)
            {
                int[] intData = JpegUtilityTestFixture.Create8x8RandomIntData(-200, 200, seed);
                float[] floatSrc = intData.ConvertAllToFloat();

                ReferenceImplementations.StandardIntegerDCT.TransformFDCTInplace(intData);

                float[] dest = new float[64];
                float[] temp = new float[64];

                ReferenceImplementations.FastFloatingPointDCT.fDCT2D_llm(floatSrc, dest, temp, offsetSourceByNeg128: true);

                for (int i = 0; i < 64; i++)
                {
                    float expected = intData[i];
                    float actual = dest[i];

                    Assert.Equal(expected, actual, new ApproximateFloatComparer(1f));
                }
            }
        }
    }
}