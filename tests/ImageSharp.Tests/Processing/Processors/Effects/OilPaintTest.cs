// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

using SixLabors.Primitives;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Effects
{
    public class OilPaintTest : FileTestBase
    {
        public static readonly TheoryData<int, int> OilPaintValues = new TheoryData<int, int>
                                                                         {
                                                                             { 15, 10 }, { 6, 5 }
                                                                         };

        [Theory]
        [WithFileCollection(nameof(DefaultFiles), nameof(OilPaintValues), DefaultPixelType)]
        public void ApplyOilPaintFilter<TPixel>(TestImageProvider<TPixel> provider, int levels, int brushSize)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.OilPaint(levels, brushSize));
                image.DebugSave(provider, string.Join("-", levels, brushSize));
            }
        }

        [Theory]
        [WithFileCollection(nameof(DefaultFiles), nameof(OilPaintValues), DefaultPixelType)]
        public void ApplyOilPaintFilterInBox<TPixel>(TestImageProvider<TPixel> provider, int levels, int brushSize)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> source = provider.GetImage())
            using (Image<TPixel> image = source.Clone())
            {
                var bounds = new Rectangle(10, 10, image.Width / 2, image.Height / 2);

                image.Mutate(x => x.OilPaint(levels, brushSize, bounds));
                image.DebugSave(provider, string.Join("-", levels, brushSize));

                ImageComparer.Tolerant().VerifySimilarityIgnoreRegion(source, image, bounds);
            }
        }
    }
}