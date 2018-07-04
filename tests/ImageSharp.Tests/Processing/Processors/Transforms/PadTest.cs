// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Transforms
{
    public class PadTest : FileTestBase
    {
        [Theory]
        [WithFileCollection(nameof(DefaultFiles), DefaultPixelType)]
        public void ImageShouldPad<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.Pad(image.Width + 50, image.Height + 50));
                image.DebugSave(provider);

                // Check pixels are empty
                for (int y = 0; y < 25; y++)
                {
                    for (int x = 0; x < 25; x++)
                    {
                        Assert.Equal(default(TPixel), image[x, y]);
                    }
                }
            }
        }
    }
}