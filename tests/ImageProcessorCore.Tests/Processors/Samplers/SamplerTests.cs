namespace ImageProcessorCore.Tests
{
    using System.Diagnostics;
    using System.IO;

    using Processors;

    using Xunit;

    public class SamplerTests : FileTestBase
    {
        public static readonly TheoryData<string, IResampler> ReSamplers =
            new TheoryData<string, IResampler>
            {
                { "Bicubic", new BicubicResampler() },
                { "Triangle", new TriangleResampler() },
                { "NearestNeighbor", new NearestNeighborResampler() },
                // Perf: Enable for local testing only
                //{ "Box", new BoxResampler() },
                //{ "Lanczos3", new Lanczos3Resampler() },
                //{ "Lanczos5", new Lanczos5Resampler() },
                //{ "Lanczos8", new Lanczos8Resampler() },
                //{ "MitchellNetravali", new MitchellNetravaliResampler() },
                //{ "Hermite", new HermiteResampler() },
                //{ "Spline", new SplineResampler() },
                //{ "Robidoux", new RobidouxResampler() },
                //{ "RobidouxSharp", new RobidouxSharpResampler() },
                //{ "RobidouxSoft", new RobidouxSoftResampler() },
                //{ "Welch", new WelchResampler() }
            };

        public static readonly TheoryData<string, IImageSampler> Samplers = new TheoryData<string, IImageSampler>
        {
             { "Resize", new ResizeProcessor(new BicubicResampler()) },
             { "Crop", new CropProcessor() }
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

        [Fact]
        public void ImageShouldPad()
        {
            if (!Directory.Exists("TestOutput/Pad"))
            {
                Directory.CreateDirectory("TestOutput/Pad");
            }

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    Stopwatch watch = Stopwatch.StartNew();

                    string filename = Path.GetFileName(file);

                    Image image = new Image(stream);
                    using (FileStream output = File.OpenWrite($"TestOutput/Pad/{filename}"))
                    {
                        image.Pad(image.Width + 50, image.Height + 50, this.ProgressUpdate)
                             .Save(output);
                    }

                    Trace.WriteLine($"{watch.ElapsedMilliseconds}ms");
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
                    string filename = Path.GetFileNameWithoutExtension(file) + "-" + name + Path.GetExtension(file);

                    Image image = new Image(stream);
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
                    string filename = Path.GetFileNameWithoutExtension(file) + "-" + name + Path.GetExtension(file);

                    Image image = new Image(stream);
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

            string name = "FixedHeight";

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    Stopwatch watch = Stopwatch.StartNew();
                    string filename = Path.GetFileNameWithoutExtension(file) + "-" + name + Path.GetExtension(file);

                    Image image = new Image(stream);
                    using (FileStream output = File.OpenWrite($"TestOutput/Resize/{filename}"))
                    {
                        image.Resize(0, image.Height / 3, new TriangleResampler(), false, this.ProgressUpdate)
                             .Save(output);
                    }

                    Trace.WriteLine($"{name}: {watch.ElapsedMilliseconds}ms");
                }
            }
        }

        [Fact]
        public void ImageShouldResizeWithCropMode()
        {
            if (!Directory.Exists("TestOutput/ResizeCrop"))
            {
                Directory.CreateDirectory("TestOutput/ResizeCrop");
            }

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    Stopwatch watch = Stopwatch.StartNew();
                    string filename = Path.GetFileName(file);

                    Image image = new Image(stream);
                    using (FileStream output = File.OpenWrite($"TestOutput/ResizeCrop/{filename}"))
                    {
                        ResizeOptions options = new ResizeOptions()
                        {
                            Size = new Size(image.Width / 2, image.Height)
                        };

                        image.Resize(options, this.ProgressUpdate)
                             .Save(output);
                    }

                    Trace.WriteLine($"{filename}: {watch.ElapsedMilliseconds}ms");
                }
            }
        }

        [Fact]
        public void ImageShouldResizeWithPadMode()
        {
            if (!Directory.Exists("TestOutput/ResizePad"))
            {
                Directory.CreateDirectory("TestOutput/ResizePad");
            }

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    Stopwatch watch = Stopwatch.StartNew();
                    string filename = Path.GetFileName(file);

                    Image image = new Image(stream);
                    using (FileStream output = File.OpenWrite($"TestOutput/ResizePad/{filename}"))
                    {
                        ResizeOptions options = new ResizeOptions()
                        {
                            Size = new Size(image.Width + 200, image.Height),
                            Mode = ResizeMode.Pad
                        };

                        image.Resize(options, this.ProgressUpdate)
                             .Save(output);
                    }

                    Trace.WriteLine($"{filename}: {watch.ElapsedMilliseconds}ms");
                }
            }
        }

        [Fact]
        public void ImageShouldResizeWithBoxPadMode()
        {
            if (!Directory.Exists("TestOutput/ResizeBoxPad"))
            {
                Directory.CreateDirectory("TestOutput/ResizeBoxPad");
            }

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    Stopwatch watch = Stopwatch.StartNew();
                    string filename = Path.GetFileName(file);

                    Image image = new Image(stream);
                    using (FileStream output = File.OpenWrite($"TestOutput/ResizeBoxPad/{filename}"))
                    {
                        ResizeOptions options = new ResizeOptions()
                        {
                            Size = new Size(image.Width + 200, image.Height + 200),
                            Mode = ResizeMode.BoxPad
                        };

                        image.Resize(options, this.ProgressUpdate)
                             .Save(output);
                    }

                    Trace.WriteLine($"{filename}: {watch.ElapsedMilliseconds}ms");
                }
            }
        }

        [Fact]
        public void ImageShouldResizeWithMaxMode()
        {
            if (!Directory.Exists("TestOutput/ResizeMax"))
            {
                Directory.CreateDirectory("TestOutput/ResizeMax");
            }

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    Stopwatch watch = Stopwatch.StartNew();
                    string filename = Path.GetFileName(file);

                    Image image = new Image(stream);
                    using (FileStream output = File.OpenWrite($"TestOutput/ResizeMax/{filename}"))
                    {
                        ResizeOptions options = new ResizeOptions()
                        {
                            Size = new Size(300, 300),
                            Mode = ResizeMode.Max,
                            //Sampler = new NearestNeighborResampler()
                        };

                        image.Resize(options, this.ProgressUpdate)
                             .Save(output);
                    }

                    Trace.WriteLine($"{filename}: {watch.ElapsedMilliseconds}ms");
                }
            }
        }

        [Fact]
        public void ImageShouldResizeWithMinMode()
        {
            if (!Directory.Exists("TestOutput/ResizeMin"))
            {
                Directory.CreateDirectory("TestOutput/ResizeMin");
            }

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    Stopwatch watch = Stopwatch.StartNew();
                    string filename = Path.GetFileName(file);

                    Image image = new Image(stream);
                    using (FileStream output = File.OpenWrite($"TestOutput/ResizeMin/{filename}"))
                    {
                        ResizeOptions options = new ResizeOptions()
                        {
                            Size = new Size(image.Width - 50, image.Height - 25),
                            Mode = ResizeMode.Min
                        };

                        image.Resize(options, this.ProgressUpdate)
                             .Save(output);
                    }

                    Trace.WriteLine($"{filename}: {watch.ElapsedMilliseconds}ms");
                }
            }
        }

        [Fact]
        public void ImageShouldResizeWithStretchMode()
        {
            if (!Directory.Exists("TestOutput/ResizeStretch"))
            {
                Directory.CreateDirectory("TestOutput/ResizeStretch");
            }

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    Stopwatch watch = Stopwatch.StartNew();
                    string filename = Path.GetFileName(file);

                    Image image = new Image(stream);
                    using (FileStream output = File.OpenWrite($"TestOutput/ResizeStretch/{filename}"))
                    {
                        ResizeOptions options = new ResizeOptions()
                        {
                            Size = new Size(image.Width - 200, image.Height),
                            Mode = ResizeMode.Stretch
                        };

                        image.Resize(options, this.ProgressUpdate)
                             .Save(output);
                    }

                    Trace.WriteLine($"{filename}: {watch.ElapsedMilliseconds}ms");
                }
            }
        }

        [Fact]
        public void ImageShouldNotDispose()
        {
            if (!Directory.Exists("TestOutput/Dispose"))
            {
                Directory.CreateDirectory("TestOutput/Dispose");
            }

            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    Stopwatch watch = Stopwatch.StartNew();
                    string filename = Path.GetFileName(file);

                    Image image = new Image(stream);
                    image = image.BackgroundColor(Color.RebeccaPurple);
                    using (FileStream output = File.OpenWrite($"TestOutput/Dispose/{filename}"))
                    {
                        ResizeOptions options = new ResizeOptions()
                        {
                            Size = new Size(image.Width - 10, image.Height),
                            Mode = ResizeMode.Stretch
                        };

                        image.Resize(options, this.ProgressUpdate)
                             .Save(output);
                    }

                    Trace.WriteLine($"{filename}: {watch.ElapsedMilliseconds}ms");
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
                    string filename = Path.GetFileNameWithoutExtension(file) + "-" + rotateType + flipType + Path.GetExtension(file);

                    Image image = new Image(stream);
                    using (FileStream output = File.OpenWrite($"TestOutput/RotateFlip/{filename}"))
                    {
                        image.RotateFlip(rotateType, flipType, this.ProgressUpdate)
                             .Save(output);
                    }

                    Trace.WriteLine($"{rotateType + "-" + flipType}: {watch.ElapsedMilliseconds}ms");
                }
            }
        }

        [Fact]
        public void ImageShouldRotate()
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

                    string filename = Path.GetFileName(file);

                    Image image = new Image(stream);
                    using (FileStream output = File.OpenWrite($"TestOutput/Rotate/{filename}"))
                    {
                        image.Rotate(63, this.ProgressUpdate)
                             .Save(output);
                    }

                    Trace.WriteLine($"{watch.ElapsedMilliseconds}ms");
                }
            }
        }

        [Fact]
        public void ImageShouldSkew()
        {
            if (!Directory.Exists("TestOutput/Skew"))
            {
                Directory.CreateDirectory("TestOutput/Skew");
            }

            // Matches live example http://www.w3schools.com/css/tryit.asp?filename=trycss3_transform_skew
            foreach (string file in Files)
            {
                using (FileStream stream = File.OpenRead(file))
                {
                    Stopwatch watch = Stopwatch.StartNew();

                    string filename = Path.GetFileName(file);

                    Image image = new Image(stream);
                    using (FileStream output = File.OpenWrite($"TestOutput/Skew/{filename}"))
                    {
                        image.Skew(20, 10, this.ProgressUpdate)
                             .Save(output);
                    }

                    Trace.WriteLine($"{watch.ElapsedMilliseconds}ms");
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
                    string filename = Path.GetFileNameWithoutExtension(file) + "-EntropyCrop" + Path.GetExtension(file);

                    Image image = new Image(stream);
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

                    string filename = Path.GetFileNameWithoutExtension(file) + "-Crop" + Path.GetExtension(file);

                    Image image = new Image(stream);
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
