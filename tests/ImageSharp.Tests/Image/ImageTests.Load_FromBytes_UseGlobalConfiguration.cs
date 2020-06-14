// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.PixelFormats;

using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    public partial class ImageTests
    {
        public class Load_FromBytes_UseGlobalConfiguration
        {
            private static byte[] ByteArray { get; } = TestFile.Create(TestImages.Bmp.Bit8).Bytes;

            private static Span<byte> ByteSpan => new Span<byte>(ByteArray);

            private static void VerifyDecodedImage(Image img)
            {
                Assert.Equal(new Size(127, 64), img.Size());
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void Bytes_Specific(bool useSpan)
            {
                using (var img = useSpan ? Image.Load<Rgba32>(ByteSpan) : Image.Load<Rgba32>(ByteArray))
                {
                    VerifyDecodedImage(img);
                }
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void Bytes_Agnostic(bool useSpan)
            {
                using (var img = useSpan ? Image.Load(ByteSpan) : Image.Load(ByteArray))
                {
                    VerifyDecodedImage(img);
                }
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void Bytes_Decoder_Specific(bool useSpan)
            {
                using (var img = useSpan ? Image.Load<Rgba32>(ByteSpan, new BmpDecoder()) : Image.Load<Rgba32>(ByteArray, new BmpDecoder()))
                {
                    VerifyDecodedImage(img);
                }
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void Bytes_Decoder_Agnostic(bool useSpan)
            {
                using (var img = useSpan ? Image.Load(ByteSpan, new BmpDecoder()) : Image.Load(ByteArray, new BmpDecoder()))
                {
                    VerifyDecodedImage(img);
                }
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void Bytes_OutFormat_Specific(bool useSpan)
            {
                IImageFormat format;
                using (var img = useSpan ? Image.Load<Rgba32>(ByteSpan, out format) : Image.Load<Rgba32>(ByteArray, out format))
                {
                    VerifyDecodedImage(img);
                    Assert.IsType<BmpFormat>(format);
                }
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public void Bytes_OutFormat_Agnostic(bool useSpan)
            {
                IImageFormat format;
                using (var img = useSpan ? Image.Load(ByteSpan, out format) : Image.Load(ByteArray, out format))
                {
                    VerifyDecodedImage(img);
                    Assert.IsType<BmpFormat>(format);
                }
            }
        }
    }
}
