// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Filters
{
    using SixLabors.ImageSharp.Processing;

    [GroupOutput("Filters")]
    public class GrayscaleTest
    {
        public static readonly TheoryData<GrayscaleMode> GrayscaleModeTypes
            = new TheoryData<GrayscaleMode>
            {
                GrayscaleMode.Bt601,
                GrayscaleMode.Bt709
            };

        /// <summary>
        /// Use test patterns over loaded images to save decode time.
        /// </summary>
        [Theory]
        [WithTestPatternImages(nameof(GrayscaleModeTypes), 48, 48, PixelTypes.Rgba32)]
        public void ApplyGrayscaleFilter<TPixel>(TestImageProvider<TPixel> provider, GrayscaleMode value)
            where TPixel : struct, IPixel<TPixel>
        {
            provider.RunValidatingProcessorTest(x => x.Grayscale(value), value);
        }
    }
}