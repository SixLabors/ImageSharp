// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.MetaData;
using SixLabors.Primitives;

using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    public class ImageInfoTests
    {
        [Fact]
        public void ImageInfoInitializesCorrectly()
        {
            const int Width = 50;
            const int Height = 60;
            var size = new Size(Width, Height);
            var rectangle = new Rectangle(0, 0, Width, Height);
            var pixelType = new PixelTypeInfo(8);
            var meta = new ImageMetaData();

            var info = new ImageInfo(pixelType, Width, Height, meta);

            Assert.Equal(pixelType, info.PixelType);
            Assert.Equal(Width, info.Width);
            Assert.Equal(Height, info.Height);
            Assert.Equal(size, info.Size());
            Assert.Equal(rectangle, info.Bounds());
            Assert.Equal(meta, info.MetaData);
        }
    }
}
