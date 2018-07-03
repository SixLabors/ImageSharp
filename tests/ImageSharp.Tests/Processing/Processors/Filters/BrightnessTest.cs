// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Effects
{
    using SixLabors.ImageSharp.Processing;

    [GroupOutput("Filters")]
    public class BrightnessTest
    {
        public static readonly TheoryData<float> BrightnessValues
        = new TheoryData<float>
        {
            .5F,
           1.5F
        };

        [Theory]
        [WithTestPatternImages(nameof(BrightnessValues), 48, 48, PixelTypes.Rgba32)]
        public void ApplyBrightnessFilter<TPixel>(TestImageProvider<TPixel> provider, float value)
            where TPixel : struct, IPixel<TPixel>
        {
            provider.RunValidatingProcessorTest(ctx => ctx.Brightness(value), value);
        }
    }
}