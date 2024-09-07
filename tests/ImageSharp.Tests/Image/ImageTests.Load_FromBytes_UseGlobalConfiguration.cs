// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests;

public partial class ImageTests
{
    public class Load_FromBytes_UseGlobalConfiguration
    {
        private static byte[] ByteArray { get; } = TestFile.Create(TestImages.Bmp.Bit8).Bytes;

        private static Span<byte> ByteSpan => new(ByteArray);

        private static void VerifyDecodedImage(Image img) => Assert.Equal(new(127, 64), img.Size);

        [Fact]
        public void Bytes_Specific()
        {
            using Image<Rgba32> img = Image.Load<Rgba32>(ByteSpan);
            VerifyDecodedImage(img);
        }

        [Fact]
        public void Bytes_Agnostic()
        {
            using Image img = Image.Load(ByteSpan);
            VerifyDecodedImage(img);
        }

        [Fact]
        public void Bytes_OutFormat_Specific()
        {
            using Image<Rgba32> img = Image.Load<Rgba32>(ByteSpan);
            VerifyDecodedImage(img);
            Assert.IsType<BmpFormat>(img.Metadata.DecodedImageFormat);
        }

        [Fact]
        public void Bytes_OutFormat_Agnostic()
        {
            using Image img = Image.Load(ByteSpan);
            VerifyDecodedImage(img);
            Assert.IsType<BmpFormat>(img.Metadata.DecodedImageFormat);
        }
    }
}
