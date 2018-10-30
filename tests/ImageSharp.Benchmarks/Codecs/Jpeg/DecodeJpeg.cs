// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Drawing;
using System.IO;
using BenchmarkDotNet.Attributes;

using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;
using CoreSize = SixLabors.Primitives.Size;
using SDImage = System.Drawing.Image;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg
{
    [Config(typeof(Config.ShortClr))]
    public class DecodeJpeg
    {
        private byte[] jpegBytes;

        private string TestImageFullPath => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImage);

        [Params(TestImages.Jpeg.Baseline.Jpeg420Exif
            //, TestImages.Jpeg.Baseline.Calliphora
            )]
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
            using (var memoryStream = new MemoryStream(this.jpegBytes))
            {
                using (var image = SDImage.FromStream(memoryStream))
                {
                    return image.Size;
                }
            }
        }

        [Benchmark(Description = "Decode Jpeg - ImageSharp")]
        public CoreSize JpegImageSharp()
        {
            using (var memoryStream = new MemoryStream(this.jpegBytes))
            {
                using (var image = Image.Load<Rgba32>(memoryStream, new JpegDecoder()))
                {
                    return new CoreSize(image.Width, image.Height);
                }
            }
        }

        // RESULTS (2018 October):
        //
        // BenchmarkDotNet=v0.10.14, OS=Windows 10.0.17134
        // Intel Core i7-7700HQ CPU 2.80GHz (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
        // Frequency=2742191 Hz, Resolution=364.6719 ns, Timer=TSC
        // .NET Core SDK=2.1.403
        //   [Host]     : .NET Core 2.1.5 (CoreCLR 4.6.26919.02, CoreFX 4.6.26919.02), 64bit RyuJIT
        //   Job-MCUBGX : .NET Framework 4.7.1 (CLR 4.0.30319.42000), 64bit RyuJIT-v4.7.3190.0
        //   Job-TZIRPF : .NET Core 2.1.5 (CoreCLR 4.6.26919.02, CoreFX 4.6.26919.02), 64bit RyuJIT
        // 
        // 
        //                          Method | Runtime |                    TestImage |     Mean |     Error |    StdDev | Scaled | ScaledSD |    Gen 0 | Allocated |
        // ------------------------------- |-------- |----------------------------- |---------:|----------:|----------:|-------:|---------:|---------:|----------:|
        //  'Decode Jpeg - System.Drawing' |     Clr | Jpg/baseline/jpeg420exif.jpg | 17.10 ms |  4.720 ms | 0.2667 ms |   1.00 |     0.00 | 218.7500 | 757.88 KB |
        //      'Decode Jpeg - ImageSharp' |     Clr | Jpg/baseline/jpeg420exif.jpg | 57.77 ms |  4.626 ms | 0.2614 ms |   3.38 |     0.04 | 125.0000 | 566.01 KB |
        //                                 |         |                              |          |           |           |        |          |          |           |
        //  'Decode Jpeg - System.Drawing' |    Core | Jpg/baseline/jpeg420exif.jpg | 17.45 ms |  2.092 ms | 0.1182 ms |   1.00 |     0.00 | 218.7500 | 757.04 KB |
        //      'Decode Jpeg - ImageSharp' |    Core | Jpg/baseline/jpeg420exif.jpg | 48.39 ms | 14.562 ms | 0.8228 ms |   2.77 |     0.04 | 125.0000 | 529.96 KB |
    }
}
