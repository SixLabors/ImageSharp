// <copyright file="RotateTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Processing.Transforms
{
    using ImageSharp.PixelFormats;
    using ImageSharp.Processing;

    using Xunit;

    public class RotateTests : FileTestBase
    {
        public static readonly TheoryData<float> RotateFloatValues
            = new TheoryData<float>
        {
             170,
            -170
        };

        public static readonly TheoryData<RotateType> RotateEnumValues
            = new TheoryData<RotateType>
        {
            RotateType.None,
            RotateType.Rotate90,
            RotateType.Rotate180,
            RotateType.Rotate270
        };

        [Theory]
        [WithFileCollection(nameof(DefaultFiles), nameof(RotateFloatValues), DefaultPixelType)]
        public void ImageShouldRotate<TPixel>(TestImageProvider<TPixel> provider, float value)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Rotate(value)
                    .DebugSave(provider, value, Extensions.Bmp);
            }
        }

        [Theory]
        [WithFileCollection(nameof(DefaultFiles), nameof(RotateEnumValues), DefaultPixelType)]
        public void ImageShouldRotateEnum<TPixel>(TestImageProvider<TPixel> provider, RotateType value)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Rotate(value)
                    .DebugSave(provider, value, Extensions.Bmp);
            }
        }
    }
}