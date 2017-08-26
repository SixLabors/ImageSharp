namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    using System;

    using SixLabors.ImageSharp.Formats.Jpeg.Common;
    using SixLabors.ImageSharp.Tests.Formats.Jpg.Utils;

    using Xunit;
    using Xunit.Abstractions;

    public partial class ReferenceImplementationsTests
    {
        
        public class StandardIntegerDCT
        {
            public StandardIntegerDCT(ITestOutputHelper output)
            {
                this.Output = output;
            }

            private ITestOutputHelper Output { get; }

            [Theory]
            [InlineData(42)]
            [InlineData(1)]
            [InlineData(2)]
            public void IDCT_IsEquivalentTo_AccurateImplementation(int seed)
            {
                int[] data = Create8x8RandomIntData(-1000, 1000, seed);

                Block8x8 source = default(Block8x8);
                source.LoadFrom(data);

                Block8x8 expected = ReferenceImplementations.AccurateDCT.TransformIDCT(ref source);
                Block8x8 actual = ReferenceImplementations.StandardIntegerDCT.TransformIDCT(ref source);

                long diff = Block8x8.TotalDifference(ref expected, ref actual);
                this.Output.WriteLine(expected.ToString());
                this.Output.WriteLine(actual.ToString());
                this.Output.WriteLine("DIFFERENCE: "+diff);
            }


            [Theory]
            [InlineData(42, 0)]
            [InlineData(1, 0)]
            [InlineData(2, 0)]
            public void ForwardThenInverse(int seed, int startAt)
            {
                Span<int> original = JpegUtilityTestFixture.Create8x8RandomIntData(-200, 200, seed);

                Span<int> block = original.AddScalarToAllValues(128);

                ReferenceImplementations.StandardIntegerDCT.TransformFDCTInplace(block);

                for (int i = 0; i < 64; i++)
                {
                    block[i] /= 8;
                }

                ReferenceImplementations.StandardIntegerDCT.TransformIDCTInplace(block);

                for (int i = startAt; i < 64; i++)
                {
                    float expected = original[i];
                    float actual = (float)block[i];

                    Assert.Equal(expected, actual, new ApproximateFloatComparer(3f));
                }
            }
        }
    }
}