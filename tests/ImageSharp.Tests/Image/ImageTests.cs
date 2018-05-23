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
    public class ImageTests
    {
        public class Constructor
        {
            [Fact]
            public void Width_Height()
            {
                using (var image = new Image<Rgba32>(11, 23))
                {
                    Assert.Equal(11, image.Width);
                    Assert.Equal(23, image.Height);
                    Assert.Equal(11*23, image.GetPixelSpan().Length);
                    image.ComparePixelBufferTo(default(Rgba32));

                    Assert.Equal(Configuration.Default, image.GetConfiguration());
                }
            }

            [Fact]
            public void Configuration_Width_Height()
            {
                Configuration configuration = Configuration.Default.ShallowCopy();

                using (var image = new Image<Rgba32>(configuration, 11, 23))
                {
                    Assert.Equal(11, image.Width);
                    Assert.Equal(23, image.Height);
                    Assert.Equal(11 * 23, image.GetPixelSpan().Length);
                    image.ComparePixelBufferTo(default(Rgba32));

                    Assert.Equal(configuration, image.GetConfiguration());
                }
            }

            [Fact]
            public void Configuration_Width_Height_BackroundColor()
            {
                Configuration configuration = Configuration.Default.ShallowCopy();
                Rgba32 color = Rgba32.Aquamarine;

                using (var image = new Image<Rgba32>(configuration, 11, 23, color))
                {
                    Assert.Equal(11, image.Width);
                    Assert.Equal(23, image.Height);
                    Assert.Equal(11 * 23, image.GetPixelSpan().Length);
                    image.ComparePixelBufferTo(color);

                    Assert.Equal(configuration, image.GetConfiguration());
                }
            }
        }

        [Fact]
        public void Load_ByteArray()
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
        public void Load_FileSystemPath()
        {
            TestFile file = TestFile.Create(TestImages.Bmp.Car);
            using (Image<Rgba32> image = Image.Load<Rgba32>(file.FullPath))
            {
                Assert.Equal(600, image.Width);
                Assert.Equal(450, image.Height);
            }
        }

        [Fact]
        public void Load_FileSystemPath_FileNotFound()
        {
            System.IO.FileNotFoundException ex = Assert.Throws<System.IO.FileNotFoundException>(
                () =>
                {
                    Image.Load<Rgba32>(Guid.NewGuid().ToString());
                });
        }

        [Fact]
        public void Load_FileSystemPath_NullPath()
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
