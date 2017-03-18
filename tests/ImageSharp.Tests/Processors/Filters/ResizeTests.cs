// <copyright file="ResizeTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>
namespace ImageSharp.Tests
{
    using System;
    using System.IO;
    using Processing;
    using Xunit;
    using Xunit.Abstractions;

    public class ResizeProfilingBenchmarks : MeasureFixture
    {
        public ResizeProfilingBenchmarks(ITestOutputHelper output)
            : base(output)
        {
        }

        public int ExecutionCount { get; set; } = 50;

        [Theory]
        [InlineData(100, 100)]
        [InlineData(1000, 1000)]
        public void ResizeBicubic(int width, int height)
        {
            this.Measure(this.ExecutionCount,
                () =>
                    {
                        using (Image image = new Image(width, height))
                        {
                            image.Resize(width / 4, height / 4);
                        }
                    });
        }
    }

    public class ResizeTests : FileTestBase
    {
        public static readonly TheoryData<string, IResampler> ReSamplers =
            new TheoryData<string, IResampler>
                {
                    { "Bicubic", new BicubicResampler() },
                    { "Triangle", new TriangleResampler() },
                    { "NearestNeighbor", new NearestNeighborResampler() },

                    // Perf: Enable for local testing only
                    // { "Box", new BoxResampler() },
                    // { "Lanczos3", new Lanczos3Resampler() },
                    // { "Lanczos5", new Lanczos5Resampler() },
                    { "MitchellNetravali", new MitchellNetravaliResampler() },

                    // { "Lanczos8", new Lanczos8Resampler() },
                    // { "Hermite", new HermiteResampler() },
                    // { "Spline", new SplineResampler() },
                    // { "Robidoux", new RobidouxResampler() },
                    // { "RobidouxSharp", new RobidouxSharpResampler() },
                    // { "RobidouxSoft", new RobidouxSoftResampler() },
                    // { "Welch", new WelchResampler() }
                };

