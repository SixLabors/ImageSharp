// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Processing;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.ProfilingBenchmarks;

public class LoadResizeSaveProfilingBenchmarks : MeasureFixture
{
    public LoadResizeSaveProfilingBenchmarks(ITestOutputHelper output)
        : base(output)
    {
    }

    [Theory(Skip = ProfilingSetup.SkipProfilingTests)]
    [InlineData(TestImages.Jpeg.Baseline.Jpeg420Exif)]
    public void LoadResizeSave(string imagePath)
    {
        Configuration configuration = Configuration.CreateDefaultInstance();
        configuration.MaxDegreeOfParallelism = 1;

        DecoderOptions options = new()
        {
            Configuration = configuration
        };

        byte[] imageBytes = TestFile.Create(imagePath).Bytes;

        using MemoryStream ms = new MemoryStream();
        this.Measure(
            30,
            () =>
                {
                    using (Image image = Image.Load(options, imageBytes))
                    {
                        image.Mutate(x => x.Resize(image.Size / 4));
                        image.SaveAsJpeg(ms);
                    }

                    ms.Seek(0, SeekOrigin.Begin);
                });
    }
}
