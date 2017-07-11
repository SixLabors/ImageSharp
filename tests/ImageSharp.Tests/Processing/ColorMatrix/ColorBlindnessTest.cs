// <copyright file="ColorBlindnessTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Processing.ColorMatrix
{
    using System.Collections.Generic;
    using ImageSharp.PixelFormats;
    using ImageSharp.Processing;
    using ImageSharp.Processing.Processors;
    using ImageSharp.Tests.TestUtilities;
    using SixLabors.Primitives;
    using Xunit;

    public class ColorBlindnessTest : BaseImageOperationsExtensionTest
    {
        public static IEnumerable<object[]> TheoryData = new[] {
            new object[]{ new TestType<AchromatomalyProcessor<Rgba32>>(), ColorBlindness.Achromatomaly },
            new object[]{ new TestType<AchromatopsiaProcessor<Rgba32>>(), ColorBlindness.Achromatopsia },
            new object[]{ new TestType<DeuteranomalyProcessor<Rgba32>>(), ColorBlindness.Deuteranomaly },
            new object[]{ new TestType<DeuteranopiaProcessor<Rgba32>>(), ColorBlindness.Deuteranopia },
            new object[]{ new TestType<ProtanomalyProcessor<Rgba32>>(), ColorBlindness.Protanomaly },
            new object[]{ new TestType<ProtanopiaProcessor<Rgba32>>(), ColorBlindness.Protanopia },
            new object[]{ new TestType<TritanomalyProcessor<Rgba32>>(), ColorBlindness.Tritanomaly },
            new object[]{ new TestType<TritanopiaProcessor<Rgba32>>(), ColorBlindness.Tritanopia }
        };

        [Theory]
        [MemberData(nameof(TheoryData))]
        public void ColorBlindness_CorrectProcessor<T>(TestType<T> testType, ColorBlindness colorBlindness)
            where T : IImageProcessor<Rgba32>
        {
            this.operations.ColorBlindness(colorBlindness);
            var p = this.Verify<T>();
        }
        [Theory]
        [MemberData(nameof(TheoryData))]
        public void ColorBlindness_rect_CorrectProcessor<T>(TestType<T> testType, ColorBlindness colorBlindness)
            where T : IImageProcessor<Rgba32>
        {
            this.operations.ColorBlindness(colorBlindness, this.rect);
            var p = this.Verify<T>(this.rect);
        }
    }
}