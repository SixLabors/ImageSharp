// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Conversion
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
        public void Rec2020CompandingIsCorrect()
        {
            const float input = .667F;
            float e = Rec2020Companding.Expand(input);
            float c = Rec2020Companding.Compress(e);
            CompandingIsCorrectImpl(e, c, .4484759F, .3937096F);
        }

        [Fact]
        public void Rec709CompandingIsCorrect()
        {
            const float input = .667F;
            float e = Rec709Companding.Expand(input);
            float c = Rec709Companding.Compress(e);
            CompandingIsCorrectImpl(e, c, .4483577F, .3937451F);
        }

        [Fact]
        public void SRgbCompandingIsCorrect()
        {
            const float input = .667F;
            float e = SRgbCompanding.Expand(input);
            float c = SRgbCompanding.Compress(e);
            CompandingIsCorrectImpl(e, c, .40242353F, .667F);
        }

        [Fact]
        public void GammaCompandingIsCorrect()
        {
            const float gamma = 2.2F;
            const float input = .667F;
            float e = GammaCompanding.Expand(input, gamma);
            float c = GammaCompanding.Compress(e, gamma);
            CompandingIsCorrectImpl(e, c, .41027668F, .667F);
        }

        [Fact]
        public void LCompandingIsCorrect()
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
