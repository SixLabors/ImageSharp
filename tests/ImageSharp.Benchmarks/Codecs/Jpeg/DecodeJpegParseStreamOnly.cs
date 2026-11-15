// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
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

    // Both methods read from this stream. Each call rewinds it, so the results measure the decoders only.
    private MemoryStream inputStream;

    [GlobalSetup]
    public void Setup()
    {
        this.jpegBytes = File.ReadAllBytes(this.TestImageFullPath);
        this.inputStream = new MemoryStream(this.jpegBytes, false);
    }

    /// <summary>
    /// Releases the shared input stream.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        this.inputStream.Dispose();
        this.inputStream = null;
    }

    [Benchmark(Baseline = true, Description = "System.Drawing FULL")]
    public SDSize JpegSystemDrawing()
    {
        this.inputStream.Position = 0;
        using System.Drawing.Image image = System.Drawing.Image.FromStream(this.inputStream);
        return image.Size;
    }

    [Benchmark(Description = "JpegDecoderCore.ParseStream")]
    public void ParseStream()
    {
        this.inputStream.Position = 0;
        using BufferedReadStream bufferedStream = new(Configuration.Default, this.inputStream);
        JpegDecoderOptions options = new(new DecoderOptions(Configuration.Default) { SkipMetadata = true });

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
        public override void ConvertStrideBaseline(IccProfile iccProfile)
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

    /* Results 12.11.2026
    BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9818)
    AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
    .NET SDK 11.0.100-preview.7.26381.103
      [Host]     : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
      Job-RCQXAA : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

    Arguments=/p:DebugType=portable  IterationCount=3  LaunchCount=1
    WarmupCount=3

    | Method                      | TestImage             | Mean     | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
    |---------------------------- |---------------------- |---------:|----------:|----------:|------:|--------:|----------:|------------:|
    | 'System.Drawing FULL'       | Jpg/baseline/Lake.jpg | 2.761 ms | 0.6452 ms | 0.0354 ms |  1.00 |    0.02 |     330 B |        1.00 |
    | JpegDecoderCore.ParseStream | Jpg/baseline/Lake.jpg | 1.146 ms | 0.0844 ms | 0.0046 ms |  0.42 |    0.00 |   17085 B |       51.77 |

    Allocated reports managed memory only.
    */
}
