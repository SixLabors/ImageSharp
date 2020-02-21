// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    public class LargeImageIntegrationTests
    {
        [Theory(Skip = "For local testing only.")]
        [WithBasicTestPatternImages(width: 30000, height: 30000, PixelTypes.Rgba32)]
        public void CreateAndResize(TestImageProvider<Rgba32> provider)
        {
            using Image<Rgba32> image = provider.GetImage();
            image.Mutate(c => c.Resize(1000, 1000));
            image.DebugSave(provider);
        }
    }
}
