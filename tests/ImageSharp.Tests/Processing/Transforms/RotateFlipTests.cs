// <copyright file="RotateFlipTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Processing.Transforms
{
    using ImageSharp.PixelFormats;
    using ImageSharp.Processing;

    using Xunit;

    public class RotateFlipTests : FileTestBase
    {
        public static readonly string[] FlipFiles = { TestImages.Bmp.F };

        public static readonly TheoryData<RotateType, FlipType> RotateFlipValues
            = new TheoryData<RotateType, FlipType>
        {
            { RotateType.None, FlipType.Vertical },
            { RotateType.None, FlipType.Horizontal },
            { RotateType.Rotate90, FlipType.None },
            { RotateType.Rotate180, FlipType.None },
            { RotateType.Rotate270, FlipType.None },
        };

        [Theory]
        [WithFileCollection(nameof(FlipFiles), nameof(RotateFlipValues), DefaultPixelType)]
        public void ImageShouldRotateFlip<TPixel>(TestImageProvider<TPixel> provider, RotateType rotateType, FlipType flipType)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.RotateFlip(rotateType, flipType)
                    .DebugSave(provider, string.Join("_", rotateType, flipType));
            }
        }
    }
}