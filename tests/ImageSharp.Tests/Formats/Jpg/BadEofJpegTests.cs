﻿// <copyright file="JpegEncoderTests.cs" company="James Jackson-South">
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
    using ImageSharp.PixelFormats;

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