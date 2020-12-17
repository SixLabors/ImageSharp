// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Tests;
using SDSize = System.Drawing.Size;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg
{
    [Config(typeof(Config.ShortMultiFramework))]
    public class DecodeJpegParseStreamOnly
    {
        [Params(TestImages.Jpeg.BenchmarkSuite.Lake_Small444YCbCr)]
        public string TestImage { get; set; }

        private string TestImageFullPath => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImage);

        private byte[] jpegBytes;

        [GlobalSetup]
        public void Setup()
            => this.jpegBytes = File.ReadAllBytes(this.TestImageFullPath);

        [Benchmark(Baseline = true, Description = "System.Drawing FULL")]
        public SDSize JpegSystemDrawing()
        {
            using var memoryStream = new MemoryStream(this.jpegBytes);
            using var image = System.Drawing.Image.FromStream(memoryStream);
            return image.Size;
        }

        [Benchmark(Description = "JpegDecoderCore.ParseStream")]
        public void ParseStream()
        {
            using var memoryStream = new MemoryStream(this.jpegBytes);
            using var bufferedStream = new BufferedReadStream(Configuration.Default, memoryStream);

            var decoder = new JpegDecoderCore(Configuration.Default, new JpegDecoder { IgnoreMetadata = true });
            decoder.ParseStream(bufferedStream);
            decoder.Dispose();
        }
    }

    /*
    |                      Method |        Job |       Runtime |            TestImage |     Mean |     Error |    StdDev | Ratio |   Gen 0 | Gen 1 | Gen 2 | Allocated |
    |---------------------------- |----------- |-------------- |--------------------- |---------:|----------:|----------:|------:|--------:|------:|------:|----------:|
    |       'System.Drawing FULL' | Job-HITJFX |    .NET 4.7.2 | Jpg/b(...)e.jpg [21] | 5.828 ms | 0.9885 ms | 0.0542 ms |  1.00 | 46.8750 |     - |     - |  211566 B |
    | JpegDecoderCore.ParseStream | Job-HITJFX |    .NET 4.7.2 | Jpg/b(...)e.jpg [21] | 5.833 ms | 0.2923 ms | 0.0160 ms |  1.00 |       - |     - |     - |   12416 B |
    |                             |            |               |                      |          |           |           |       |         |       |       |           |
    |       'System.Drawing FULL' | Job-WPSKZD | .NET Core 2.1 | Jpg/b(...)e.jpg [21] | 6.018 ms | 2.1374 ms | 0.1172 ms |  1.00 | 46.8750 |     - |     - |  210768 B |
    | JpegDecoderCore.ParseStream | Job-WPSKZD | .NET Core 2.1 | Jpg/b(...)e.jpg [21] | 4.382 ms | 0.9009 ms | 0.0494 ms |  0.73 |       - |     - |     - |   12360 B |
    |                             |            |               |                      |          |           |           |       |         |       |       |           |
    |       'System.Drawing FULL' | Job-ZLSNRP | .NET Core 3.1 | Jpg/b(...)e.jpg [21] | 5.714 ms | 0.4078 ms | 0.0224 ms |  1.00 |       - |     - |     - |     176 B |
    | JpegDecoderCore.ParseStream | Job-ZLSNRP | .NET Core 3.1 | Jpg/b(...)e.jpg [21] | 4.239 ms | 1.0943 ms | 0.0600 ms |  0.74 |       - |     - |     - |   12406 B |
    */
}
