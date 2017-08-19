// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using Xunit;
using Xunit.Abstractions;

// ReSharper disable InconsistentNaming

namespace SixLabors.ImageSharp.Tests
{
    public class BadEOFJpegTests : MeasureFixture
    {
        public BadEOFJpegTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [Theory]
        [WithFile(TestImages.Jpeg.Baseline.Bad.MissingEOF, PixelTypes.Rgba32)]
        public void LoadBaselineImage<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                Assert.NotNull(image);
                image.DebugSave(provider);
            }
        }

        [Theory] // TODO: #18
        [WithFile(TestImages.Jpeg.Progressive.Bad.BadEOF, PixelTypes.Rgba32)]
        public void LoadProgressiveImage<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                Assert.NotNull(image);
                image.DebugSave(provider);
            }
        }
    }
}