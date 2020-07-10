// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors;
using SixLabors.ImageSharp.Processing.Processors.Filters;
using SixLabors.ImageSharp.Tests.TestUtilities;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Filters
{
    public class ColorBlindnessTest : BaseImageOperationsExtensionTest
    {
        public static IEnumerable<object[]> TheoryData = new[]
        {
            new object[] { new TestType<AchromatomalyProcessor>(), ColorBlindnessMode.Achromatomaly },
            new object[] { new TestType<AchromatopsiaProcessor>(), ColorBlindnessMode.Achromatopsia },
            new object[] { new TestType<DeuteranomalyProcessor>(), ColorBlindnessMode.Deuteranomaly },
            new object[] { new TestType<DeuteranopiaProcessor>(), ColorBlindnessMode.Deuteranopia },
            new object[] { new TestType<ProtanomalyProcessor>(), ColorBlindnessMode.Protanomaly },
            new object[] { new TestType<ProtanopiaProcessor>(), ColorBlindnessMode.Protanopia },
            new object[] { new TestType<TritanomalyProcessor>(), ColorBlindnessMode.Tritanomaly },
            new object[] { new TestType<TritanopiaProcessor>(), ColorBlindnessMode.Tritanopia }
        };

        [Theory]
        [MemberData(nameof(TheoryData))]
        public void ColorBlindness_CorrectProcessor<T>(TestType<T> testType, ColorBlindnessMode colorBlindness)
            where T : IImageProcessor
        {
            this.operations.ColorBlindness(colorBlindness);
            this.Verify<T>();
        }

        [Theory]
        [MemberData(nameof(TheoryData))]
        public void ColorBlindness_rect_CorrectProcessor<T>(TestType<T> testType, ColorBlindnessMode colorBlindness)
            where T : IImageProcessor
        {
            this.operations.ColorBlindness(colorBlindness, this.rect);
            this.Verify<T>(this.rect);
        }
    }
}
