// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Transforms
{
    public class SkewTest : FileTestBase
    {
        public static readonly TheoryData<float, float> SkewValues
        = new TheoryData<float, float>
        {
            { 20, 10 },
            { -20, -10 }
        };

        [Theory]
        [WithFileCollection(nameof(DefaultFiles), nameof(SkewValues), DefaultPixelType)]
        public void ImageShouldSkew<TPixel>(TestImageProvider<TPixel> provider, float x, float y)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(i => i.Skew(x, y));
                image.DebugSave(provider, string.Join("_", x, y));
            }
        }
    }
}