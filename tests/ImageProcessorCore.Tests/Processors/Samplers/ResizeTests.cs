// <copyright file="ResizeTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Tests
{
    using System.IO;

    using Xunit;

    public class ResizeTests : FileTestBase
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
                { "MitchellNetravali", new MitchellNetravaliResampler() },
                //{ "Hermite", new HermiteResampler() },
                //{ "Spline", new SplineResampler() },
                //{ "Robidoux", new RobidouxResampler() },
                //{ "RobidouxSharp", new RobidouxSharpResampler() },
                //{ "RobidouxSoft", new RobidouxSoftResampler() },
                //{ "Welch", new WelchResampler() }
            };

        [Theory]
        [MemberData("ReSamplers")]
        public void ImageShouldResize(string name, IResampler sampler)
        {
            string path = CreateOutputDirectory("Resize");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName(name);
                Image image = file.CreateImage();

                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    image.Resize(image.Width / 2, image.Height / 2, sampler, true)
                    //image.Resize(555, 275, sampler, false)
                          .Save(output);
                }
            }
        }

        [Theory]
        [MemberData("ReSamplers")]
        public void ImageShouldResizeWidthAndKeepAspect(string name, IResampler sampler)
        {
            name = name + "-FixedWidth";

            string path = CreateOutputDirectory("Resize");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName(name);
                Image image = file.CreateImage();

                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    image.Resize(image.Width / 3, 0, sampler, false)
                          .Save(output);
                }
            }
        }

        [Theory]
        [MemberData("ReSamplers")]
        public void ImageShouldResizeHeightAndKeepAspect(string name, IResampler sampler)
        {
            name = name + "-FixedHeight";

            string path = CreateOutputDirectory("Resize");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName(name);
                Image image = file.CreateImage();

                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    image.Resize(0, image.Height / 3, sampler, false)
                          .Save(output);
                }
            }
        }

        [Theory]
        [MemberData("ReSamplers")]
        public void ImageShouldResizeWithCropWidthMode(string name, IResampler sampler)
        {
            name = name + "-CropWidth";

            string path = CreateOutputDirectory("Resize");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName(name);
                Image image = file.CreateImage();

                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    ResizeOptions options = new ResizeOptions()
                    {
                        Sampler = sampler,
                        Size = new Size(image.Width / 2, image.Height)
                    };

                    image.Resize(options)
                          .Save(output);
                }
            }
        }

        [Theory]
        [MemberData("ReSamplers")]
        public void ImageShouldResizeWithCropHeightMode(string name, IResampler sampler)
        {
            name = name + "-CropHeight";

            string path = CreateOutputDirectory("Resize");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName(name);
                Image image = file.CreateImage();

                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    ResizeOptions options = new ResizeOptions()
                    {
                        Sampler = sampler,
                        Size = new Size(image.Width, image.Height / 2)
                    };

                    image.Resize(options)
                          .Save(output);
                }
            }
        }

        [Theory]
        [MemberData("ReSamplers")]
        public void ImageShouldResizeWithPadMode(string name, IResampler sampler)
        {
            name = name + "-Pad";

            string path = CreateOutputDirectory("Resize");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName(name);
                Image image = file.CreateImage();

                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    ResizeOptions options = new ResizeOptions()
                    {
                        Size = new Size(image.Width + 200, image.Height),
                        Mode = ResizeMode.Pad
                    };

                    image.Resize(options)
                          .Save(output);
                }
            }
        }

        [Theory]
        [MemberData("ReSamplers")]
        public void ImageShouldResizeWithBoxPadMode(string name, IResampler sampler)
        {
            name = name + "-BoxPad";

            string path = CreateOutputDirectory("Resize");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName(name);
                Image image = file.CreateImage();

                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    ResizeOptions options = new ResizeOptions()
                    {
                        Sampler = sampler,
                        Size = new Size(image.Width + 200, image.Height + 200),
                        Mode = ResizeMode.BoxPad
                    };

                    image.Resize(options)
                          .Save(output);
                }
            }
        }

        [Theory]
        [MemberData("ReSamplers")]
        public void ImageShouldResizeWithMaxMode(string name, IResampler sampler)
        {
            name = name + "Max";

            string path = CreateOutputDirectory("Resize");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName(name);
                Image image = file.CreateImage();

                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    ResizeOptions options = new ResizeOptions()
                    {
                        Sampler = sampler,
                        Size = new Size(300, 300),
                        Mode = ResizeMode.Max
                    };

                    image.Resize(options)
                          .Save(output);
                }
            }
        }

        [Theory]
        [MemberData("ReSamplers")]
        public void ImageShouldResizeWithMinMode(string name, IResampler sampler)
        {
            name = name + "-Min";

            string path = CreateOutputDirectory("Resize");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName(name);
                Image image = file.CreateImage();

                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    ResizeOptions options = new ResizeOptions()
                    {
                        Sampler = sampler,
                        Size = new Size(image.Width - 50, image.Height - 25),
                        Mode = ResizeMode.Min
                    };

                    image.Resize(options)
                          .Save(output);
                }
            }
        }

        [Theory]
        [MemberData("ReSamplers")]
        public void ImageShouldResizeWithStretchMode(string name, IResampler sampler)
        {
            name = name + "Stretch";

            string path = CreateOutputDirectory("Resize");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName(name);
                Image image = file.CreateImage();

                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    ResizeOptions options = new ResizeOptions()
                    {
                        Sampler = sampler,
                        Size = new Size(image.Width - 200, image.Height),
                        Mode = ResizeMode.Stretch
                    };

                    image.Resize(options)
                          .Save(output);
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