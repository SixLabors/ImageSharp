// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

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
        public class Load_FromStream_UseDefaultConfiguration
        {
            [Fact]
            public void Stream_Specific()
            {
                byte[] data = TestFile.Create(TestImages.Bmp.F).Bytes;

                using (var stream = new MemoryStream(data))
                using (var img = Image.Load<Rgba32>(stream))
                {
                    Assert.Equal(new Size(108, 202), img.Size());
                }
            }
            
            [Fact]
            public void Stream_Agnostic()
            {
                byte[] data = TestFile.Create(TestImages.Bmp.F).Bytes;

                using (var stream = new MemoryStream(data))
                using (var img = Image.Load(stream))
                {
                    Assert.Equal(new Size(108, 202), img.Size());
                }
            }
            
            [Fact]
            public void Stream_OutFormat_Specific()
            {
                byte[] data = TestFile.Create(TestImages.Bmp.F).Bytes;


                using (var stream = new MemoryStream(data))
                using (var img = Image.Load<Rgba32>(stream, out IImageFormat format))
                {
                    Assert.Equal(new Size(108, 202), img.Size());
                    Assert.IsType<BmpFormat>(format);
                }
            }
            
            [Fact]
            public void Stream_Decoder_Specific()
            {
                byte[] data = TestFile.Create(TestImages.Bmp.F).Bytes;

                using (var stream = new MemoryStream(data))
                using (var img = Image.Load<Rgba32>(stream, new BmpDecoder()))
                {
                    Assert.Equal(new Size(108, 202), img.Size());
                }
            }
            
            [Fact]
            public void Stream_Decoder_Agnostic()
            {
                byte[] data = TestFile.Create(TestImages.Bmp.F).Bytes;

                using (var stream = new MemoryStream(data))
                using (var img = Image.Load(stream, new BmpDecoder()))
                {
                    Assert.Equal(new Size(108, 202), img.Size());
                }
            }
            
            [Fact]
            public void Stream_OutFormat_Agnostic()
            {
                byte[] data = TestFile.Create(TestImages.Bmp.F).Bytes;

                using (var stream = new MemoryStream(data))
                using (var img = Image.Load(stream, out IImageFormat format))
                {
                    Assert.Equal(new Size(108, 202), img.Size());
                    Assert.IsType<BmpFormat>(format);
                }
            }
        }
    }
}