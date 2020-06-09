// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Effects
{
    using SixLabors.ImageSharp.Processing;

    [GroupOutput("Filters")]
    public class ContrastTest
    {
        public static readonly TheoryData<float> ContrastValues
        = new TheoryData<float>
        {
            .5F,
            1.5F
        };

        [Theory]
        [WithTestPatternImages(nameof(ContrastValues), 48, 48, PixelTypes.Rgba32)]
        public void ApplyContrastFilter<TPixel>(TestImageProvider<TPixel> provider, float value)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            provider.RunValidatingProcessorTest(x => x.Contrast(value), value);
        }
    }
}
