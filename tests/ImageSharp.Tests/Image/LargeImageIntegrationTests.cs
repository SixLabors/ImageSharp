// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using Microsoft.DotNet.RemoteExecutor;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Tests;

public class LargeImageIntegrationTests
{
    [Theory(Skip = "For local testing only.")]
    [WithBasicTestPatternImages(width: 30000, height: 30000, PixelTypes.Rgba32)]
    public void CreateAndResize(TestImageProvider<Rgba32> provider)
    {
        using Image<Rgba32> image = provider.GetImage();
        image.Mutate(c => c.Resize(1000, 1000));
        image.DebugSave(provider);
    }

    [Fact]
    public void PreferContiguousImageBuffers_CreateImage_BufferIsContiguous()
    {
        // Run remotely to avoid large allocation in the test process:
        RemoteExecutor.Invoke(RunTest).Dispose();

        static void RunTest()
        {
            Configuration configuration = Configuration.Default.Clone();
            configuration.PreferContiguousImageBuffers = true;

            using Image<Rgba32> image = new(configuration, 2048, 2048);
            Assert.True(image.DangerousTryGetSinglePixelMemory(out Memory<Rgba32> mem));
            Assert.Equal(2048 * 2048, mem.Length);
        }
    }

    [Theory]
    [InlineData("bmp")]
    [InlineData("png")]
    [InlineData("jpeg")]
    [InlineData("gif")]
    [InlineData("tiff")]
    [InlineData("webp")]
    public void PreferContiguousImageBuffers_LoadImage_BufferIsContiguous(string formatOuter)
    {
        // Run remotely to avoid large allocation in the test process:
        RemoteExecutor.Invoke(RunTest, formatOuter).Dispose();

        static void RunTest(string formatInner)
        {
            using IDisposable mem = MemoryAllocatorValidator.MonitorAllocations();

            Configuration configuration = Configuration.Default.Clone();
            configuration.PreferContiguousImageBuffers = true;
            configuration.ImageFormatsManager.TryFindFormatByFileExtension(formatInner, out IImageFormat format);
            IImageEncoder encoder = configuration.ImageFormatsManager.GetEncoder(format!);
            string dir = TestEnvironment.CreateOutputDirectory(".Temp");
            string path = Path.Combine(dir, $"{Guid.NewGuid()}.{formatInner}");
            using (Image<Rgba32> temp = new(2048, 2048))
            {
                temp.Save(path, encoder);
            }

            DecoderOptions options = new()
            {
                Configuration = configuration
            };

            using Image<Rgba32> image = Image.Load<Rgba32>(options, path);
            File.Delete(path);
            Assert.Equal(1, image.GetPixelMemoryGroup().Count);
        }
    }

    [Theory]
    [WithBasicTestPatternImages(width: 10, height: 10, PixelTypes.Rgba32)]
    public void DangerousTryGetSinglePixelMemory_WhenImageTooLarge_ReturnsFalse(TestImageProvider<Rgba32> provider)
    {
        provider.LimitAllocatorBufferCapacity().InPixels(10);
        using Image<Rgba32> image = provider.GetImage();
        Assert.False(image.DangerousTryGetSinglePixelMemory(out Memory<Rgba32> mem));
        Assert.False(image.Frames.RootFrame.DangerousTryGetSinglePixelMemory(out Memory<Rgba32> _));
    }
}
