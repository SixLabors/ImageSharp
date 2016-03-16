namespace ImageProcessorCore.Tests
{
    using System.Diagnostics;
    using System.IO;

    using ImageProcessorCore.Samplers;

    using Xunit;
    using Filters;

    public class SamplerTests : ProcessorTestBase
    {
        public static readonly TheoryData<string, IResampler> ReSamplers =
            new TheoryData<string, IResampler>
            {
                { "Bicubic", new BicubicResampler() },
                { "Triangle", new TriangleResampler() },
                // Perf: Enable for local testing only
                //{ "Box", new BoxResampler() },
                //{ "Lanczos3", new Lanczos3Resampler() },
                //{ "Lanczos5", new Lanczos5Resampler() },
                //{ "Lanczos8", new Lanczos8Resampler() },
                //{ "MitchellNetravali", new MitchellNetravaliResampler() },
                { "NearestNeighbor", new NearestNeighborResampler() },
                //{ "Hermite", new HermiteResampler() },
                //{ "Spline", new SplineResampler() },
                //{ "Robidoux", new RobidouxResampler() },
                //{ "RobidouxSharp", new RobidouxSharpResampler() },
                //{ "RobidouxSoft", new RobidouxSoftResampler() },
                //{ "Welch", new WelchResampler() }
            };

        public static readonly TheoryData<string, IImageSampler> Samplers = new TheoryData<string, IImageSampler>
        {
             { "Resize", new Resize(new BicubicResampler()) },
             { "Crop", new Crop() }
        };

        public static readonly TheoryData<RotateType, FlipType> RotateFlips = new TheoryData<RotateType, FlipType>
        {
            { RotateType.None, FlipType.Vertical },
            { RotateType.None, FlipType.Horizontal },
            { RotateType.Rotate90, FlipType.None },
            { RotateType.Rotate180, FlipType.None },
            { RotateType.Rotate270, FlipType.None },
        };

        [Theory]
        [MemberData("Samplers")]
        public void SampleImage(string name, IImageSampler processor)
        {
            if (!Directory.Exists("TestOutput/Sample"))
            {
                Directory.CreateDirectory("TestOutput/Sample");
            }

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    Stopwatch watch = Stopwatch.StartNew();
                    Image image = new Image(stream);
                    string filename = Path.GetFileNameWithoutExtension(file) + "-" + name + Path.GetExtension(file);
                    using (FileStream output = File.OpenWrite($"TestOutput/Sample/{ Path.GetFileName(filename) }"))
                    {
                        processor.OnProgress += this.ProgressUpdate;
                        image = image.Process(image.Width / 2, image.Height / 2, processor);
                        image.Save(output);
                        processor.OnProgress -= this.ProgressUpdate;
                    }

                    Trace.WriteLine($"{ name }: { watch.ElapsedMilliseconds}ms");
                }
            }
        }

        [Theory]
        [MemberData("ReSamplers")]
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
                        image.Resize(image.Width / 2, image.Height / 2, sampler, false, this.ProgressUpdate)
                             .Save(output);
                    }

                    Trace.WriteLine($"{name}: {watch.ElapsedMilliseconds}ms");
                }
            }
        }

        [Fact]
        public void ImageShouldResizeWidthAndKeepAspect()
        {
            if (!Directory.Exists("TestOutput/Resize"))
            {
                Directory.CreateDirectory("TestOutput/Resize");
            }

            var name = "FixedWidth";

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    Stopwatch watch = Stopwatch.StartNew();
                    Image image = new Image(stream);
                    string filename = Path.GetFileNameWithoutExtension(file) + "-" + name + Path.GetExtension(file);
                    using (FileStream output = File.OpenWrite($"TestOutput/Resize/{filename}"))
                    {
                        image.Resize(image.Width / 3, 0, new TriangleResampler(), false, this.ProgressUpdate)
                             .Save(output);
                    }

                    Trace.WriteLine($"{name}: {watch.ElapsedMilliseconds}ms");
                }
            }
        }

        [Fact]
        public void ImageShouldResizeHeightAndKeepAspect()
        {
            if (!Directory.Exists("TestOutput/Resize"))
            {
                Directory.CreateDirectory("TestOutput/Resize");
            }

            var name = "FixedHeight";

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    Stopwatch watch = Stopwatch.StartNew();
                    Image image = new Image(stream);
                    string filename = Path.GetFileNameWithoutExtension(file) + "-" + name + Path.GetExtension(file);
                    using (FileStream output = File.OpenWrite($"TestOutput/Resize/{filename}"))
                    {
                        image.Resize(0, image.Height / 3, new TriangleResampler(), false, this.ProgressUpdate)
                             .Save(output);
                    }

                    Trace.WriteLine($"{name}: {watch.ElapsedMilliseconds}ms");
                }
            }
        }

        [Theory]
        [MemberData("RotateFlips")]
        public void ImageShouldRotateFlip(RotateType rotateType, FlipType flipType)
        {
            if (!Directory.Exists("TestOutput/RotateFlip"))
            {
                Directory.CreateDirectory("TestOutput/RotateFlip");
            }

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    Stopwatch watch = Stopwatch.StartNew();
                    Image image = new Image(stream);
                    string filename = Path.GetFileNameWithoutExtension(file) + "-" + rotateType + flipType + Path.GetExtension(file);
                    using (FileStream output = File.OpenWrite($"TestOutput/RotateFlip/{filename}"))
                    {
                        image.RotateFlip(rotateType, flipType, this.ProgressUpdate)
                             .Save(output);
                    }

                    Trace.WriteLine($"{rotateType + "-" + flipType}: {watch.ElapsedMilliseconds}ms");
                }
            }
        }

        [Theory]
        [MemberData("ReSamplers")]
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
                        image.Rotate(45, sampler, false, this.ProgressUpdate)
                             //.BackgroundColor(Color.Aqua)
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
                        image.EntropyCrop(.5f, this.ProgressUpdate).Save(output);
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
                        image.Crop(image.Width / 2, image.Height / 2, this.ProgressUpdate).Save(output);
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
