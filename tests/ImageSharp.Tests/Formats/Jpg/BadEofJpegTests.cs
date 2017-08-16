// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
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
                provider.Utility.SaveTestOutputFile(image, "bmp");
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
                provider.Utility.SaveTestOutputFile(image, "bmp");
            }
        }
    }
}