// <copyright file="RotateTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

// ReSharper disable InconsistentNaming
namespace ImageSharp.Tests.Processing.Processors.Transforms
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
        [WithTestPatternImages(nameof(RotateFloatValues), 100, 50, DefaultPixelType)]
        [WithTestPatternImages(nameof(RotateFloatValues), 50, 100, DefaultPixelType)]
        public void Rotate<TPixel>(TestImageProvider<TPixel> provider, float value)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.Rotate(value));
                image.DebugSave(provider, value);
            }
        }

        [Theory]
        [WithTestPatternImages(nameof(RotateEnumValues), 100, 50, DefaultPixelType)]
        [WithTestPatternImages(nameof(RotateEnumValues), 50, 100, DefaultPixelType)]
        public void Rotate_WithRotateTypeEnum<TPixel>(TestImageProvider<TPixel> provider, RotateType value)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.Rotate(value));
                image.DebugSave(provider, value);
            }
        }
    }
}