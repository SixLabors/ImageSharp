// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using BenchmarkDotNet.Attributes;
using System.Drawing;
using System.IO;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg
{
    [Config(typeof(Config.ShortClr))]
    public class DecodeJpegParseStreamOnly
    {
        [Params(TestImages.Jpeg.Baseline.Jpeg420Exif)]
        public string TestImage { get; set; }

        private string TestImageFullPath => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImage);

        private byte[] jpegBytes;

        [GlobalSetup]
        public void Setup()
        {
            this.jpegBytes = File.ReadAllBytes(this.TestImageFullPath);
        }

        [Benchmark(Baseline = true, Description = "System.Drawing FULL")]
        public Size JpegSystemDrawing()
        {
            using (var memoryStream = new MemoryStream(this.jpegBytes))
            {
                using (var image = System.Drawing.Image.FromStream(memoryStream))
                {
                    return image.Size;
                }
            }
        }

        [Benchmark(Description = "JpegDecoderCore.ParseStream")]
        public void ParseStreamPdfJs()
        {
            using (var memoryStream = new MemoryStream(this.jpegBytes))
            {
                var decoder = new JpegDecoderCore(Configuration.Default, new Formats.Jpeg.JpegDecoder { IgnoreMetadata = true });
                decoder.ParseStream(memoryStream);
                decoder.Dispose();
            }
        }

        // RESULTS (2019 April 23):
        //
        // BenchmarkDotNet=v0.11.3, OS=Windows 10.0.17763.437 (1809/October2018Update/Redstone5)
        // Intel Core i7-6600U CPU 2.60GHz (Skylake), 1 CPU, 4 logical and 2 physical cores
        // .NET Core SDK=2.2.202
        //  [Host] : .NET Core 2.1.9 (CoreCLR 4.6.27414.06, CoreFX 4.6.27415.01), 64bit RyuJIT
        //  Clr    : .NET Framework 4.7.2 (CLR 4.0.30319.42000), 64bit RyuJIT-v4.7.3362.0
        //  Core   : .NET Core 2.1.9 (CoreCLR 4.6.27414.06, CoreFX 4.6.27415.01), 64bit RyuJIT
        //
        // |                      Method |  Job | Runtime |            TestImage |     Mean |    Error |    StdDev | Ratio | RatioSD |    Gen 0 | Gen 1 | Gen 2 | Allocated |
        // |---------------------------- |----- |-------- |--------------------- |---------:|---------:|----------:|------:|--------:|---------:|------:|------:|----------:|
        // |       'System.Drawing FULL' |  Clr |     Clr | Jpg/b(...)f.jpg [28] | 18.69 ms | 8.273 ms | 0.4535 ms |  1.00 |    0.00 | 343.7500 |     - |     - | 757.89 KB |
        // | JpegDecoderCore.ParseStream |  Clr |     Clr | Jpg/b(...)f.jpg [28] | 15.76 ms | 4.266 ms | 0.2339 ms |  0.84 |    0.03 |        - |     - |     - |  11.83 KB |
        // |                             |      |         |                      |          |          |           |       |         |          |       |       |           |
        // |       'System.Drawing FULL' | Core |    Core | Jpg/b(...)f.jpg [28] | 17.68 ms | 2.711 ms | 0.1486 ms |  1.00 |    0.00 | 343.7500 |     - |     - | 757.04 KB |
        // | JpegDecoderCore.ParseStream | Core |    Core | Jpg/b(...)f.jpg [28] | 14.27 ms | 3.671 ms | 0.2012 ms |  0.81 |    0.00 |        - |     - |     - |  11.76 KB |
    }
}
