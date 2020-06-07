// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Filters
{
    using SixLabors.ImageSharp.Processing;

    [GroupOutput("Filters")]
    public class SaturateTest
    {
        public static readonly TheoryData<float> SaturationValues
        = new TheoryData<float>
        {
            .5F,
            1.5F,
        };

        [Theory]
        [WithTestPatternImages(nameof(SaturationValues), 48, 48, PixelTypes.Rgba32)]
        public void ApplySaturationFilter<TPixel>(TestImageProvider<TPixel> provider, float value)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            provider.RunValidatingProcessorTest(x => x.Saturate(value), value);
        }
    }
}
