
namespace ImageProcessor.Tests
{
    using System.Diagnostics;
    using System.IO;

    using ImageProcessor.Samplers;

    using Xunit;

    public class SamplerTests : ProcessorTestBase
    {
        public static readonly TheoryData<string, IResampler> Samplers =
            new TheoryData<string, IResampler>
            {
                { "Bicubic", new BicubicResampler() },
                //{ "Lanczos3", new Lanczos5Resampler() }
            };

        [Theory]
        [MemberData("Samplers")]
        public void ImageShouldResize(string name, IResampler sampler)
        {
            if (!Directory.Exists("Resized"))
            {
                Directory.CreateDirectory("Resized");
            }

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    Stopwatch watch = Stopwatch.StartNew();
                    Image image = new Image(stream);
                    string filename = Path.GetFileNameWithoutExtension(file) + "-" + name + Path.GetExtension(file);
                    using (FileStream output = File.OpenWrite($"Resized/{filename}"))
                    {
                        image.Resize(500, 500, sampler).Save(output);
                    }

                    Trace.WriteLine($"{name}: {watch.ElapsedMilliseconds}ms");
                }
            }
        }

        [Theory]
        [InlineData(-2, 0)]
        [InlineData(-1, 0)]
        [InlineData(0, 1)]
        [InlineData(1, 0)]
        [InlineData(2, 0)]
        [InlineData(2, 0)]
        public static void Lanczos3WindowOscillatesCorrectly(double x, double expected)
        {
            Lanczos5Resampler sampler = new Lanczos5Resampler();
            double result = sampler.GetValue(x);

            Assert.Equal(result, expected);
        }
    }
}
