
namespace ImageProcessor.Tests
{
    using System.Diagnostics;
    using System.IO;

    using ImageProcessor.Samplers;

    using Xunit;
    using Filters;

    public class SamplerTests : ProcessorTestBase
    {
        public static readonly TheoryData<string, IResampler> Samplers =
            new TheoryData<string, IResampler>
            {
                { "Bicubic", new BicubicResampler() },
                { "Triangle", new TriangleResampler() },
                { "Box", new BoxResampler() },
                { "Lanczos3", new Lanczos3Resampler() },
                { "Lanczos5", new Lanczos5Resampler() },
                { "Lanczos8", new Lanczos8Resampler() },
                { "MitchellNetravali", new MitchellNetravaliResampler() },
                { "NearestNeighbor", new NearestNeighborResampler() },
                { "Hermite", new HermiteResampler() },
                { "Spline", new SplineResampler() },
                { "Robidoux", new RobidouxResampler() },
                { "RobidouxSharp", new RobidouxSharpResampler() },
                { "RobidouxSoft", new RobidouxSoftResampler() },
                { "Welch", new WelchResampler() }
            };

        [Theory]
        [MemberData("Samplers")]
        public void ImageShouldResize(string name, IResampler sampler)
        {
            if (!Directory.Exists("TestOutput/Resize"))
            {
                Directory.CreateDirectory("TestOutput/Resize");
            }

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    Stopwatch watch = Stopwatch.StartNew();
                    Image image = new Image(stream);
                    string filename = Path.GetFileNameWithoutExtension(file) + "-" + name + Path.GetExtension(file);
                    using (FileStream output = File.OpenWrite($"TestOutput/Resize/{filename}"))
                    {
                        image.Resize(image.Width / 2, image.Height / 2, sampler)
                             .Save(output);
                    }

                    Trace.WriteLine($"{name}: {watch.ElapsedMilliseconds}ms");
                }
            }
        }

        [Theory]
        [MemberData("Samplers")]
        public void ImageShouldRotate(string name, IResampler sampler)
        {
            if (!Directory.Exists("TestOutput/Rotate"))
            {
                Directory.CreateDirectory("TestOutput/Rotate");
            }

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    Stopwatch watch = Stopwatch.StartNew();
                    Image image = new Image(stream);
                    string filename = Path.GetFileNameWithoutExtension(file) + "-" + name + Path.GetExtension(file);
                    using (FileStream output = File.OpenWrite($"TestOutput/Rotate/{filename}"))
                    {
                        image.Rotate(45, sampler)
                             .Save(output);
                    }

                    Trace.WriteLine($"{name}: {watch.ElapsedMilliseconds}ms");
                }
            }
        }

        [Fact]
        public void ImageShouldEntropyCrop()
        {
            if (!Directory.Exists("TestOutput/EntropyCrop"))
            {
                Directory.CreateDirectory("TestOutput/EntropyCrop");
            }

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    Image image = new Image(stream);
                    string filename = Path.GetFileNameWithoutExtension(file) + "-EntropyCrop" + Path.GetExtension(file);
                    using (FileStream output = File.OpenWrite($"TestOutput/EntropyCrop/{filename}"))
                    {
                        image.EntropyCrop().Save(output);
                    }
                }
            }
        }

        [Fact]
        public void ImageShouldCrop()
        {
            if (!Directory.Exists("TestOutput/Crop"))
            {
                Directory.CreateDirectory("TestOutput/Crop");
            }

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    Image image = new Image(stream);
                    string filename = Path.GetFileNameWithoutExtension(file) + "-Crop" + Path.GetExtension(file);
                    using (FileStream output = File.OpenWrite($"TestOutput/Crop/{filename}"))
                    {
                        image.Crop(image.Width / 2, image.Height / 2).Save(output);
                    }
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
        public static void Lanczos3WindowOscillatesCorrectly(float x, float expected)
        {
            Lanczos3Resampler sampler = new Lanczos3Resampler();
            float result = sampler.GetValue(x);

            Assert.Equal(result, expected);
        }
    }
}
