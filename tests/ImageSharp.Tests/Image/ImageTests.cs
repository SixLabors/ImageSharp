// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    /// <summary>
    /// Tests the <see cref="Image"/> class.
    /// </summary>
    public class ImageTests : FileTestBase
    {
        [Fact]
        public void ConstructorByteArray()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                Image.Load<Rgba32>((byte[])null);
            });

            TestFile file = TestFile.Create(TestImages.Bmp.Car);
            using (Image<Rgba32> image = Image.Load<Rgba32>(file.Bytes))
            {
                Assert.Equal(600, image.Width);
                Assert.Equal(450, image.Height);
            }
        }

        [Fact]
        public void ConstructorFileSystem()
        {
            TestFile file = TestFile.Create(TestImages.Bmp.Car);
            using (Image<Rgba32> image = Image.Load<Rgba32>(file.FullPath))
            {
                Assert.Equal(600, image.Width);
                Assert.Equal(450, image.Height);
            }
        }

        [Fact]
        public void ConstructorFileSystem_FileNotFound()
        {
            System.IO.FileNotFoundException ex = Assert.Throws<System.IO.FileNotFoundException>(
                () =>
                {
                    Image.Load<Rgba32>(Guid.NewGuid().ToString());
                });
        }

        [Fact]
        public void ConstructorFileSystem_NullPath()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
                () =>
                {
                    Image.Load<Rgba32>((string)null);
                });
        }

        [Fact]
        public void Save_DetecedEncoding()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageTests));
            string file = System.IO.Path.Combine(dir, "Save_DetecedEncoding.png");

            using (Image<Rgba32> image = new Image<Rgba32>(10, 10))
            {
                image.Save(file);
            }

            using (Image<Rgba32> img = Image.Load(file, out var mime))
            {
                Assert.Equal("image/png", mime.DefaultMimeType);
            }
        }

        [Fact]
        public void Save_WhenExtensionIsUnknown_Throws()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageTests));
            string file = System.IO.Path.Combine(dir, "Save_UnknownExtensionsEncoding_Throws.tmp");

            NotSupportedException ex = Assert.Throws<NotSupportedException>(
                () =>
                    {
                        using (Image<Rgba32> image = new Image<Rgba32>(10, 10))
                        {
                            image.Save(file);
                        }
                    });
        }

        [Fact]
        public void Save_SetEncoding()
        {
            string dir = TestEnvironment.CreateOutputDirectory(nameof(ImageTests));
            string file = System.IO.Path.Combine(dir, "Save_SetEncoding.dat");

            using (Image<Rgba32> image = new Image<Rgba32>(10, 10))
            {
                image.Save(file, new PngEncoder());
            }
            using (Image<Rgba32> img = Image.Load(file, out var mime))
            {
                Assert.Equal("image/png", mime.DefaultMimeType);
            }
        }

        [Theory]
        [WithTestPatternImages(10, 10, PixelTypes.Rgba32)]
        public void CloneFrame<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> img = provider.GetImage())
            {
                img.Frames.Add(new ImageFrame<TPixel>(10, 10));// add a frame anyway
                using (Image<TPixel> cloned = img.Clone(0))
                {
                    Assert.Equal(2, img.Frames.Count);
                    cloned.ComparePixelBufferTo(img.GetPixelSpan());
                }
            }
        }

        [Theory]
        [WithTestPatternImages(10, 10, PixelTypes.Rgba32)]
        public void CloneFrameAs<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> img = provider.GetImage())
            {
                img.Frames.Add(new ImageFrame<TPixel>(10, 10));// add a frame anyway
                using (Image<Bgra32> cloned = img.CloneAs<Bgra32>(0))
                {
                    for (var x = 0; x < img.Width; x++)
                    {
                        for (var y = 0; y < img.Height; y++)
                        {
                            Bgra32 pixelClone = cloned[x, y];
                            Bgra32 pixelSource = default(Bgra32);
                            img[x, y].ToBgra32(ref pixelSource);
                            Assert.Equal(pixelSource, pixelClone);
                        }
                    }
                    Assert.Equal(2, img.Frames.Count);
                }
            }
        }

        [Theory]
        [WithTestPatternImages(10, 10, PixelTypes.Rgba32)]
        public void ExtractFrame<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> img = provider.GetImage())
            {
                var sourcePixelData = img.GetPixelSpan().ToArray();

                img.Frames.Add(new ImageFrame<TPixel>(10, 10));
                using (Image<TPixel> cloned = img.Extract(0))
                {
                    Assert.Equal(1, img.Frames.Count);
                    cloned.ComparePixelBufferTo(sourcePixelData);
                }
            }
        }
    }
}
