// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests;
using SDImage = System.Drawing.Image;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg
{
    [Config(typeof(Config.ShortMultiFramework))]
    public class LoadResizeSave_ImageSpecific
    {
        private readonly Configuration configuration = new Configuration(new JpegConfigurationModule());

        private byte[] sourceBytes;

        private byte[] destBytes;

        private string TestImageFullPath => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImage);

        [Params(
            TestImages.Jpeg.BenchmarkSuite.Lake_Small444YCbCr,
            TestImages.Jpeg.BenchmarkSuite.BadRstProgressive518_Large444YCbCr,
            TestImages.Jpeg.BenchmarkSuite.Jpeg420Exif_MidSizeYCbCr)]

        public string TestImage { get; set; }

        [Params(false, true)]
        public bool ParallelExec { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            this.configuration.MaxDegreeOfParallelism =
                this.ParallelExec ? Environment.ProcessorCount : 1;

            this.sourceBytes = File.ReadAllBytes(this.TestImageFullPath);

            this.destBytes = new byte[this.sourceBytes.Length * 2];
        }

        [Benchmark(Baseline = true)]
        public void SystemDrawing()
        {
            using var sourceStream = new MemoryStream(this.sourceBytes);
            using var destStream = new MemoryStream(this.destBytes);
            using var source = SDImage.FromStream(sourceStream);
            using var destination = new Bitmap(source.Width / 4, source.Height / 4);
            using (var g = Graphics.FromImage(destination))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.DrawImage(source, 0, 0, 400, 400);
            }

            destination.Save(destStream, ImageFormat.Jpeg);
        }

        [Benchmark]
        public void ImageSharp()
        {
            using (var source = Image.Load(this.configuration, this.sourceBytes, new JpegDecoder { IgnoreMetadata = true }))
            using (var destStream = new MemoryStream(this.destBytes))
            {
                source.Mutate(c => c.Resize(source.Width / 4, source.Height / 4));
                source.SaveAsJpeg(destStream);
            }
        }

        // RESULTS (2018 October 31):
        //
        // BenchmarkDotNet=v0.10.14, OS=Windows 10.0.17134
        // Intel Core i7-7700HQ CPU 2.80GHz (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
        // Frequency=2742191 Hz, Resolution=364.6719 ns, Timer=TSC
        // .NET Core SDK=2.1.403
        //   [Host]     : .NET Core 2.1.5 (CoreCLR 4.6.26919.02, CoreFX 4.6.26919.02), 64bit RyuJIT
        //   Job-ZPEZGV : .NET Framework 4.7.1 (CLR 4.0.30319.42000), 64bit RyuJIT-v4.7.3190.0
        //   Job-SGOCJT : .NET Core 2.1.5 (CoreCLR 4.6.26919.02, CoreFX 4.6.26919.02), 64bit RyuJIT
        //
        //         Method | Runtime |                    TestImage | ParallelExec |      Mean |     Error |    StdDev | Scaled | ScaledSD |    Gen 0 | Allocated |
        // -------------- |-------- |----------------------------- |------------- |----------:|----------:|----------:|-------:|---------:|---------:|----------:|
        //  SystemDrawing |     Clr | Jpg/baseline/jpeg420exif.jpg |        False |  64.88 ms |  3.735 ms | 0.2110 ms |   1.00 |     0.00 | 250.0000 | 791.07 KB |
        //     ImageSharp |     Clr | Jpg/baseline/jpeg420exif.jpg |        False | 129.53 ms | 23.423 ms | 1.3234 ms |   2.00 |     0.02 |        - |  50.09 KB |
        //                |         |                              |              |           |           |           |        |          |          |           |
        //  SystemDrawing |    Core | Jpg/baseline/jpeg420exif.jpg |        False |  65.87 ms | 10.488 ms | 0.5926 ms |   1.00 |     0.00 | 250.0000 | 789.79 KB |
        //     ImageSharp |    Core | Jpg/baseline/jpeg420exif.jpg |        False |  92.00 ms |  7.241 ms | 0.4091 ms |   1.40 |     0.01 |        - |  46.36 KB |
        //                |         |                              |              |           |           |           |        |          |          |           |
        //  SystemDrawing |     Clr | Jpg/baseline/jpeg420exif.jpg |         True |  64.23 ms |  5.998 ms | 0.3389 ms |   1.00 |     0.00 | 250.0000 | 791.07 KB |
        //     ImageSharp |     Clr | Jpg/baseline/jpeg420exif.jpg |         True |  82.63 ms | 29.320 ms | 1.6566 ms |   1.29 |     0.02 |        - |  57.59 KB |
        //                |         |                              |              |           |           |           |        |          |          |           |
        //  SystemDrawing |    Core | Jpg/baseline/jpeg420exif.jpg |         True |  64.20 ms |  6.560 ms | 0.3707 ms |   1.00 |     0.00 | 250.0000 | 789.79 KB |
        //     ImageSharp |    Core | Jpg/baseline/jpeg420exif.jpg |         True |  68.08 ms | 18.376 ms | 1.0383 ms |   1.06 |     0.01 |        - |  50.49 KB |
    }
}
