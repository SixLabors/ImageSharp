// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    public partial class ImageTests
    {
        public class Load_FromStream_UseDefaultConfiguration : IDisposable
        {
            private static readonly byte[] Data = TestFile.Create(TestImages.Bmp.Bit8).Bytes;
            
            private MemoryStream Stream { get; } = new MemoryStream(Data);
            
            private static void VerifyDecodedImage(Image img)
            {
                Assert.Equal(new Size(127, 64), img.Size());
            }
            
            [Fact]
            public void Stream_Specific()
            {
                using (var img = Image.Load<Rgba32>(this.Stream))
                {
                    VerifyDecodedImage(img);
                }
            }
            
            [Fact]
            public void Stream_Agnostic()
            {
                using (var img = Image.Load(this.Stream))
                {
                    VerifyDecodedImage(img);
                }
            }
            
            [Fact]
            public void Stream_OutFormat_Specific()
            {
                using (var img = Image.Load<Rgba32>(this.Stream, out IImageFormat format))
                {
                    VerifyDecodedImage(img);
                    Assert.IsType<BmpFormat>(format);
                }
            }
            
            [Fact]
            public void Stream_Decoder_Specific()
            {
                using (var img = Image.Load<Rgba32>(this.Stream, new BmpDecoder()))
                {
                    VerifyDecodedImage(img);
                }
            }
            
            [Fact]
            public void Stream_Decoder_Agnostic()
            {
                using (var img = Image.Load(this.Stream, new BmpDecoder()))
                {
                    VerifyDecodedImage(img);
                }
            }
            
            [Fact]
            public void Stream_OutFormat_Agnostic()
            {
                using (var img = Image.Load(this.Stream, out IImageFormat format))
                {
                    VerifyDecodedImage(img);
                    Assert.IsType<BmpFormat>(format);
                }
            }

            public void Dispose()
            {
                this.Stream?.Dispose();
            }
        }
    }
}