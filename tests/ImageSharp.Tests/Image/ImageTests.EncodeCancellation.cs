// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests;

public partial class ImageTests
{
    [ValidateDisposedMemoryAllocations]
    public class Encode_Cancellation
    {
        [Fact]
        public async Task Encode_PreCancellation_Bmp()
        {
            using Image<Rgba32> image = new(10, 10);
            await Assert.ThrowsAsync<TaskCanceledException>(
                async () => await image.SaveAsBmpAsync(Stream.Null, new(canceled: true)));
        }

        [Fact]
        public async Task Encode_PreCancellation_Cur()
        {
            using Image<Rgba32> image = new(10, 10);
            await Assert.ThrowsAsync<TaskCanceledException>(
                async () => await image.SaveAsCurAsync(Stream.Null, new(canceled: true)));
        }

        [Fact]
        public async Task Encode_PreCancellation_Gif()
        {
            using Image<Rgba32> image = new(10, 10);
            await Assert.ThrowsAsync<TaskCanceledException>(
                async () => await image.SaveAsGifAsync(Stream.Null, new(canceled: true)));
        }

        [Fact]
        public async Task Encode_PreCancellation_Animated_Gif()
        {
            using Image<Rgba32> image = new(10, 10);
            image.Frames.CreateFrame();

            await Assert.ThrowsAsync<TaskCanceledException>(
                async () => await image.SaveAsGifAsync(Stream.Null, new(canceled: true)));
        }

        [Fact]
        public async Task Encode_PreCancellation_Ico()
        {
            using Image<Rgba32> image = new(10, 10);
            await Assert.ThrowsAsync<TaskCanceledException>(
                async () => await image.SaveAsIcoAsync(Stream.Null, new(canceled: true)));
        }

        [Fact]
        public async Task Encode_PreCancellation_Jpeg()
        {
            using Image<Rgba32> image = new(10, 10);
            await Assert.ThrowsAsync<TaskCanceledException>(
                async () => await image.SaveAsJpegAsync(Stream.Null, new(canceled: true)));
        }

        [Fact]
        public async Task Encode_PreCancellation_Pbm()
        {
            using Image<Rgba32> image = new(10, 10);
            await Assert.ThrowsAsync<TaskCanceledException>(
                async () => await image.SaveAsPbmAsync(Stream.Null, new(canceled: true)));
        }

        [Fact]
        public async Task Encode_PreCancellation_Png()
        {
            using Image<Rgba32> image = new(10, 10);
            await Assert.ThrowsAsync<TaskCanceledException>(
                async () => await image.SaveAsPngAsync(Stream.Null, new(canceled: true)));
        }

        [Fact]
        public async Task Encode_PreCancellation_Animated_Png()
        {
            using Image<Rgba32> image = new(10, 10);
            image.Frames.CreateFrame();

            await Assert.ThrowsAsync<TaskCanceledException>(
                async () => await image.SaveAsPngAsync(Stream.Null, new(canceled: true)));
        }

        [Fact]
        public async Task Encode_PreCancellation_Qoi()
        {
            using Image<Rgba32> image = new(10, 10);
            await Assert.ThrowsAsync<TaskCanceledException>(
                async () => await image.SaveAsQoiAsync(Stream.Null, new(canceled: true)));
        }

        [Fact]
        public async Task Encode_PreCancellation_Tga()
        {
            using Image<Rgba32> image = new(10, 10);
            await Assert.ThrowsAsync<TaskCanceledException>(
                async () => await image.SaveAsTgaAsync(Stream.Null, new(canceled: true)));
        }

        [Fact]
        public async Task Encode_PreCancellation_Tiff()
        {
            using Image<Rgba32> image = new(10, 10);
            await Assert.ThrowsAsync<TaskCanceledException>(
                async () => await image.SaveAsTiffAsync(Stream.Null, new(canceled: true)));
        }

        [Fact]
        public async Task Encode_PreCancellation_Webp()
        {
            using Image<Rgba32> image = new(10, 10);
            await Assert.ThrowsAsync<TaskCanceledException>(
                async () => await image.SaveAsWebpAsync(Stream.Null, new(canceled: true)));
        }

        [Fact]
        public async Task Encode_PreCancellation_Animated_Webp()
        {
            using Image<Rgba32> image = new(10, 10);
            image.Frames.CreateFrame();

            await Assert.ThrowsAsync<TaskCanceledException>(
                async () => await image.SaveAsWebpAsync(Stream.Null, new(canceled: true)));
        }
    }
}
