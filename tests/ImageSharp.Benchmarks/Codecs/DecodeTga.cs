// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;

using BenchmarkDotNet.Attributes;

using ImageMagick;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Benchmarks.Codecs
{
    [Config(typeof(Config.ShortClr))]
    public class DecodeTga : BenchmarkBase
    {
        private string TestImageFullPath => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImage);

        [Params(TestImages.Tga.Bit24)]
        public string TestImage { get; set; }

        [Benchmark(Baseline = true, Description = "ImageMagick Tga")]
        public Size TgaImageMagick()
        {
            using (var magickImage = new MagickImage(this.TestImageFullPath))
            {
                return new Size(magickImage.Width, magickImage.Height);
            }
        }

        [Benchmark(Description = "ImageSharp Tga")]
        public Size TgaCore()
        {
            using (var image = Image.Load<Rgba32>(this.TestImageFullPath))
            {
                return new Size(image.Width, image.Height);
            }
        }
    }
}
