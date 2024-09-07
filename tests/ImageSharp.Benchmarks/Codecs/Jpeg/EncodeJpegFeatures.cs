// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg;

/// <summary>
/// Benchmark for all available encoding features of the Jpeg file type.
/// </summary>
/// <remarks>
/// This benchmark does NOT compare ImageSharp to any other jpeg codecs.
/// </remarks>
public class EncodeJpegFeatures
{
    // Big enough, 4:4:4 chroma sampling
    // No metadata
    private const string TestImage = TestImages.Jpeg.Baseline.Calliphora;

    public static IEnumerable<JpegColorType> ColorSpaceValues => new[]
    {
        JpegColorType.Luminance,
        JpegColorType.Rgb,
        JpegColorType.YCbCrRatio420,
        JpegColorType.YCbCrRatio444,
    };

    [Params(75, 90, 100)]
    public int Quality;

    [ParamsSource(nameof(ColorSpaceValues), Priority = -100)]
    public JpegColorType TargetColorSpace;

    private Image<Rgb24> bmpCore;
    private JpegEncoder encoder;

    private MemoryStream destinationStream;

    [GlobalSetup]
    public void Setup()
    {
        using FileStream imageBinaryStream = File.OpenRead(Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, TestImage));
        this.bmpCore = Image.Load<Rgb24>(imageBinaryStream);
        this.encoder = new()
        {
            Quality = this.Quality,
            ColorType = this.TargetColorSpace,
            Interleaved = true,
        };
        this.destinationStream = new();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.bmpCore.Dispose();
        this.bmpCore = null;

        this.destinationStream.Dispose();
        this.destinationStream = null;
    }

    [Benchmark]
    public void Benchmark()
    {
        this.bmpCore.SaveAsJpeg(this.destinationStream, this.encoder);
        this.destinationStream.Seek(0, SeekOrigin.Begin);
    }
}

/*
BenchmarkDotNet=v0.13.0, OS=Windows 10.0.19044
Intel Core i7-6700K CPU 4.00GHz (Skylake), 1 CPU, 8 logical and 4 physical cores
.NET SDK=6.0.202
[Host]     : .NET 6.0.4 (6.0.422.16404), X64 RyuJIT
DefaultJob : .NET 6.0.4 (6.0.422.16404), X64 RyuJIT


|    Method | TargetColorSpace | Quality |      Mean |     Error |    StdDev |
|---------- |----------------- |-------- |----------:|----------:|----------:|
| Benchmark |        Luminance |      75 |  4.618 ms | 0.0263 ms | 0.0233 ms |
| Benchmark |              Rgb |      75 | 12.543 ms | 0.0650 ms | 0.0608 ms |
| Benchmark |    YCbCrRatio420 |      75 |  6.639 ms | 0.0778 ms | 0.1256 ms |
| Benchmark |    YCbCrRatio444 |      75 |  8.590 ms | 0.0570 ms | 0.0505 ms |
| Benchmark |        Luminance |      90 |  4.902 ms | 0.0307 ms | 0.0288 ms |
| Benchmark |              Rgb |      90 | 13.447 ms | 0.0468 ms | 0.0415 ms |
| Benchmark |    YCbCrRatio420 |      90 |  7.218 ms | 0.0586 ms | 0.0548 ms |
| Benchmark |    YCbCrRatio444 |      90 |  9.150 ms | 0.0779 ms | 0.0729 ms |
| Benchmark |        Luminance |     100 |  6.731 ms | 0.0325 ms | 0.0304 ms |
| Benchmark |              Rgb |     100 | 19.831 ms | 0.1009 ms | 0.0788 ms |
| Benchmark |    YCbCrRatio420 |     100 | 10.541 ms | 0.0423 ms | 0.0396 ms |
| Benchmark |    YCbCrRatio444 |     100 | 15.345 ms | 0.3276 ms | 0.3065 ms |
*/
