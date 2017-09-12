// <copyright file="DecodeJpeg.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace SixLabors.ImageSharp.Benchmarks.Image
{
    using System.Drawing;
    using System.IO;

    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Attributes.Jobs;

    using SixLabors.ImageSharp.Tests;

    using CoreImage = ImageSharp.Image;

    using CoreSize = SixLabors.Primitives.Size;
    
    [Config(typeof(Config.Short))]
    public class DecodeJpeg : BenchmarkBase
    {
        private byte[] jpegBytes;

        private static readonly string TestImage = Path.Combine(
            TestEnvironment.InputImagesDirectoryFullPath,
            TestImages.Jpeg.Baseline.Calliphora);

        [GlobalSetup]
        public void ReadImages()
        {
            if (this.jpegBytes == null)
            {
                this.jpegBytes = File.ReadAllBytes(TestImage);
            }
        }

        [Benchmark(Baseline = true, Description = "Decode Jpeg - System.Drawing")]
        public Size JpegSystemDrawing()
        {
            using (MemoryStream memoryStream = new MemoryStream(this.jpegBytes))
            {
                using (Image image = Image.FromStream(memoryStream))
                {
                    return image.Size;
                }
            }
        }

        [Benchmark(Description = "Decode Jpeg - ImageSharp")]
        public CoreSize JpegCore()
        {
            using (MemoryStream memoryStream = new MemoryStream(this.jpegBytes))
            {
                using (Image<Rgba32> image = CoreImage.Load<Rgba32>(memoryStream))
                {
                    return new CoreSize(image.Width, image.Height);
                }
            }
        }
    }
}
