// // Copyright (c) Six Labors and contributors.
// // Licensed under the Apache License, Version 2.0.

using System;

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

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
                using (var img = Image.Load<Rgba32>(this.Path))
                {
                    VerifyDecodedImage(img);
                }
            }
            
            [Fact]
            public void Path_Agnostic()
            {
                using (var img = Image.Load(this.Path))
                {
                    VerifyDecodedImage(img);
                }
            }
            
            
            [Fact]
            public void Path_Decoder_Specific()
            {
                using (var img = Image.Load<Rgba32>(this.Path, new BmpDecoder()))
                {
                    VerifyDecodedImage(img);
                }
            }
            
            [Fact]
            public void Path_Decoder_Agnostic()
            {
                using (var img = Image.Load(this.Path, new BmpDecoder()))
                {
                    VerifyDecodedImage(img);
                }
            }
            
            [Fact]
            public void Path_OutFormat_Specific()
            {
                using (var img = Image.Load<Rgba32>(this.Path, out IImageFormat format))
                {
                    VerifyDecodedImage(img);
                    Assert.IsType<BmpFormat>(format);
                }
            }

            [Fact]
            public void Path_OutFormat_Agnostic()
            {
                using (var img = Image.Load(this.Path, out IImageFormat format))
                {
                    VerifyDecodedImage(img);
                    Assert.IsType<BmpFormat>(format);
                }
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
        }
    }
}
