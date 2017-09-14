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

    using SixLabors.ImageSharp.Formats.Jpeg.GolangPort;
    using SixLabors.ImageSharp.Formats.Jpeg.PdfJsPort;
    using SixLabors.ImageSharp.Tests;

    using CoreImage = ImageSharp.Image;

    using CoreSize = SixLabors.Primitives.Size;
    
    [Config(typeof(Config.Short))]
    public class DecodeJpeg : BenchmarkBase
    {
        private byte[] jpegBytes;

        private string TestImageFullPath => Path.Combine(
            TestEnvironment.InputImagesDirectoryFullPath,
            this.TestImage);

        [Params(TestImages.Jpeg.Baseline.Jpeg420Exif, TestImages.Jpeg.Baseline.Calliphora)]
        public string TestImage { get; set; }

        [GlobalSetup]
        public void ReadImages()
        {
            if (this.jpegBytes == null)
            {
                this.jpegBytes = File.ReadAllBytes(this.TestImageFullPath);
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
        public CoreSize JpegImageSharpOrig()
        {
            using (MemoryStream memoryStream = new MemoryStream(this.jpegBytes))
            {
                using (Image<Rgba32> image = CoreImage.Load<Rgba32>(memoryStream, new OrigJpegDecoder()))
                {
                    return new CoreSize(image.Width, image.Height);
                }
            }
        }

        [Benchmark(Description = "Decode Jpeg - ImageSharp PdfJs")]
        public CoreSize JpegImageSharpPdfJs()
        {
            using (MemoryStream memoryStream = new MemoryStream(this.jpegBytes))
            {
                using (Image<Rgba32> image = CoreImage.Load<Rgba32>(memoryStream, new PdfJsJpegDecoder()))
                {
                    return new CoreSize(image.Width, image.Height);
                }
            }
        }
    }
}
