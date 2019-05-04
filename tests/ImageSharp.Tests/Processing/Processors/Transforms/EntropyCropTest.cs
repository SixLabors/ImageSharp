// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Transforms
{
    [GroupOutput("Transforms")]
    public class EntropyCropTest
    {
        public static readonly TheoryData<float> EntropyCropValues = new TheoryData<float> { .25F, .75F };

        public static readonly string[] InputImages =
            {
                TestImages.Png.Ducky,
                TestImages.Jpeg.Baseline.Jpeg400,
                TestImages.Jpeg.Baseline.MultiScanBaselineCMYK
            };

        [Theory]
        [WithFileCollection(nameof(InputImages), nameof(EntropyCropValues), PixelTypes.Rgba32)]
        public void EntropyCrop<TPixel>(TestImageProvider<TPixel> provider, float value)
            where TPixel : struct, IPixel<TPixel>
        {
            provider.RunValidatingProcessorTest(x => x.EntropyCrop(value), value, appendPixelTypeToFileName: false);
        }
    }
}