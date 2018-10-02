// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.ColorSpaces;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces
{
    public class StringRepresentationTests
    {
        private static readonly Vector3 one = new Vector3(1);
        private static readonly Vector3 zero = new Vector3(0);
        private static readonly Vector3 random = new Vector3(42.4F, 94.5F, 83.4F);

        public static readonly TheoryData<object, string> TestData = new TheoryData<object, string>
        {
            { new CieLab(zero), "CieLab(0, 0, 0)" },
            { new CieLch(zero), "CieLch(0, 0, 0)" },
            { new CieLchuv(zero), "CieLchuv(0, 0, 0)" },
            { new CieLuv(zero), "CieLuv(0, 0, 0)" },
            { new CieXyz(zero), "CieXyz(0, 0, 0)" },
            { new CieXyy(zero), "CieXyy(0, 0, 0)" },
            { new HunterLab(zero), "HunterLab(0, 0, 0)" },
            { new Lms(zero), "Lms(0, 0, 0)" },
            { new LinearRgb(zero), "LinearRgb(0, 0, 0)" },
            { new Rgb(zero), "Rgb(0, 0, 0)" },
            { new Hsl(zero), "Hsl(0, 0, 0)" },
            { new Hsv(zero), "Hsv(0, 0, 0)" },
            { new YCbCr(zero), "YCbCr(0, 0, 0)" },

            { new CieLab(one), "CieLab(1, 1, 1)" },
            { new CieLch(one), "CieLch(1, 1, 1)" },
            { new CieLchuv(one), "CieLchuv(1, 1, 1)" },
            { new CieLuv(one), "CieLuv(1, 1, 1)" },
            { new CieXyz(one), "CieXyz(1, 1, 1)" },
            { new CieXyy(one), "CieXyy(1, 1, 1)" },
            { new HunterLab(one), "HunterLab(1, 1, 1)" },
            { new Lms(one), "Lms(1, 1, 1)" },
            { new LinearRgb(one), "LinearRgb(1, 1, 1)" },
            { new Rgb(one), "Rgb(1, 1, 1)" },
            { new Hsl(one), "Hsl(1, 1, 1)" },
            { new Hsv(one), "Hsv(1, 1, 1)" },
            { new YCbCr(one), "YCbCr(1, 1, 1)" },
            { new CieXyChromaticityCoordinates(1, 1), "CieXyChromaticityCoordinates(1, 1)"},

            { new CieLab(random), "CieLab(42.4, 94.5, 83.4)" },
            { new CieLch(random), "CieLch(42.4, 94.5, 83.4)" },
            { new CieLchuv(random), "CieLchuv(42.4, 94.5, 83.4)" },
            { new CieLuv(random), "CieLuv(42.4, 94.5, 83.4)" },
            { new CieXyz(random), "CieXyz(42.4, 94.5, 83.4)" },
            { new CieXyy(random), "CieXyy(42.4, 94.5, 83.4)" },
            { new HunterLab(random), "HunterLab(42.4, 94.5, 83.4)" },
            { new Lms(random), "Lms(42.4, 94.5, 83.4)" },
            { new LinearRgb(random), "LinearRgb(1, 1, 1)" },  // clamping to 1 is expected
            { new Rgb(random), "Rgb(1, 1, 1)" },              // clamping to 1 is expected
            { new Hsl(random), "Hsl(42.4, 1, 1)" },           // clamping to 1 is expected
            { new Hsv(random), "Hsv(42.4, 1, 1)" },           // clamping to 1 is expected
            { new YCbCr(random), "YCbCr(42.4, 94.5, 83.4)" },
       };

        [Theory]
        [MemberData(nameof(TestData))]
        public void StringRepresentationsAreCorrect(object color, string text) => Assert.Equal(text, color.ToString());
    }
}