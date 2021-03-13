// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;
using SDImage = System.Drawing.Image;
using SDSize = System.Drawing.Size;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg
{
    /// <summary>
    /// Image-specific Jpeg benchmarks
    /// </summary>
    [Config(typeof(Config.ShortMultiFramework))]
    public class DecodeJpeg_ImageSpecific
    {
        private byte[] jpegBytes;

        private string TestImageFullPath => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImage);

#pragma warning disable SA1115
        [Params(
            TestImages.Jpeg.BenchmarkSuite.Lake_Small444YCbCr,
            TestImages.Jpeg.BenchmarkSuite.BadRstProgressive518_Large444YCbCr,

            // The scaled result for the large image "ExifGetString750Transform_Huge420YCbCr"
            // is almost the same as the result for Jpeg420Exif,
            // which proves that the execution time for the most common YCbCr 420 path scales linearly.
            // TestImages.Jpeg.BenchmarkSuite.ExifGetString750Transform_Huge420YCbCr,
            TestImages.Jpeg.BenchmarkSuite.Jpeg420Exif_MidSizeYCbCr)]

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
        public SDSize JpegSystemDrawing()
        {
            using (var memoryStream = new MemoryStream(this.jpegBytes))
            {
                using (var image = SDImage.FromStream(memoryStream))
                {
                    return image.Size;
                }
            }
        }

        [Benchmark(Description = "Decode Jpeg - ImageSharp")]
        public Size JpegImageSharp()
        {
            using (var memoryStream = new MemoryStream(this.jpegBytes))
            {
                using (var image = Image.Load<Rgba32>(memoryStream, new JpegDecoder { IgnoreMetadata = true }))
                {
                    return new Size(image.Width, image.Height);
                }
            }
        }

        /*
        |                         Method |            TestImage |       Mean |       Error |     StdDev | Ratio | RatioSD | Gen 0 | Gen 1 | Gen 2 |  Allocated |
        |------------------------------- |--------------------- |-----------:|------------:|-----------:|------:|--------:|------:|------:|------:|-----------:|
        | 'Decode Jpeg - System.Drawing' | Jpg/b(...)e.jpg [21] |   5.122 ms |   1.3978 ms |  0.0766 ms |  1.00 |    0.00 |     - |     - |     - |      176 B |
        |     'Decode Jpeg - ImageSharp' | Jpg/b(...)e.jpg [21] |  11.991 ms |   0.2514 ms |  0.0138 ms |  2.34 |    0.03 |     - |     - |     - |    15816 B |
        |                                |                      |            |             |            |       |         |       |       |       |            |
        | 'Decode Jpeg - System.Drawing' | Jpg/b(...)f.jpg [28] |  14.943 ms |   1.8410 ms |  0.1009 ms |  1.00 |    0.00 |     - |     - |     - |      176 B |
        |     'Decode Jpeg - ImageSharp' | Jpg/b(...)f.jpg [28] |  29.759 ms |   1.5452 ms |  0.0847 ms |  1.99 |    0.01 |     - |     - |     - |    16824 B |
        |                                |                      |            |             |            |       |         |       |       |       |            |
        | 'Decode Jpeg - System.Drawing' | Jpg/i(...)e.jpg [43] | 388.229 ms | 382.8946 ms | 20.9877 ms |  1.00 |    0.00 |     - |     - |     - |      216 B |
        |     'Decode Jpeg - ImageSharp' | Jpg/i(...)e.jpg [43] | 276.490 ms | 195.5104 ms | 10.7166 ms |  0.71 |    0.01 |     - |     - |     - | 36022368 B |
         */
    }
}
