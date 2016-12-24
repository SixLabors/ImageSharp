// ReSharper disable InconsistentNaming
namespace ImageSharp.Tests.Formats.Jpg
{
    using System.Numerics;
    using ImageSharp.Formats;
    using Xunit;
    using Xunit.Abstractions;

    public class ReferenceImplementationsTests : UtilityTestClassBase
    {
        public ReferenceImplementationsTests(ITestOutputHelper output)
            : base(output)
        {
        }


        [Theory]
        [InlineData(42)]
        [InlineData(1)]
        [InlineData(2)]
        public void Idct_FloatingPointReferenceImplementation_IsEquivalentToIntegerImplementation(int seed)
        {
            MutableSpan<int> intData = Create8x8RandomIntData(-200, 200, seed);
            MutableSpan<float> floatSrc = intData.ConvertToFloat32MutableSpan();

            ReferenceImplementations.IntegerReferenceDCT.TransformIDCTInplace(intData);
            
            MutableSpan<float> dest = new MutableSpan<float>(64);
            MutableSpan<float> temp = new MutableSpan<float>(64);

            ReferenceImplementations.iDCT2D_llm(floatSrc, dest, temp);

            for (int i = 0; i < 64; i++)
            {
                float expected = intData[i];
                float actual = dest[i];

                Assert.Equal(expected, actual, new ApproximateFloatComparer(1f));
            }
        }

        [Theory]
        [InlineData(42, 0)]
        [InlineData(1, 0)]
        [InlineData(2, 0)]
        public void IntegerDCT_ForwardThenInverse(int seed, int startAt)
        {
            MutableSpan<int> original = Create8x8RandomIntData(-200, 200, seed);

            var block = original.AddScalarToAllValues(128);

            ReferenceImplementations.IntegerReferenceDCT.TransformFDCTInplace(block);
            
            for (int i = 0; i < 64; i++)
            {
                block[i] /= 8;
            }

            ReferenceImplementations.IntegerReferenceDCT.TransformIDCTInplace(block);
            
            for (int i = startAt; i < 64; i++)
            {
                float expected = original[i];
                float actual = (float)block[i];

                Assert.Equal(expected, actual, new ApproximateFloatComparer(3f));
            }

        }

        [Theory]
        [InlineData(42, 0)]
        [InlineData(1, 0)]
        [InlineData(2, 0)]
        public void FloatingPointDCT_ReferenceImplementation_ForwardThenInverse(int seed, int startAt)
        {
            var data = Create8x8RandomIntData(-200, 200, seed);
            MutableSpan<float> src = new MutableSpan<int>(data).ConvertToFloat32MutableSpan();
            MutableSpan<float> dest = new MutableSpan<float>(64);
            MutableSpan<float> temp = new MutableSpan<float>(64);

            ReferenceImplementations.fDCT2D_llm(src, dest, temp, true);
            ReferenceImplementations.iDCT2D_llm(dest, src, temp);

            for (int i = startAt; i < 64; i++)
            {
                float expected = data[i];
                float actual = (float)src[i];
                
                Assert.Equal(expected, actual, new ApproximateFloatComparer(2f));
            }
        }

        [Fact]
        public void HowMuchIsTheFish()
        {
            Output.WriteLine(Vector<int>.Count.ToString());
        }

        [Theory]
        [InlineData(42)]
        [InlineData(1)]
        [InlineData(2)]
        public void Fdct_FloatingPointReferenceImplementation_IsEquivalentToIntegerImplementation(int seed)
        {
            MutableSpan<int> intData = Create8x8RandomIntData(-200, 200, seed);
            MutableSpan<float> floatSrc = intData.ConvertToFloat32MutableSpan();
            
            ReferenceImplementations.IntegerReferenceDCT.TransformFDCTInplace(intData);
            
            MutableSpan<float> dest = new MutableSpan<float>(64);
            MutableSpan<float> temp = new MutableSpan<float>(64);

            ReferenceImplementations.fDCT2D_llm(floatSrc, dest, temp, offsetSourceByNeg128: true);

            for (int i = 0; i < 64; i++)
            {
                float expected = intData[i];
                float actual = dest[i];

                Assert.Equal(expected, actual, new ApproximateFloatComparer(1f));
            }
        }   
    }
}