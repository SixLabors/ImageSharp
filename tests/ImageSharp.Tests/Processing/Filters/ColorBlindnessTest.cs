// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors;
using SixLabors.ImageSharp.Processing.Processors.Filters;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Processing.Filters;

[Trait("Category", "Processors")]
public class ColorBlindnessTest : BaseImageOperationsExtensionTest
{
    public static IEnumerable<object[]> TheoryData =
    [
        [new TestType<AchromatomalyProcessor>(), ColorBlindnessMode.Achromatomaly],
        [new TestType<AchromatopsiaProcessor>(), ColorBlindnessMode.Achromatopsia],
        [new TestType<DeuteranomalyProcessor>(), ColorBlindnessMode.Deuteranomaly],
        [new TestType<DeuteranopiaProcessor>(), ColorBlindnessMode.Deuteranopia],
        [new TestType<ProtanomalyProcessor>(), ColorBlindnessMode.Protanomaly],
        [new TestType<ProtanopiaProcessor>(), ColorBlindnessMode.Protanopia],
        [new TestType<TritanomalyProcessor>(), ColorBlindnessMode.Tritanomaly],
        [new TestType<TritanopiaProcessor>(), ColorBlindnessMode.Tritanopia]
    ];

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
