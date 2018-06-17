// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
// ReSharper disable InconsistentNaming

namespace SixLabors.ImageSharp.Tests
{
    public partial class ImageTests
    {
        public class Load_BasicCases
        {
            [Fact]
            public void ByteArray()
            {
                Assert.Throws<ArgumentNullException>(() =>
                    {
                        Image.Load<Rgba32>((byte[])null);
                    });

                var file = TestFile.Create(TestImages.Bmp.Car);
                using (var image = Image.Load<Rgba32>(file.Bytes))
                {
                    Assert.Equal(600, image.Width);
                    Assert.Equal(450, image.Height);
                }
            }

            [Fact]
            public void FileSystemPath()
            {
                var file = TestFile.Create(TestImages.Bmp.Car);
                using (var image = Image.Load<Rgba32>(file.FullPath))
                {
                    Assert.Equal(600, image.Width);
                    Assert.Equal(450, image.Height);
                }
            }

            [Fact]
            public void FileSystemPath_FileNotFound()
            {
                System.IO.FileNotFoundException ex = Assert.Throws<System.IO.FileNotFoundException>(
                    () =>
                        {
                            Image.Load<Rgba32>(Guid.NewGuid().ToString());
                        });
            }

            [Fact]
            public void FileSystemPath_NullPath()
            {
                ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
                    () =>
                        {
                            Image.Load<Rgba32>((string)null);
                        });
            }
        }
    }
}