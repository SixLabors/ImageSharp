// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Effects
{
    using SixLabors.ImageSharp.Processing;

    [GroupOutput("Filters")]
    public class InvertTest
    {
        [Theory]
        [WithTestPatternImages(48, 48, PixelTypes.Rgba32)]
        public void ApplyInvertFilter<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            provider.RunValidatingProcessorTest(x => x.Invert());
        }
    }
}