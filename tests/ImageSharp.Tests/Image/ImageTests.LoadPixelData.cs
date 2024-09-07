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
    }
}
