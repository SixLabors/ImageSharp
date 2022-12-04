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

        private static TheoryData<bool, string, double> GetTestData()
        {
            string[] testFileForEachCodec = new[]
            {
                TestImages.Png.BikeSmall,
                TestImages.Jpeg.Baseline.Jpeg420Small,
                TestImages.Bmp.Car,
                TestImages.Tiff.RgbUncompressed,
                TestImages.Gif.Kumin,
                TestImages.Tga.Bit32PalRleBottomLeft,
                TestImages.Webp.TestPatternOpaqueSmall,
                TestImages.Pbm.GrayscaleBinaryWide
            };

            double[] percentages = new[] { 0, 0.5, 0.9 };

            TheoryData<bool, string, double> data = new();

            foreach (string file in testFileForEachCodec)
            {
                foreach (double p in percentages)
                {
                    data.Add(false, file, p);
                    data.Add(true, file, p);
                }
            }

            return data;
        }

        public static TheoryData<bool, string, double> TestData { get; } = GetTestData();

        [Theory]
        [MemberData(nameof(TestData))]
        public async Task IdentifyAsync_IsCancellable(bool useMemoryStream, string file, double percentageOfStreamReadToCancel)
        {
            CancellationTokenSource cts = new();
            using IPausedStream pausedStream = useMemoryStream ?
                new PausedMemoryStream(TestFile.Create(file).Bytes) :
                new PausedStream(TestFile.GetInputFileFullPath(file));

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
            configuration.FileSystem = new SingleStreamFileSystem((Stream)pausedStream);
            DecoderOptions options = new()
            {
                Configuration = configuration
            };

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                async () => await Image.IdentifyAsync(options, "someFakeFile", cts.Token))
                .WaitAsync(TimeSpan.FromSeconds(10));
        }

        [Theory]
        [MemberData(nameof(TestData))]
        public async Task LoadAsync_IsCancellable(bool useMemoryStream, string file, double percentageOfStreamReadToCancel)
        {
            CancellationTokenSource cts = new();
            using IPausedStream pausedStream = useMemoryStream ?
                new PausedMemoryStream(TestFile.Create(file).Bytes) :
                new PausedStream(TestFile.GetInputFileFullPath(file));

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
            configuration.FileSystem = new SingleStreamFileSystem((Stream)pausedStream);
            DecoderOptions options = new()
            {
                Configuration = configuration
            };

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                using Image image = await Image.LoadAsync(options, "someFakeFile", cts.Token);
            }).WaitAsync(TimeSpan.FromSeconds(10));
        }

        protected override Stream CreateStream() => this.TestFormat.CreateAsyncSemaphoreStream(this.notifyWaitPositionReachedSemaphore, this.continueSemaphore, this.isTestStreamSeekable);
    }
}
