// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Effects
{
    using SixLabors.ImageSharp.Processing;

    [GroupOutput("Filters")]
    public class OpacityTest
    {
        public static readonly TheoryData<float> AlphaValues
        = new TheoryData<float>
        {
            20/100F,
            80/100F
        };

        [Theory]
        [WithTestPatternImages(nameof(AlphaValues), 48, 48, PixelTypes.Rgba32)]
        public void ApplyAlphaFilter<TPixel>(TestImageProvider<TPixel> provider, float value)
            where TPixel : struct, IPixel<TPixel>
        {
            provider.RunValidatingProcessorTest(x => x.Opacity(value), value);
        }
    }
}