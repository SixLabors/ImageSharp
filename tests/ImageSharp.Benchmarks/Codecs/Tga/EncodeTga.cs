// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using BenchmarkDotNet.Attributes;
using ImageMagick;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.Codecs
{
    [Config(typeof(Config.ShortMultiFramework))]
    public class EncodeTga
    {
        private MagickImage tgaMagick;
        private Image<Rgba32> tga;

        private string TestImageFullPath
            => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImage);

        [Params(TestImages.Tga.Bit24BottomLeft)]
        public string TestImage { get; set; }

        [GlobalSetup]
        public void ReadImages()
        {
            if (this.tga == null)
            {
                this.tga = Image.Load<Rgba32>(this.TestImageFullPath);
                this.tgaMagick = new MagickImage(this.TestImageFullPath);
            }
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            this.tga.Dispose();
            this.tga = null;
            this.tgaMagick.Dispose();
        }

        [Benchmark(Baseline = true, Description = "Magick Tga")]
        public void MagickTga()
        {
            using var memoryStream = new MemoryStream();
            this.tgaMagick.Write(memoryStream, MagickFormat.Tga);
        }

        [Benchmark(Description = "ImageSharp Tga")]
        public void ImageSharpTga()
        {
            using var memoryStream = new MemoryStream();
            this.tga.SaveAsTga(memoryStream);
        }
    }
}
