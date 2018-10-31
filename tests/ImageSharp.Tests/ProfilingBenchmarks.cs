using System.IO;

using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;

using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests
{
    public class ProfilingBenchmarks : MeasureFixture
    {
        public const string SkipProfilingTests =
#if false
            null;
#else
            "Profiling benchmark, enable manually!";
#endif


        public ProfilingBenchmarks(ITestOutputHelper output)
            : base(output)
        {
        }

        [Theory(Skip = SkipProfilingTests)]
        [InlineData(TestImages.Jpeg.Baseline.Jpeg420Exif)]
        public void LoadResizeSave(string imagePath)
        {
            var configuration = Configuration.CreateDefaultInstance();
            configuration.MaxDegreeOfParallelism = 1;

            byte[] imageBytes = TestFile.Create(imagePath).Bytes;

            using (var ms = new MemoryStream())
            {
                this.Measure(30,
                    () =>
                        {
                            using (var image = Image.Load(configuration, imageBytes))
                            {
                                image.Mutate(x => x.Resize(image.Size() / 4));
                                image.SaveAsJpeg(ms);
                            }
                            ms.Seek(0, SeekOrigin.Begin);
                        });
            }
        }
    }
}