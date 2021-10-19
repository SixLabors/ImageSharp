// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Transforms
{
    [Trait("Category", "Processors")]
    [GroupOutput("Transforms")]
    public class CropTest
    {
        [Theory]
        [WithTestPatternImages(70, 30, PixelTypes.Rgba32, 0, 0, 70, 30)]
        [WithTestPatternImages(30, 70, PixelTypes.Rgba32, 7, 13, 20, 50)]
        public void Crop<TPixel>(TestImageProvider<TPixel> provider, int x, int y, int w, int h)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var rect = new Rectangle(x, y, w, h);
            FormattableString info = $"X{x}Y{y}.W{w}H{h}";
            provider.RunValidatingProcessorTest(
                ctx => ctx.Crop(rect),
                info,
                appendPixelTypeToFileName: false,
                comparer: ImageComparer.Exact);
        }
    }
}
