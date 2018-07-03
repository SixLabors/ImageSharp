// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Filters;
using SixLabors.ImageSharp.Processing.Processors;
using SixLabors.ImageSharp.Tests.TestUtilities;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Filters
{
    public class ColorBlindnessTest : BaseImageOperationsExtensionTest
    {
        public static IEnumerable<object[]> TheoryData = new[] {
            new object[]{ new TestType<AchromatomalyProcessor<Rgba32>>(), ColorBlindnessMode.Achromatomaly },
            new object[]{ new TestType<AchromatopsiaProcessor<Rgba32>>(), ColorBlindnessMode.Achromatopsia },
            new object[]{ new TestType<DeuteranomalyProcessor<Rgba32>>(), ColorBlindnessMode.Deuteranomaly },
            new object[]{ new TestType<DeuteranopiaProcessor<Rgba32>>(), ColorBlindnessMode.Deuteranopia },
            new object[]{ new TestType<ProtanomalyProcessor<Rgba32>>(), ColorBlindnessMode.Protanomaly },
            new object[]{ new TestType<ProtanopiaProcessor<Rgba32>>(), ColorBlindnessMode.Protanopia },
            new object[]{ new TestType<TritanomalyProcessor<Rgba32>>(), ColorBlindnessMode.Tritanomaly },
            new object[]{ new TestType<TritanopiaProcessor<Rgba32>>(), ColorBlindnessMode.Tritanopia }
        };

        [Theory]
        [MemberData(nameof(TheoryData))]
        public void ColorBlindness_CorrectProcessor<T>(TestType<T> testType, ColorBlindnessMode colorBlindness)
            where T : IImageProcessor<Rgba32>
        {
            this.operations.ColorBlindness(colorBlindness);
            T p = this.Verify<T>();
        }
        [Theory]
        [MemberData(nameof(TheoryData))]
        public void ColorBlindness_rect_CorrectProcessor<T>(TestType<T> testType, ColorBlindnessMode colorBlindness)
            where T : IImageProcessor<Rgba32>
        {
            this.operations.ColorBlindness(colorBlindness, this.rect);
            T p = this.Verify<T>(this.rect);
        }
    }
}