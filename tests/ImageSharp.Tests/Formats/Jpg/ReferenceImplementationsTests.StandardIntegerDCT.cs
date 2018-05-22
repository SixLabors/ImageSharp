// ReSharper disable InconsistentNaming

using System;

using SixLabors.ImageSharp.Formats.Jpeg.Components;
using SixLabors.ImageSharp.Tests.Formats.Jpg.Utils;

using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    public partial class ReferenceImplementationsTests
    {
        public class StandardIntegerDCT : JpegFixture
        {
            public StandardIntegerDCT(ITestOutputHelper output)
                : base(output)
            {
            }

            [Theory]
            [InlineData(42, 200)]
            [InlineData(1, 200)]
            [InlineData(2, 200)]
            public void IDCT_IsEquivalentTo_AccurateImplementation(int seed, int range)
            {
                int[] data = Create8x8RandomIntData(-range, range, seed);

                Block8x8 source = default;
                source.LoadFrom(data);

                Block8x8 expected = ReferenceImplementations.AccurateDCT.TransformIDCT(ref source);
                Block8x8 actual = ReferenceImplementations.StandardIntegerDCT.TransformIDCT(ref source);

                this.CompareBlocks(expected, actual, 1);
            }

            [Theory]
            [InlineData(42)]
            [InlineData(1)]
            [InlineData(2)]
            public void FDCT_IsEquivalentTo_AccurateImplementation(int seed)
            {
                int[] data = Create8x8RandomIntData(-1000, 1000, seed);

                Block8x8F source = default;
                source.LoadFrom(data);

                Block8x8F expected = ReferenceImplementations.AccurateDCT.TransformFDCT(ref source);

                source += 128;
                Block8x8 temp = source.RoundAsInt16Block();
                Block8x8 actual8 = ReferenceImplementations.StandardIntegerDCT.Subtract128_TransformFDCT_Upscale8(ref temp);
                Block8x8F actual = actual8.AsFloatBlock();
                actual /= 8;

                this.CompareBlocks(expected, actual, 1f);
            }

            [Theory]
            [InlineData(42, 0)]
            [InlineData(1, 0)]
            [InlineData(2, 0)]
            public void ForwardThenInverse(int seed, int startAt)
            {
                Span<int> original = JpegFixture.Create8x8RandomIntData(-200, 200, seed);

                Span<int> block = original.AddScalarToAllValues(128);

                ReferenceImplementations.StandardIntegerDCT.Subtract128_TransformFDCT_Upscale8_Inplace(block);

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