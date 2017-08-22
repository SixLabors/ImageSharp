// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats.Jpeg.GolangPort.Utils;

using Xunit;
using Xunit.Abstractions;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    using System;

    public class ReferenceImplementationsTests : JpegUtilityTestFixture
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
            int[] intData = Create8x8RandomIntData(-200, 200, seed);
            Span<float> floatSrc = intData.ConvertAllToFloat();

            ReferenceImplementations.IntegerReferenceDCT.TransformIDCTInplace(intData);

            float[] dest = new float[64];
            float[] temp = new float[64];

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
            Span<int> original = Create8x8RandomIntData(-200, 200, seed);

            Span<int> block = original.AddScalarToAllValues(128);

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
            int[] data = Create8x8RandomIntData(-200, 200, seed);
            float[] src = data.ConvertAllToFloat();
            float[] dest = new float[64];
            float[] temp = new float[64];

            ReferenceImplementations.fDCT2D_llm(src, dest, temp, true);
            ReferenceImplementations.iDCT2D_llm(dest, src, temp);

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
        public void Fdct_FloatingPointReferenceImplementation_IsEquivalentToIntegerImplementation(int seed)
        {
            int[] intData = Create8x8RandomIntData(-200, 200, seed);
            float[] floatSrc = intData.ConvertAllToFloat();

            ReferenceImplementations.IntegerReferenceDCT.TransformFDCTInplace(intData);

            float[] dest = new float[64];
            float[] temp = new float[64];

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