        [Theory]
        [MemberData(nameof(ReSamplers))]
        public void ImageShouldResize(string name, IResampler sampler)
        {
            string path = this.CreateOutputDirectory("Resize");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName(name);
                using (Image image = file.CreateImage())
                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    image.Resize(image.Width / 2, image.Height / 2, sampler, true).Save(output);
                }
            }
        }

        [Theory]
        [MemberData(nameof(ReSamplers))]
        public void ImageShouldResizeFromSourceRectangle(string name, IResampler sampler)
        {
            name = $"{name}-SourceRect";

            string path = this.CreateOutputDirectory("Resize");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName(name);
                using (Image image = file.CreateImage())
                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    Rectangle sourceRectangle = new Rectangle(image.Width / 8, image.Height / 8, image.Width / 4, image.Height / 4);
                    Rectangle destRectangle = new Rectangle(image.Width / 4, image.Height / 4, image.Width / 2, image.Height / 2);
                    image.Resize(image.Width, image.Height, sampler, sourceRectangle, destRectangle, false).Save(output);
                }
            }
        }

        [Theory]
        [MemberData(nameof(ReSamplers))]
        public void ImageShouldResizeWidthAndKeepAspect(string name, IResampler sampler)
        {
            name = $"{name}-FixedWidth";

            string path = this.CreateOutputDirectory("Resize");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName(name);
                using (Image image = file.CreateImage())
                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    image.Resize(image.Width / 3, 0, sampler, false).Save(output);
                }
            }
        }

        [Theory]
        [MemberData(nameof(ReSamplers))]
        public void ImageShouldResizeHeightAndKeepAspect(string name, IResampler sampler)
        {
            name = $"{name}-FixedHeight";

            string path = this.CreateOutputDirectory("Resize");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName(name);
                using (Image image = file.CreateImage())
                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    image.Resize(0, image.Height / 3, sampler, false).Save(output);
                }
            }
        }

        [Theory]
        [MemberData(nameof(ReSamplers))]
        public void ImageShouldResizeWithCropWidthMode(string name, IResampler sampler)
        {
            name = $"{name}-CropWidth";

            string path = this.CreateOutputDirectory("Resize");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName(name);
                using (Image image = file.CreateImage())
                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    ResizeOptions options = new ResizeOptions
                    {
                        Sampler = sampler,
                        Size = new Size(image.Width / 2, image.Height)
                    };

                    image.Resize(options).Save(output);
                }
            }
        }

        [Theory]
        [MemberData(nameof(ReSamplers))]
        public void ImageShouldResizeWithCropHeightMode(string name, IResampler sampler)
        {
            name = $"{name}-CropHeight";

            string path = this.CreateOutputDirectory("Resize");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName(name);
                using (Image image = file.CreateImage())
                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    ResizeOptions options = new ResizeOptions
                    {
                        Sampler = sampler,
                        Size = new Size(image.Width, image.Height / 2)
                    };

                    image.Resize(options).Save(output);
                }
            }
        }

        [Theory]
        [MemberData(nameof(ReSamplers))]
        public void ImageShouldResizeWithPadMode(string name, IResampler sampler)
        {
            name = $"{name}-Pad";

            string path = this.CreateOutputDirectory("Resize");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName(name);
                using (Image image = file.CreateImage())
                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    ResizeOptions options = new ResizeOptions
                    {
                        Size = new Size(image.Width + 200, image.Height),
                        Mode = ResizeMode.Pad
                    };

                    image.Resize(options).Save(output);
                }
            }
        }

        [Theory]
        [MemberData(nameof(ReSamplers))]
        public void ImageShouldResizeWithBoxPadMode(string name, IResampler sampler)
        {
            name = $"{name}-BoxPad";

            string path = this.CreateOutputDirectory("Resize");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName(name);
                using (Image image = file.CreateImage())
                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    ResizeOptions options = new ResizeOptions
                    {
                        Sampler = sampler,
                        Size = new Size(image.Width + 200, image.Height + 200),
                        Mode = ResizeMode.BoxPad
                    };

                    image.Resize(options).Save(output);
                }
            }
        }

        [Theory]
        [MemberData(nameof(ReSamplers))]
        public void ImageShouldResizeWithMaxMode(string name, IResampler sampler)
        {
            name = $"{name}Max";

            string path = this.CreateOutputDirectory("Resize");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName(name);
                using (Image image = file.CreateImage())
                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    ResizeOptions options = new ResizeOptions
                    {
                        Sampler = sampler,
                        Size = new Size(300, 300),
                        Mode = ResizeMode.Max
                    };

                    image.Resize(options).Save(output);
                }
            }
        }

        [Theory]
        [MemberData(nameof(ReSamplers))]
        public void ImageShouldResizeWithMinMode(string name, IResampler sampler)
        {
            name = $"{name}-Min";

            string path = this.CreateOutputDirectory("Resize");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName(name);
                using (Image image = file.CreateImage())
                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    ResizeOptions options = new ResizeOptions
                    {
                        Sampler = sampler,
                        Size = new Size((int)Math.Round(image.Width * .75F), (int)Math.Round(image.Height * .95F)),
                        Mode = ResizeMode.Min
                    };

                    image.Resize(options).Save(output);
                }
            }
        }

        [Theory]
        [MemberData(nameof(ReSamplers))]
        public void ImageShouldResizeWithStretchMode(string name, IResampler sampler)
        {
            name = $"{name}Stretch";

            string path = this.CreateOutputDirectory("Resize");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName(name);
                using (Image image = file.CreateImage())
                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    ResizeOptions options = new ResizeOptions
                    {
                        Sampler = sampler,
                        Size = new Size(image.Width / 2, image.Height),
                        Mode = ResizeMode.Stretch
                    };

                    image.Resize(options).Save(output);
                }
            }
        }

        [Theory]
        [InlineData(-2, 0)]
        [InlineData(-1, 0)]
        [InlineData(0, 1)]
        [InlineData(1, 0)]
        [InlineData(2, 0)]
        public static void BicubicWindowOscillatesCorrectly(float x, float expected)
        {
            BicubicResampler sampler = new BicubicResampler();
            float result = sampler.GetValue(x);

            Assert.Equal(result, expected);
        }

        [Theory]
        [InlineData(-2, 0)]
        [InlineData(-1, 0)]
        [InlineData(0, 1)]
        [InlineData(1, 0)]
        [InlineData(2, 0)]
        public static void TriangleWindowOscillatesCorrectly(float x, float expected)
        {
            TriangleResampler sampler = new TriangleResampler();
            float result = sampler.GetValue(x);

            Assert.Equal(result, expected);
        }

        [Theory]
        [InlineData(-2, 0)]
        [InlineData(-1, 0)]
        [InlineData(0, 1)]
        [InlineData(1, 0)]
        [InlineData(2, 0)]
        public static void Lanczos3WindowOscillatesCorrectly(float x, float expected)
        {
            Lanczos3Resampler sampler = new Lanczos3Resampler();
            float result = sampler.GetValue(x);

            Assert.Equal(result, expected);
        }

        [Theory]
        [InlineData(-4, 0)]
        [InlineData(-2, 0)]
        [InlineData(0, 1)]
        [InlineData(2, 0)]
        [InlineData(4, 0)]
        public static void Lanczos5WindowOscillatesCorrectly(float x, float expected)
        {
            Lanczos5Resampler sampler = new Lanczos5Resampler();
            float result = sampler.GetValue(x);

            Assert.Equal(result, expected);
        }
    }
}