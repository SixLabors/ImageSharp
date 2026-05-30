// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests;

public partial class ImageTests
{
    public class Decode_Cancellation : ImageLoadTestBase
    {
        public Decode_Cancellation() => this.TopLevelConfiguration.StreamProcessingBufferSize = 128;

        public static readonly string[] TestFileForEachCodec =
        [
            TestImages.Jpeg.Baseline.Snake,

            // TODO: Figure out Unix cancellation failures, and validate cancellation for each decoder.
            //TestImages.Bmp.Car,
            //TestImages.Png.Bike,
            //TestImages.Tiff.RgbUncompressed,
            //TestImages.Gif.Kumin,
            //TestImages.Tga.Bit32BottomRight,
            //TestImages.Webp.Lossless.WithExif,
            //TestImages.Pbm.GrayscaleBinaryWide
        ];

        public static object[][] IdentifyData { get; } = TestFileForEachCodec.Select(f => new object[] { f }).ToArray();

        [Theory]
        [MemberData(nameof(IdentifyData))]
        public async Task IdentifyAsync_PreCancelled(string file)
        {
            await using FileStream fs = File.OpenRead(TestFile.GetInputFileFullPath(file));
            CancellationToken preCancelled = new(canceled: true);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await Image.IdentifyAsync(fs, preCancelled));
        }

        private static TheoryData<bool, string, double> CreateLoadData()
        {
            double[] percentages = [0, 0.3, 0.7];

            TheoryData<bool, string, double> data = [];

            foreach (string file in TestFileForEachCodec)
            {
                foreach (double p in percentages)
                {
                    data.Add(false, file, p);
                    data.Add(true, file, p);
                }
            }

            return data;
        }

        public static TheoryData<bool, string, double> LoadData { get; } = CreateLoadData();

        // TODO: Figure out cancellation failures on Linux
        [ConditionalTheory(typeof(TestEnvironment), nameof(TestEnvironment.IsWindows))]
        [MemberData(nameof(LoadData))]
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
            configuration.StreamProcessingBufferSize = (int)Math.Min(128, pausedStream.Length / 4);

            DecoderOptions options = new()
            {
                Configuration = configuration
            };

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                using Image image = await Image.LoadAsync(options, "someFakeFile", cts.Token);
            });
        }
    }
}
