// <copyright file="SkewTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Processing.Transforms
{
    using ImageSharp.PixelFormats;

    using Xunit;

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
                image.Skew(x, y)
                    .DebugSave(provider, string.Join("_", x, y), Extensions.Bmp);
            }
        }
    }
}