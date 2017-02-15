// <copyright file="PixelAccessorTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System;

    using ImageSharp.Formats;

    using Xunit;

    /// <summary>
    /// Tests the <see cref="Image"/> class.
    /// </summary>
    public class ImageTests
    {
        [Fact]
        public void ConstructorByteArray()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                new Image((byte[])null);
            });

            TestFile file = TestFile.Create(TestImages.Bmp.Car);
            using (Image image = new Image(file.Bytes))
            {
                Assert.Equal(600, image.Width);
                Assert.Equal(450, image.Height);
            }
        }

        [Fact]
        public void ConstructorFileSystem()
        {
            TestFile file = TestFile.Create(TestImages.Bmp.Car);
            using (Image image = new Image(file.FilePath))
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
                    new Image(Guid.NewGuid().ToString());
                });
        }

        [Fact]
        public void ConstructorFileSystem_NullPath()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
                () =>
                {
                    new Image(null);
                });
        }

        [Fact]
        public void Save_DetecedEncoding()
        {
            string file = TestFile.GetPath("../../TestOutput/Save_DetecedEncoding.png");
            var dir = System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file));
            using (Image image = new Image(10, 10))
            {
                image.Save(file);
            }

            var c = TestFile.Create("../../TestOutput/Save_DetecedEncoding.png");
            using (var img = c.CreateImage())
            {
                Assert.IsType<PngFormat>(img.CurrentImageFormat);
            }
        }

        [Fact]
        public void Save_UnknownExtensionsEncoding()
        {
            string file = TestFile.GetPath("../../TestOutput/Save_DetecedEncoding.tmp");
            var ex = Assert.Throws<InvalidOperationException>(
                () =>
                    {
                        using (Image image = new Image(10, 10))
                        {
                            image.Save(file);
                        }
                    });
        }

        [Fact]
        public void Save_SetFormat()
        {
            string file = TestFile.GetPath("../../TestOutput/Save_SetFormat.dat");
            var dir = System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file));
            using (Image image = new Image(10, 10))
            {
                image.Save(file, new PngFormat());
            }

            var c = TestFile.Create("../../TestOutput/Save_SetFormat.dat");
            using (var img = c.CreateImage())
            {
                Assert.IsType<PngFormat>(img.CurrentImageFormat);
            }
        }

        [Fact]
        public void Save_SetEncoding()
        {
            string file = TestFile.GetPath("../../TestOutput/Save_SetEncoding.dat");
            var dir = System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file));
            using (Image image = new Image(10, 10))
            {
                image.Save(file, new PngEncoder());
            }

            var c = TestFile.Create("../../TestOutput/Save_SetEncoding.dat");
            using (var img = c.CreateImage())
            {
                Assert.IsType<PngFormat>(img.CurrentImageFormat);
            }
        }
    }
}
