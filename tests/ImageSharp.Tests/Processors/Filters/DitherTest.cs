// <copyright file="DitherTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System.IO;

    using ImageSharp.Dithering;
    using ImageSharp.Dithering.Ordered;

    using Xunit;

    public class DitherTest : FileTestBase
    {
        public static readonly TheoryData<string, IOrderedDither> Ditherers = new TheoryData<string, IOrderedDither>
        {
            { "Ordered", new Ordered() },
            { "Bayer", new Bayer() }
        };

        public static readonly TheoryData<string, IErrorDiffuser> ErrorDiffusers = new TheoryData<string, IErrorDiffuser>
        {
            { "Atkinson", new Atkinson() },
            { "Burks", new Burks() },
            { "FloydSteinberg", new FloydSteinberg() },
            { "JarvisJudiceNinke", new JarvisJudiceNinke() },
            { "Sierra2", new Sierra2() },
            { "Sierra3", new Sierra3() },
            { "SierraLite", new SierraLite() },
            { "Stucki", new Stucki() },
        };

        [Theory]
        [MemberData(nameof(Ditherers))]
        public void ImageShouldApplyDitherFilter(string name, IOrderedDither ditherer)
        {
            string path = this.CreateOutputDirectory("Dither", "Dither");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName(name);
                using (Image image = file.CreateImage())
                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    image.Dither(ditherer).Save(output);
                }
            }
        }

        [Theory]
        [MemberData(nameof(Ditherers))]
        public void ImageShouldApplyDitherFilterInBox(string name, IOrderedDither ditherer)
        {
            string path = this.CreateOutputDirectory("Dither", "Dither");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName($"{name}-InBox");
                using (Image image = file.CreateImage())
                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    image.Dither(ditherer, new Rectangle(10, 10, image.Width / 2, image.Height / 2)).Save(output);
                }
            }
        }

        [Theory]
        [MemberData(nameof(ErrorDiffusers))]
        public void ImageShouldApplyDiffusionFilter(string name, IErrorDiffuser diffuser)
        {
            string path = this.CreateOutputDirectory("Dither", "Diffusion");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName(name);
                using (Image image = file.CreateImage())
                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    image.Dither(diffuser, .5F).Save(output);
                }
            }
        }

        [Theory]
        [MemberData(nameof(ErrorDiffusers))]
        public void ImageShouldApplyDiffusionFilterInBox(string name, IErrorDiffuser diffuser)
        {
            string path = this.CreateOutputDirectory("Dither", "Diffusion");

            foreach (TestFile file in Files)
            {
                string filename = file.GetFileName($"{name}-InBox");
                using (Image image = file.CreateImage())
                using (FileStream output = File.OpenWrite($"{path}/{filename}"))
                {
                    image.Dither(diffuser, .5F, new Rectangle(10, 10, image.Width / 2, image.Height / 2)).Save(output);
                }
            }
        }
    }
}