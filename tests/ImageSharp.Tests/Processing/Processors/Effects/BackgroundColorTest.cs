// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Effects
{
    [GroupOutput("Effects")]
    public class BackgroundColorTest
    {
        public static readonly string[] InputImages =
            {
                TestImages.Png.Splash,
                TestImages.Png.Ducky
            };

        [Theory]
        [WithFileCollection(nameof(InputImages), PixelTypes.Rgba32)]
        public void FullImage<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            provider.RunValidatingProcessorTest(x => x.BackgroundColor(Color.HotPink));
        }

        [Theory]
        [WithFileCollection(nameof(InputImages), PixelTypes.Rgba32)]
        public void InBox<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            provider.RunRectangleConstrainedValidatingProcessorTest(
                (x, rect) => x.BackgroundColor(Color.HotPink, rect));
        }
    }
}
