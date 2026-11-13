// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg;

[Config(typeof(Config.Short))]
public class IdentifyJpeg
{
    private byte[] jpegBytes;

    // Identify reads from this stream. Each call rewinds it, so the results measure the decoder only.
    private MemoryStream inputStream;

    private string TestImageFullPath => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImage);

    [Params(TestImages.Jpeg.Baseline.Jpeg420Exif, TestImages.Jpeg.Baseline.Calliphora)]
    public string TestImage { get; set; }

    [GlobalSetup]
    public void ReadImages()
    {
        this.jpegBytes ??= File.ReadAllBytes(this.TestImageFullPath);
        this.inputStream ??= new MemoryStream(this.jpegBytes, false);
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

    [Benchmark]
    public ImageInfo Identify()
    {
        this.inputStream.Position = 0;
        return JpegDecoder.Instance.Identify(DecoderOptions.Default, this.inputStream);
    }

    /* Results 12.11.2026
    BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9818)
    AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
    .NET SDK 11.0.100-preview.7.26381.103
      [Host]     : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
      Job-RCQXAA : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

    Arguments=/p:DebugType=portable  IterationCount=3  LaunchCount=1
    WarmupCount=3

    | Method   | TestImage                    | Mean       | Error      | StdDev   | Gen0   | Gen1   | Allocated |
    |--------- |----------------------------- |-----------:|-----------:|---------:|-------:|-------:|----------:|
    | Identify | Jpg/baseline/Calliphora.jpg  |   845.7 ns | 1,059.6 ns | 58.08 ns | 0.6514 | 0.0248 |  10.64 KB |
    | Identify | Jpg/baseline/jpeg420exif.jpg | 5,527.5 ns | 1,496.7 ns | 82.04 ns | 1.7319 | 1.6327 |  28.39 KB |

    Allocated reports managed memory only.
    */
}
