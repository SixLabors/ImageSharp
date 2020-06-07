// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Conversion
{
    public class VonKriesChromaticAdaptationTests
    {
        private static readonly ApproximateColorSpaceComparer ColorSpaceComparer = new ApproximateColorSpaceComparer(.0001F);
        public static readonly TheoryData<CieXyz, CieXyz> WhitePoints = new TheoryData<CieXyz, CieXyz>
        {
            { CieLuv.DefaultWhitePoint, CieLab.DefaultWhitePoint },
            { CieLuv.DefaultWhitePoint, CieLuv.DefaultWhitePoint }
        };

        [Theory]
        [MemberData(nameof(WhitePoints))]
        public void SingleAndBulkTransformYieldIdenticalResults(CieXyz sourceWhitePoint, CieXyz destinationWhitePoint)
        {
            var adaptation = new VonKriesChromaticAdaptation();
            var input = new CieXyz(1, 0, 1);
            CieXyz expected = adaptation.Transform(input, sourceWhitePoint, destinationWhitePoint);

            Span<CieXyz> inputSpan = new CieXyz[5];
            inputSpan.Fill(input);

            Span<CieXyz> actualSpan = new CieXyz[5];

            adaptation.Transform(inputSpan, actualSpan, sourceWhitePoint, destinationWhitePoint);

            for (int i = 0; i < inputSpan.Length; i++)
            {
                Assert.Equal(expected, actualSpan[i], ColorSpaceComparer);
            }
        }
    }
}
