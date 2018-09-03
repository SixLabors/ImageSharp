// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.MetaData;
using SixLabors.Primitives;

using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    public class ImageInfoTests
    {
        [Fact]
        public void JpegInfoInitializesCorrectly()
        {
            const int Width = 50;
            const int Height = 60;
            var size = new Size(Width, Height);
            var rectangle = new Rectangle(0, 0, Width, Height);
            var pixelType = new PixelTypeInfo(8);
            var meta = new ImageMetaData();

            var info = new JpegInfo(pixelType, size, meta);

            Assert.Equal(pixelType, info.PixelType);
            Assert.Equal(Width, info.Width);
            Assert.Equal(Height, info.Height);
            Assert.Equal(size, info.Size());
            Assert.Equal(rectangle, info.Bounds());
            Assert.Equal(meta, info.MetaData);
        }

        [Fact]
        public void BmpInfoInitializesCorrectly()
        {
            const int Width = 50;
            const int Height = 60;
            var size = new Size(Width, Height);
            var rectangle = new Rectangle(0, 0, Width, Height);
            var pixelType = new PixelTypeInfo(8);
            var meta = new ImageMetaData();

            var info = new BmpInfo(pixelType, size, meta);

            Assert.Equal(pixelType, info.PixelType);
            Assert.Equal(Width, info.Width);
            Assert.Equal(Height, info.Height);
            Assert.Equal(size, info.Size());
            Assert.Equal(rectangle, info.Bounds());
            Assert.Equal(meta, info.MetaData);
        }

        [Fact]
        public void PngInfoInitializesCorrectly()
        {
            const int Width = 50;
            const int Height = 60;
            var size = new Size(Width, Height);
            var rectangle = new Rectangle(0, 0, Width, Height);
            var pixelType = new PixelTypeInfo(8);
            var meta = new ImageMetaData();

            var info = new PngInfo(pixelType, size, meta);

            Assert.Equal(pixelType, info.PixelType);
            Assert.Equal(Width, info.Width);
            Assert.Equal(Height, info.Height);
            Assert.Equal(size, info.Size());
            Assert.Equal(rectangle, info.Bounds());
            Assert.Equal(meta, info.MetaData);
        }

        [Fact]
        public void GifInfoInitializesCorrectly()
        {
            const GifColorTableMode mode = GifColorTableMode.Local;
            const int Width = 50;
            const int Height = 60;
            var size = new Size(Width, Height);
            var rectangle = new Rectangle(0, 0, Width, Height);
            var pixelType = new PixelTypeInfo(8);
            var meta = new ImageMetaData();

            var info = new GifInfo(mode, pixelType, size, meta);

            Assert.Equal(mode, info.ColorTableMode);
            Assert.Equal(pixelType, info.PixelType);
            Assert.Equal(Width, info.Width);
            Assert.Equal(Height, info.Height);
            Assert.Equal(size, info.Size());
            Assert.Equal(rectangle, info.Bounds());
            Assert.Equal(meta, info.MetaData);
        }
    }
}
