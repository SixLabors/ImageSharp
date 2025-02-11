// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Tests;
using SDSize = System.Drawing.Size;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg;

[Config(typeof(Config.Short))]
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
        using MemoryStream memoryStream = new(this.jpegBytes);
        using System.Drawing.Image image = System.Drawing.Image.FromStream(memoryStream);
        return image.Size;
    }

    [Benchmark(Description = "JpegDecoderCore.ParseStream")]
    public void ParseStream()
    {
        using MemoryStream memoryStream = new(this.jpegBytes);
        using BufferedReadStream bufferedStream = new(Configuration.Default, memoryStream);
        JpegDecoderOptions options = new() { GeneralOptions = new() { SkipMetadata = true } };

        using JpegDecoderCore decoder = new(options);
        NoopSpectralConverter spectralConverter = new();
        decoder.ParseStream(bufferedStream, spectralConverter, cancellationToken: default);
    }

    // We want to test only stream parsing and scan decoding, we don't need to convert spectral data to actual pixels
    // Nor we need to allocate final pixel buffer
    // Note: this still introduces virtual method call overhead for baseline interleaved images
    // There's no way to eliminate it as spectral conversion is built into the scan decoding loop for memory footprint reduction
    private sealed class NoopSpectralConverter : SpectralConverter
    {
        public override void ConvertStrideBaseline()
        {
        }

        public override bool HasPixelBuffer() => throw new NotImplementedException();

        public override void InjectFrameData(JpegFrame frame, IRawJpegData jpegData)
        {
        }

        public override void PrepareForDecoding()
        {
        }
    }
}

/*
BenchmarkDotNet=v0.13.0, OS=Windows 10.0.19042.1083 (20H2/October2020Update)
Intel Core i7-6700K CPU 4.00GHz (Skylake), 1 CPU, 8 logical and 4 physical cores
.NET SDK=6.0.100-preview.3.21202.5
[Host]     : .NET Core 3.1.13 (CoreCLR 4.700.21.11102, CoreFX 4.700.21.11602), X64 RyuJIT
Job-VAJCIU : .NET Core 2.1.26 (CoreCLR 4.6.29812.02, CoreFX 4.6.29812.01), X64 RyuJIT
Job-INPXCR : .NET Core 3.1.13 (CoreCLR 4.700.21.11102, CoreFX 4.700.21.11602), X64 RyuJIT
Job-JRCLOJ : .NET Framework 4.8 (4.8.4390.0), X64 RyuJIT

IterationCount=3  LaunchCount=1  WarmupCount=3
|                      Method |        Job |              Runtime |             TestImage |     Mean |     Error |    StdDev | Ratio |   Gen 0 | Gen 1 | Gen 2 | Allocated |
|---------------------------- |----------- |--------------------- |---------------------- |---------:|----------:|----------:|------:|--------:|------:|------:|----------:|
|       'System.Drawing FULL' | Job-VAJCIU |        .NET Core 2.1 | Jpg/baseline/Lake.jpg | 5.196 ms | 0.7520 ms | 0.0412 ms |  1.00 | 46.8750 |     - |     - | 210,768 B |
| JpegDecoderCore.ParseStream | Job-VAJCIU |        .NET Core 2.1 | Jpg/baseline/Lake.jpg | 3.467 ms | 0.0784 ms | 0.0043 ms |  0.67 |       - |     - |     - |  12,416 B |
|                             |            |                      |                       |          |           |           |       |         |       |       |           |
|       'System.Drawing FULL' | Job-INPXCR |        .NET Core 3.1 | Jpg/baseline/Lake.jpg | 5.201 ms | 0.4105 ms | 0.0225 ms |  1.00 |       - |     - |     - |     183 B |
| JpegDecoderCore.ParseStream | Job-INPXCR |        .NET Core 3.1 | Jpg/baseline/Lake.jpg | 3.349 ms | 0.0468 ms | 0.0026 ms |  0.64 |       - |     - |     - |  12,408 B |
|                             |            |                      |                       |          |           |           |       |         |       |       |           |
|       'System.Drawing FULL' | Job-JRCLOJ | .NET Framework 4.7.2 | Jpg/baseline/Lake.jpg | 5.164 ms | 0.6524 ms | 0.0358 ms |  1.00 | 46.8750 |     - |     - | 211,571 B |
| JpegDecoderCore.ParseStream | Job-JRCLOJ | .NET Framework 4.7.2 | Jpg/baseline/Lake.jpg | 4.548 ms | 0.3357 ms | 0.0184 ms |  0.88 |       - |     - |     - |  12,480 B |
*/
