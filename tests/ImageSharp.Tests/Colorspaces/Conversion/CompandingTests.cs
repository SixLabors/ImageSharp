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
            CompandingIsCorrectImpl(new Rec2020Companding(), .667F, .4484759F, .3937096F);
        }

        [Fact]
        public void Rec709CompandingIsCorrect()
        {
            CompandingIsCorrectImpl(new Rec709Companding(), .667F, .4483577F, .3937451F);
        }

        [Fact]
        public void SRgbCompandingIsCorrect()
        {
            CompandingIsCorrectImpl(new SRgbCompanding(), .667F, .40242353F, .667F);
        }

        [Fact]
        public void GammaCompandingIsCorrect()
        {
            CompandingIsCorrectImpl(new GammaCompanding(2.2F), .667F, .41027668F, .667F);
        }

        [Fact]
        public void LCompandingIsCorrect()
        {
            CompandingIsCorrectImpl(new LCompanding(), .667F, .36236193F, .58908917F);
        }

        private static void CompandingIsCorrectImpl(ICompanding companding, float input, float expanded, float compressed)
        {
            float e = companding.Expand(input);
            float c = companding.Compress(e);

            Assert.Equal(expanded, e, FloatComparer);
            Assert.Equal(compressed, c, FloatComparer);
        }
    }
}
