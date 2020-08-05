// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.PixelFormats;

using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    public partial class ImageTests
    {
        public class Load_FileSystemPath_UseDefaultConfiguration
        {
            private string Path { get; } = TestFile.GetInputFileFullPath(TestImages.Bmp.Bit8);

            private static void VerifyDecodedImage(Image img)
            {
                Assert.Equal(new Size(127, 64), img.Size());
            }

            [Fact]
            public void Path_Specific()
            {
                using var img = Image.Load<Rgba32>(this.Path);
                VerifyDecodedImage(img);
            }

            [Fact]
            public void Path_Agnostic()
            {
                using var img = Image.Load(this.Path);
                VerifyDecodedImage(img);
            }

            [Fact]
            public async Task Path_Agnostic_Async()
            {
                using var img = await Image.LoadAsync(this.Path);
                VerifyDecodedImage(img);
            }

            [Fact]
            public async Task Path_Specific_Async()
            {
                using var img = await Image.LoadAsync<Rgb24>(this.Path);
                VerifyDecodedImage(img);
            }

            [Fact]
            public async Task Path_Agnostic_Configuration_Async()
            {
                using var img = await Image.LoadAsync(this.Path);
                VerifyDecodedImage(img);
            }

            [Fact]
            public void Path_Decoder_Specific()
            {
                using var img = Image.Load<Rgba32>(this.Path, new BmpDecoder());
                VerifyDecodedImage(img);
            }

            [Fact]
            public void Path_Decoder_Agnostic()
            {
                using var img = Image.Load(this.Path, new BmpDecoder());
                VerifyDecodedImage(img);
            }

            [Fact]
            public async Task Path_Decoder_Agnostic_Async()
            {
                using var img = await Image.LoadAsync(this.Path, new BmpDecoder());
                VerifyDecodedImage(img);
            }

            [Fact]
            public async Task Path_Decoder_Specific_Async()
            {
                using var img = await Image.LoadAsync<Rgb24>(this.Path, new BmpDecoder());
                VerifyDecodedImage(img);
            }

            [Fact]
            public void Path_OutFormat_Specific()
            {
                using var img = Image.Load<Rgba32>(this.Path, out IImageFormat format);
                VerifyDecodedImage(img);
                Assert.IsType<BmpFormat>(format);
            }

            [Fact]
            public void Path_OutFormat_Agnostic()
            {
                using var img = Image.Load(this.Path, out IImageFormat format);
                VerifyDecodedImage(img);
                Assert.IsType<BmpFormat>(format);
            }

            [Fact]
            public void WhenFileNotFound_Throws()
            {
                Assert.Throws<System.IO.FileNotFoundException>(
                    () =>
                        {
                            Image.Load<Rgba32>(Guid.NewGuid().ToString());
                        });
            }

            [Fact]
            public void WhenPathIsNull_Throws()
            {
                Assert.Throws<ArgumentNullException>(
                    () =>
                        {
                            Image.Load<Rgba32>((string)null);
                        });
            }

            [Fact]
            public Task Async_WhenFileNotFound_Throws()
            {
                return Assert.ThrowsAsync<System.IO.FileNotFoundException>(
                    () => Image.LoadAsync<Rgba32>(Guid.NewGuid().ToString()));
            }

            [Fact]
            public Task Async_WhenPathIsNull_Throws()
            {
                return Assert.ThrowsAsync<ArgumentNullException>(
                    () => Image.LoadAsync<Rgba32>((string)null));
            }
        }
    }
}
