// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Linq;
using System.Numerics;
using SixLabors.ImageSharp.ColorSpaces.Companding;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Companding
{
    /// <summary>
    /// Tests various companding algorithms. Numbers are hand calculated from formulas online.
    /// TODO: Oddly the formula for converting to/from Rec 2020 and 709 from Wikipedia seems to cause the value to 
    /// fail a round trip. They're large spaces so this is a surprise. More reading required!!
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
            CompandingIsCorrectImpl(e, c, .4484759F, .3937096F);
        }

        [Fact]
        public void Rec709Companding_IsCorrect()
        {
            const float input = .667F;
            float e = Rec709Companding.Expand(input);
            float c = Rec709Companding.Compress(e);
            CompandingIsCorrectImpl(e, c, .4483577F, .3937451F);
        }

        [Fact]
        public void SRgbCompanding_IsCorrect()
        {
            const float input = .667F;
            float e = SRgbCompanding.Expand(input);
            float c = SRgbCompanding.Compress(e);
            CompandingIsCorrectImpl(e, c, .40242353F, .667F);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(30)]
        public void SRgbCompanding_Expand_VectorSpan(int length)
        {
            var rnd = new Random(42);
            Vector4[] source = rnd.GenerateRandomVectorArray(length, 0, 1);
            Vector4[] expected = source.Select(v => SRgbCompanding.Expand(v)).ToArray();

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
            Vector4[] expected = source.Select(v => SRgbCompanding.Compress(v)).ToArray();

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
            CompandingIsCorrectImpl(e, c, .41027668F, .667F);
        }

        [Fact]
        public void LCompanding_IsCorrect()
        {
            const float input = .667F;
            float e = LCompanding.Expand(input);
            float c = LCompanding.Compress(e);
            CompandingIsCorrectImpl(e, c, .36236193F, .58908917F);
        }

        private static void CompandingIsCorrectImpl(float e, float c, float expanded, float compressed)
        {
            Assert.Equal(expanded, e, FloatComparer);
            Assert.Equal(compressed, c, FloatComparer);
        }
    }
}