// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Filters
{
    [Trait("Category", "Processors")]
    [GroupOutput("Filters")]
    public class LomographTest
    {
        [Theory]
        [WithTestPatternImages(48, 48, PixelTypes.Rgba32)]
        public void ApplyLomographFilter<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            provider.RunValidatingProcessorTest(x => x.Lomograph());
        }
    }
}
