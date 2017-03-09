// <copyright file="JpegEncoderTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImageSharp.Formats;
using Xunit;
using Xunit.Abstractions;
// ReSharper disable InconsistentNaming

namespace ImageSharp.Tests
{
    using System.Numerics;

    using ImageSharp.Formats.Jpg;
    using ImageSharp.Processing;

    public class BadEOFJpegTests : MeasureFixture
    {
        public BadEOFJpegTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [Theory]
        [WithFile(TestImages.Jpeg.Baseline.Bad.MissingEOF, PixelTypes.Color)]
        public void LoadBaselineImage<TColor>(TestImageProvider<TColor> provider)
            where TColor : struct, IPixel<TColor>
        {
            using (Image<TColor> image = provider.GetImage())
            {
                Assert.NotNull(image);
                provider.Utility.SaveTestOutputFile(image, "bmp");
            }
        }

        [Theory] // TODO: #18
        [WithFile(TestImages.Jpeg.Progressive.Bad.BadEOF, PixelTypes.Color)]
        public void LoadProgressiveImage<TColor>(TestImageProvider<TColor> provider)
            where TColor : struct, IPixel<TColor>
        {
            using (Image<TColor> image = provider.GetImage())
            {
                Assert.NotNull(image);
                provider.Utility.SaveTestOutputFile(image, "bmp");
            }
        }
    }
}