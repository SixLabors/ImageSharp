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

        private static readonly string TestFile = Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, TestImages.Bmp.Car);

        public static readonly TheoryData<double> Percentages = new() { 0, 0.5, 0.9 };

        [Theory]
        [MemberData(nameof(Percentages))]
        public async Task IdentifyAsync_IsCancellable(double percentageOfStreamReadToCancel)
        {
            CancellationTokenSource cts = new();
            using PausedStream pausedStream = new(TestFile);
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
        [MemberData(nameof(Percentages))]
        public async Task LoadAsync_IsCancellable(double percentageOfStreamReadToCancel)
        {
            CancellationTokenSource cts = new();
            using PausedStream pausedStream = new(TestFile);
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
