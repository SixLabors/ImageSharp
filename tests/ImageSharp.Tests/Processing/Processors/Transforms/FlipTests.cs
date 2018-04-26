// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Transforms
{
    using SixLabors.ImageSharp.Processing.Transforms;

    public class FlipTests : FileTestBase
    {
        public static readonly string[] FlipFiles = { TestImages.Bmp.F };

        public static readonly TheoryData<FlipMode> FlipValues
            = new TheoryData<FlipMode>
        {
            { FlipMode.None },
            { FlipMode.Vertical },
            { FlipMode.Horizontal },
        };

        [Theory]
        [WithFileCollection(nameof(FlipFiles), nameof(FlipValues), DefaultPixelType)]
        public void ImageShouldFlip<TPixel>(TestImageProvider<TPixel> provider, FlipMode flipType)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.Flip(flipType));
                image.DebugSave(provider, flipType);
            }
        }
    }
}