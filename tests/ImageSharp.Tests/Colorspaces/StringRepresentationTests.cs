// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces
{
    public class StringRepresentationTests
    {
        private static readonly Vector3 One = new Vector3(1);
        private static readonly Vector3 Zero = new Vector3(0);
        private static readonly Vector3 Random = new Vector3(42.4F, 94.5F, 83.4F);

        public static readonly TheoryData<object, string> TestData = new TheoryData<object, string>
        {
            { new CieLab(Zero), "CieLab(0, 0, 0)" },
            { new CieLch(Zero), "CieLch(0, 0, 0)" },
            { new CieLchuv(Zero), "CieLchuv(0, 0, 0)" },
            { new CieLuv(Zero), "CieLuv(0, 0, 0)" },
            { new CieXyz(Zero), "CieXyz(0, 0, 0)" },
            { new CieXyy(Zero), "CieXyy(0, 0, 0)" },
            { new HunterLab(Zero), "HunterLab(0, 0, 0)" },
            { new Lms(Zero), "Lms(0, 0, 0)" },
            { new LinearRgb(Zero), "LinearRgb(0, 0, 0)" },
            { new Rgb(Zero), "Rgb(0, 0, 0)" },
            { new Hsl(Zero), "Hsl(0, 0, 0)" },
            { new Hsv(Zero), "Hsv(0, 0, 0)" },
            { new YCbCr(Zero), "YCbCr(0, 0, 0)" },
            { new CieLab(One), "CieLab(1, 1, 1)" },
            { new CieLch(One), "CieLch(1, 1, 1)" },
            { new CieLchuv(One), "CieLchuv(1, 1, 1)" },
            { new CieLuv(One), "CieLuv(1, 1, 1)" },
            { new CieXyz(One), "CieXyz(1, 1, 1)" },
            { new CieXyy(One), "CieXyy(1, 1, 1)" },
            { new HunterLab(One), "HunterLab(1, 1, 1)" },
            { new Lms(One), "Lms(1, 1, 1)" },
            { new LinearRgb(One), "LinearRgb(1, 1, 1)" },
            { new Rgb(One), "Rgb(1, 1, 1)" },
            { new Hsl(One), "Hsl(1, 1, 1)" },
            { new Hsv(One), "Hsv(1, 1, 1)" },
            { new YCbCr(One), "YCbCr(1, 1, 1)" },
            { new CieXyChromaticityCoordinates(1, 1), "CieXyChromaticityCoordinates(1, 1)" },
            { new CieLab(Random), "CieLab(42.4, 94.5, 83.4)" },
            { new CieLch(Random), "CieLch(42.4, 94.5, 83.4)" },
            { new CieLchuv(Random), "CieLchuv(42.4, 94.5, 83.4)" },
            { new CieLuv(Random), "CieLuv(42.4, 94.5, 83.4)" },
            { new CieXyz(Random), "CieXyz(42.4, 94.5, 83.4)" },
            { new CieXyy(Random), "CieXyy(42.4, 94.5, 83.4)" },
            { new HunterLab(Random), "HunterLab(42.4, 94.5, 83.4)" },
            { new Lms(Random), "Lms(42.4, 94.5, 83.4)" },
            { new LinearRgb(Random), "LinearRgb(1, 1, 1)" },  // clamping to 1 is expected
            { new Rgb(Random), "Rgb(1, 1, 1)" },              // clamping to 1 is expected
            { new Hsl(Random), "Hsl(42.4, 1, 1)" },           // clamping to 1 is expected
            { new Hsv(Random), "Hsv(42.4, 1, 1)" },           // clamping to 1 is expected
            { new YCbCr(Random), "YCbCr(42.4, 94.5, 83.4)" },
        };

        [Theory]
        [MemberData(nameof(TestData))]
        public void StringRepresentationsAreCorrect(object color, string text) => Assert.Equal(text, color.ToString());
    }
}
