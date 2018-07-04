// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Transforms
{
    using SixLabors.ImageSharp.Processing;

    public class RotateFlipTests : FileTestBase
    {
        public static readonly string[] FlipFiles = { TestImages.Bmp.F };

        public static readonly TheoryData<RotateMode, FlipMode> RotateFlipValues
            = new TheoryData<RotateMode, FlipMode>
        {
            { RotateMode.None, FlipMode.Vertical },
            { RotateMode.None, FlipMode.Horizontal },
            { RotateMode.Rotate90, FlipMode.None },
            { RotateMode.Rotate180, FlipMode.None },
            { RotateMode.Rotate270, FlipMode.None },
        };

        [Theory]
        [WithTestPatternImages(nameof(RotateFlipValues), 100, 50, DefaultPixelType)]
        [WithTestPatternImages(nameof(RotateFlipValues), 50, 100, DefaultPixelType)]
        public void RotateFlip<TPixel>(TestImageProvider<TPixel> provider, RotateMode rotateType, FlipMode flipType)
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