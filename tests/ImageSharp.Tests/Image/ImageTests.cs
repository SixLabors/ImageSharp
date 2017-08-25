// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
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
    }
}
