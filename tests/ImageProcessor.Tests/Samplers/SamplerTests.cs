
namespace ImageProcessor.Tests.Filters
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;

    using Samplers;

    using Xunit;

    public class SamplerTests
    {
        public static readonly List<string> Files = new List<string>
        {
            { "../../TestImages/Formats/Jpg/Backdrop.jpg" },
            { "../../TestImages/Formats/Bmp/Car.bmp" },
            { "../../TestImages/Formats/Png/cmyk.png" },
            //{ "../../TestImages/Formats/Gif/a.gif" },
            { "../../TestImages/Formats/Gif/leaf.gif" },
            //{ "../../TestImages/Formats/Gif/ani.gif" },
            //{ "../../TestImages/Formats/Gif/ani2.gif" },
            //{ "../../TestImages/Formats/Gif/giphy.gif" },
        };

        public static readonly TheoryData<string, IResampler> Samplers =
            new TheoryData<string, IResampler>
            {
                { "Bicubic", new BicubicResampler() },
                { "Lanczos3", new Lanczos3Resampler() }
            };

        [Theory]
        [MemberData("Samplers")]
        public void ResizeImage(string name, IResampler sampler)
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
                        image.Resize(900, 900, sampler).Save(output);
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
            Lanczos3Resampler sampler = new Lanczos3Resampler();
            double result = sampler.GetValue(x);

            Assert.Equal(result, expected);
        }
    }
}
