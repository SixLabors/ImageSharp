// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
// ReSharper disable InconsistentNaming

namespace SixLabors.ImageSharp.Tests
{
    using Moq;

    using SixLabors.ImageSharp.IO;

    public partial class ImageTests
    {
        public class Load_FileSystemPath : ImageLoadTestBase
        {
            [Fact]
            public void BasicCase()
            {
                var img = Image.Load<Rgba32>(this.TopLevelConfiguration, this.MockFilePath);

                Assert.NotNull(img);

                this.TestFormat.VerifyDecodeCall(this.Marker, this.TopLevelConfiguration);
            }

            [Fact]
            public void UseLocalConfiguration()
            {
                var img = Image.Load<Rgba32>(this.LocalConfiguration, this.MockFilePath);

                Assert.NotNull(img);

                this.localDecoder.Verify(x => x.Decode<Rgba32>(this.LocalConfiguration, this.DataStream));
            }

            [Fact]
            public void UseCustomDecoder()
            {
                var img = Image.Load<Rgba32>(this.TopLevelConfiguration, this.MockFilePath, this.localDecoder.Object);

                Assert.NotNull(img);
                this.localDecoder.Verify(x => x.Decode<Rgba32>(this.TopLevelConfiguration, this.DataStream));
            }


            [Fact]
            public void UseGlobalConfigration()
            {
                var file = TestFile.Create(TestImages.Bmp.Car);
                using (var image = Image.Load<Rgba32>(file.FullPath))
                {
                    Assert.Equal(600, image.Width);
                    Assert.Equal(450, image.Height);
                }
            }

            [Fact]
            public void WhenFileNotFound_Throws()
            {
                System.IO.FileNotFoundException ex = Assert.Throws<System.IO.FileNotFoundException>(
                    () =>
                    {
                        Image.Load<Rgba32>(Guid.NewGuid().ToString());
                    });
            }

            [Fact]
            public void WhenPathIsNull_Throws()
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