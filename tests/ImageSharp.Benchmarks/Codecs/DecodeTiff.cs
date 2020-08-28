// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;
using SDImage = System.Drawing.Image;
using SDSize = System.Drawing.Size;

namespace SixLabors.ImageSharp.Benchmarks.Codecs
{
    [Config(typeof(Config.ShortClr))]
    public class DecodeTiff : BenchmarkBase
    {
        private byte[] tiffBytes;

        private string TestImageFullPath => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImage);

        [Params(TestImages.Tiff.RgbPackbits)]
        public string TestImage { get; set; }

        [GlobalSetup]
        public void ReadImages()
        {
            if (this.tiffBytes == null)
            {
                this.tiffBytes = File.ReadAllBytes(this.TestImageFullPath);
            }
        }

        [Benchmark(Baseline = true, Description = "System.Drawing Tiff")]
        public SDSize TiffSystemDrawing()
        {
            using (var memoryStream = new MemoryStream(this.tiffBytes))
            using (var image = SDImage.FromStream(memoryStream))
            {
                return image.Size;
            }
        }

        [Benchmark(Description = "ImageSharp Tiff")]
        public Size TiffCore()
        {
            using (var memoryStream = new MemoryStream(this.tiffBytes))
            using (var image = Image.Load<Rgba32>(memoryStream))
            {
                return image.Size();
            }
        }
    }
}
