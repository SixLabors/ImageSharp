// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests;

public partial class ImageTests
{
    public class LoadPixelData
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [ValidateDisposedMemoryAllocations]
        public void FromPixels(bool useSpan)
        {
            Rgba32[] data = [Color.Black.ToPixel<Rgba32>(), Color.White.ToPixel<Rgba32>(), Color.White.ToPixel<Rgba32>(), Color.Black.ToPixel<Rgba32>()
            ];

            using Image<Rgba32> img = useSpan
                ? Image.LoadPixelData<Rgba32>(data.AsSpan(), 2, 2)
                : Image.LoadPixelData<Rgba32>(data, 2, 2);
            Assert.NotNull(img);
            Assert.Equal(Color.Black, Color.FromPixel(img[0, 0]));
            Assert.Equal(Color.White, Color.FromPixel(img[0, 1]));

            Assert.Equal(Color.White, Color.FromPixel(img[1, 0]));
            Assert.Equal(Color.Black, Color.FromPixel(img[1, 1]));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void FromBytes(bool useSpan)
        {
            byte[] data =
            [
                0, 0, 0, 255, // 0,0
                255, 255, 255, 255, // 0,1
                255, 255, 255, 255, // 1,0
                0, 0, 0, 255 // 1,1
            ];

            using Image<Rgba32> img = useSpan
                ? Image.LoadPixelData<Rgba32>(data.AsSpan(), 2, 2)
                : Image.LoadPixelData<Rgba32>(data, 2, 2);
            Assert.NotNull(img);
            Assert.Equal(Color.Black, Color.FromPixel(img[0, 0]));
            Assert.Equal(Color.White, Color.FromPixel(img[0, 1]));

            Assert.Equal(Color.White, Color.FromPixel(img[1, 0]));
            Assert.Equal(Color.Black, Color.FromPixel(img[1, 1]));
        }

        [Fact]
        public void FromPixels_WithRowStride()
        {
            Rgba32[] data =
            [
                Color.Black.ToPixel<Rgba32>(),
                Color.White.ToPixel<Rgba32>(),
                Color.Red.ToPixel<Rgba32>(), // padding
                Color.White.ToPixel<Rgba32>(),
                Color.Black.ToPixel<Rgba32>(),
                Color.Red.ToPixel<Rgba32>() // padding
            ];

            using Image<Rgba32> img = Image.LoadPixelData<Rgba32>(data.AsSpan(), width: 2, height: 2, rowStride: 3);
            Assert.NotNull(img);
            Assert.Equal(Color.Black, Color.FromPixel(img[0, 0]));
            Assert.Equal(Color.White, Color.FromPixel(img[0, 1]));
            Assert.Equal(Color.White, Color.FromPixel(img[1, 0]));
            Assert.Equal(Color.Black, Color.FromPixel(img[1, 1]));
        }

        [Fact]
        public void FromBytes_WithRowStrideInBytes()
        {
            byte[] data =
            [
                0, 0, 0, 255, // 0,0
                255, 255, 255, 255, // 0,1
                255, 0, 0, 255, // padding
                255, 255, 255, 255, // 1,0
                0, 0, 0, 255, // 1,1
                255, 0, 0, 255 // padding
            ];

            using Image<Rgba32> img = Image.LoadPixelData<Rgba32>(data.AsSpan(), width: 2, height: 2, rowStrideInBytes: 12);
            Assert.NotNull(img);
            Assert.Equal(Color.Black, Color.FromPixel(img[0, 0]));
            Assert.Equal(Color.White, Color.FromPixel(img[0, 1]));
            Assert.Equal(Color.White, Color.FromPixel(img[1, 0]));
            Assert.Equal(Color.Black, Color.FromPixel(img[1, 1]));
        }

        [Fact]
        public void FromPixels_WithRowStride_InvalidStride_Throws()
        {
            Rgba32[] data = new Rgba32[6];
            Assert.ThrowsAny<ArgumentOutOfRangeException>(
                () => Image.LoadPixelData<Rgba32>(data.AsSpan(), width: 2, height: 2, rowStride: 1));
        }

        [Fact]
        public void FromPixels_WithRowStride_InvalidLength_Throws()
        {
            Rgba32[] data = new Rgba32[4];
            Assert.ThrowsAny<ArgumentException>(
                () => Image.LoadPixelData<Rgba32>(data.AsSpan(), width: 2, height: 2, rowStride: 3));
        }

        [Fact]
        public void FromBytes_WithRowStrideInBytes_InvalidStride_Throws()
        {
            byte[] data = new byte[24];
            Assert.ThrowsAny<ArgumentException>(
                () => Image.LoadPixelData<Rgba32>(data.AsSpan(), width: 2, height: 2, rowStrideInBytes: 10));
        }

        [Fact]
        public void FromBytes_WithRowStrideInBytes_InvalidLength_Throws()
        {
            byte[] data = new byte[19];
            Assert.ThrowsAny<ArgumentException>(
                () => Image.LoadPixelData<Rgba32>(data.AsSpan(), width: 2, height: 2, rowStrideInBytes: 12));
        }
    }
}
