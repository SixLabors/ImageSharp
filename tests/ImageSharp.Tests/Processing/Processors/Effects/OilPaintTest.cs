// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Effects
{
    [GroupOutput("Effects")]
    public class OilPaintTest
    {
        public static readonly TheoryData<int, int> OilPaintValues = new TheoryData<int, int>
                                                                         {
                                                                             { 15, 10 },
                                                                             { 6, 5 }
                                                                         };

        public static readonly string[] InputImages =
            {
                TestImages.Png.CalliphoraPartial,
                TestImages.Bmp.Car
            };

        [Theory]
        [WithFileCollection(nameof(InputImages), nameof(OilPaintValues), PixelTypes.Rgba32)]
        public void FullImage<TPixel>(TestImageProvider<TPixel> provider, int levels, int brushSize)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            provider.RunValidatingProcessorTest(
                x =>
                {
                    x.OilPaint(levels, brushSize);
                    return $"{levels}-{brushSize}";
                },
                ImageComparer.TolerantPercentage(0.01F),
                appendPixelTypeToFileName: false);
        }

        [Theory]
        [WithFileCollection(nameof(InputImages), nameof(OilPaintValues), PixelTypes.Rgba32)]
        [WithTestPatternImages(nameof(OilPaintValues), 100, 100, PixelTypes.Rgba32)]
        public void InBox<TPixel>(TestImageProvider<TPixel> provider, int levels, int brushSize)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            provider.RunRectangleConstrainedValidatingProcessorTest(
                (x, rect) => x.OilPaint(levels, brushSize, rect),
                $"{levels}-{brushSize}",
                ImageComparer.TolerantPercentage(0.01F));
        }
    }
}
