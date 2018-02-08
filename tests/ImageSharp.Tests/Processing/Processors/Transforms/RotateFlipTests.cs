// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Transforms
{
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
        [WithTestPatternImages(nameof(RotateFlipValues), 100, 50, DefaultPixelType)]
        [WithTestPatternImages(nameof(RotateFlipValues), 50, 100, DefaultPixelType)]
        public void RotateFlip<TPixel>(TestImageProvider<TPixel> provider, RotateType rotateType, FlipType flipType)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.RotateFlip(rotateType, flipType));
                image.DebugSave(provider, string.Join("_", rotateType, flipType));
            }
        }
    }
}