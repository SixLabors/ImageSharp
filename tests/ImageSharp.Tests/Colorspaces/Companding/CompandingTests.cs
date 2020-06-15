// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using SixLabors.ImageSharp.ColorSpaces.Companding;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Companding
{
    /// <summary>
    /// Tests various companding algorithms. Expanded numbers are hand calculated from formulas online.
    /// </summary>
    public class CompandingTests
    {
        private static readonly ApproximateFloatComparer FloatComparer = new ApproximateFloatComparer(.00001F);

        [Fact]
        public void Rec2020Companding_IsCorrect()
        {
            const float input = .667F;
            float e = Rec2020Companding.Expand(input);
            float c = Rec2020Companding.Compress(e);
            CompandingIsCorrectImpl(e, c, .4484759F, input);
        }

        [Fact]
        public void Rec709Companding_IsCorrect()
        {
            const float input = .667F;
            float e = Rec709Companding.Expand(input);
            float c = Rec709Companding.Compress(e);
            CompandingIsCorrectImpl(e, c, .4483577F, input);
        }

        [Fact]
        public void SRgbCompanding_IsCorrect()
        {
            const float input = .667F;
            float e = SRgbCompanding.Expand(input);
            float c = SRgbCompanding.Compress(e);
            CompandingIsCorrectImpl(e, c, .40242353F, input);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(30)]
        public void SRgbCompanding_Expand_VectorSpan(int length)
        {
            var rnd = new Random(42);
            Vector4[] source = rnd.GenerateRandomVectorArray(length, 0, 1);
            var expected = new Vector4[source.Length];

            for (int i = 0; i < source.Length; i++)
            {
                Vector4 s = source[i];
                ref Vector4 e = ref expected[i];
                SRgbCompanding.Expand(ref s);
                e = s;
            }

            SRgbCompanding.Expand(source);

            Assert.Equal(expected, source, new ApproximateFloatComparer(1e-6f));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(30)]
        public void SRgbCompanding_Compress_VectorSpan(int length)
        {
            var rnd = new Random(42);
            Vector4[] source = rnd.GenerateRandomVectorArray(length, 0, 1);
            var expected = new Vector4[source.Length];

            for (int i = 0; i < source.Length; i++)
            {
                Vector4 s = source[i];
                ref Vector4 e = ref expected[i];
                SRgbCompanding.Compress(ref s);
                e = s;
            }

            SRgbCompanding.Compress(source);

            Assert.Equal(expected, source, new ApproximateFloatComparer(1e-6f));
        }

        [Fact]
        public void GammaCompanding_IsCorrect()
        {
            const float gamma = 2.2F;
            const float input = .667F;
            float e = GammaCompanding.Expand(input, gamma);
            float c = GammaCompanding.Compress(e, gamma);
            CompandingIsCorrectImpl(e, c, .41027668F, input);
        }

        [Fact]
        public void LCompanding_IsCorrect()
        {
            const float input = .667F;
            float e = LCompanding.Expand(input);
            float c = LCompanding.Compress(e);
            CompandingIsCorrectImpl(e, c, .36236193F, input);
        }

        private static void CompandingIsCorrectImpl(float e, float c, float expanded, float compressed)
        {
            Assert.Equal(expanded, e, FloatComparer);
            Assert.Equal(compressed, c, FloatComparer);
        }
    }
}