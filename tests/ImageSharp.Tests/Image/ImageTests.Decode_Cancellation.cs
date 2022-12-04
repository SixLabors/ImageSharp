// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests;

public partial class ImageTests
{
    public class Decode_Cancellation : ImageLoadTestBase
    {
        private bool isTestStreamSeekable;
        private readonly SemaphoreSlim notifyWaitPositionReachedSemaphore = new(0);
        private readonly SemaphoreSlim continueSemaphore = new(0);

        public Decode_Cancellation() => this.TopLevelConfiguration.StreamProcessingBufferSize = 128;

        private static readonly string[] TestFiles = new[]
        {
            TestImages.Png.BikeSmall,
            TestImages.Jpeg.Baseline.Jpeg420Small,
            TestImages.Bmp.Car,
            TestImages.Tiff.RgbUncompressed,
            TestImages.Gif.Kumin,
            TestImages.Tga.Bit32PalRleBottomLeft,
            TestImages.Webp.TestPatternOpaqueSmall,
            TestImages.Pbm.RgbPlainMagick
        };

        private static readonly double[] CancellationPercentages = new[] { 0, 0.5, 0.9 };

        public static readonly object[][] TestFilesWithPercentages = TestFiles
            .SelectMany(f => CancellationPercentages.Select(p => new object[] { f, p }))
            .ToArray();

        [Theory]
        [MemberData(nameof(TestFilesWithPercentages))]
        public async Task IdentifyAsync_IsCancellable(string file, double percentageOfStreamReadToCancel)
        {
            CancellationTokenSource cts = new();
            string path = Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, file);
            using PausedStream pausedStream = new(path);
            pausedStream.OnWaiting(s =>
            {
                if (s.Position >= s.Length * percentageOfStreamReadToCancel)
                {
                    cts.Cancel();
                    pausedStream.Release();
                }
                else
                {
                    pausedStream.Next();
                }
            });

            Configuration configuration = Configuration.CreateDefaultInstance();
            configuration.FileSystem = new SingleStreamFileSystem(pausedStream);
            DecoderOptions options = new()
            {
                Configuration = configuration
            };

            await Assert.ThrowsAsync<TaskCanceledException>(async () => await Image.IdentifyAsync(options, "someFakeFile", cts.Token));
        }

        [Theory]
        [MemberData(nameof(TestFilesWithPercentages))]
        public async Task LoadAsync_IsCancellable(string file, double percentageOfStreamReadToCancel)
        {
            CancellationTokenSource cts = new();
            string path = Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, file);
            using PausedStream pausedStream = new(path);
            pausedStream.OnWaiting(s =>
            {
                if (s.Position >= s.Length * percentageOfStreamReadToCancel)
                {
                    cts.Cancel();
                    pausedStream.Release();
                }
                else
                {
                    pausedStream.Next();
                }
            });

            Configuration configuration = Configuration.CreateDefaultInstance();
            configuration.FileSystem = new SingleStreamFileSystem(pausedStream);
            DecoderOptions options = new()
            {
                Configuration = configuration
            };

            await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            {
                using Image image = await Image.LoadAsync(options, "someFakeFile", cts.Token);
            });
        }

        protected override Stream CreateStream() => this.TestFormat.CreateAsyncSemaphoreStream(this.notifyWaitPositionReachedSemaphore, this.continueSemaphore, this.isTestStreamSeekable);
    }
}
