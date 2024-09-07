// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;
using SkiaSharp;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg;

/// <summary>
/// Benchmark for performance comparison between other codecs.
/// </summary>
/// <remarks>
/// This benchmarks tests baseline 4:2:0 chroma sampling path.
/// </remarks>
public class EncodeJpegComparison
{
    // Big enough, 4:4:4 chroma sampling
    private const string TestImage = TestImages.Jpeg.Baseline.Calliphora;

    // Change/add parameters for extra benchmarks
    [Params(75, 90, 100)]
    public int Quality;

    private MemoryStream destinationStream;

    // ImageSharp
    private Image<Rgba32> imageImageSharp;
    private JpegEncoder encoderImageSharp;

    // SkiaSharp
    private SKBitmap imageSkiaSharp;

    [GlobalSetup(Target = nameof(BenchmarkImageSharp))]
    public void SetupImageSharp()
    {
        using FileStream imageBinaryStream = File.OpenRead(Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, TestImage));

        this.imageImageSharp = Image.Load<Rgba32>(imageBinaryStream);
        this.encoderImageSharp = new() { Quality = this.Quality, ColorType = JpegColorType.YCbCrRatio420 };

        this.destinationStream = new();
    }

    [GlobalCleanup(Target = nameof(BenchmarkImageSharp))]
    public void CleanupImageSharp()
    {
        this.imageImageSharp.Dispose();
        this.imageImageSharp = null;

        this.destinationStream.Dispose();
        this.destinationStream = null;
    }

    [Benchmark(Description = "ImageSharp")]
    public void BenchmarkImageSharp()
    {
        this.imageImageSharp.SaveAsJpeg(this.destinationStream, this.encoderImageSharp);
        this.destinationStream.Seek(0, SeekOrigin.Begin);
    }

    [GlobalSetup(Target = nameof(BenchmarkSkiaSharp))]
    public void SetupSkiaSharp()
    {
        using FileStream imageBinaryStream = File.OpenRead(Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, TestImage));

        this.imageSkiaSharp = SKBitmap.Decode(imageBinaryStream);

        this.destinationStream = new();
    }

    [GlobalCleanup(Target = nameof(BenchmarkSkiaSharp))]
    public void CleanupSkiaSharp()
    {
        this.imageSkiaSharp.Dispose();
        this.imageSkiaSharp = null;

        this.destinationStream.Dispose();
        this.destinationStream = null;
    }

    [Benchmark(Description = "SkiaSharp")]
    public void BenchmarkSkiaSharp()
    {
        this.imageSkiaSharp.Encode(SKEncodedImageFormat.Jpeg, this.Quality).SaveTo(this.destinationStream);
        this.destinationStream.Seek(0, SeekOrigin.Begin);
    }
}

/*
BenchmarkDotNet=v0.13.0, OS=Windows 10.0.19044
Intel Core i7-6700K CPU 4.00GHz (Skylake), 1 CPU, 8 logical and 4 physical cores
.NET SDK=6.0.100-preview.3.21202.5
[Host]     : .NET Core 3.1.21 (CoreCLR 4.700.21.51404, CoreFX 4.700.21.51508), X64 RyuJIT
DefaultJob : .NET Core 3.1.21 (CoreCLR 4.700.21.51404, CoreFX 4.700.21.51508), X64 RyuJIT


|     Method | Quality |      Mean |     Error |    StdDev |
|----------- |-------- |----------:|----------:|----------:|
| ImageSharp |      75 |  6.820 ms | 0.0374 ms | 0.0312 ms |
|  SkiaSharp |      75 | 16.417 ms | 0.3238 ms | 0.4747 ms |
| ImageSharp |      90 |  7.849 ms | 0.1565 ms | 0.3126 ms |
|  SkiaSharp |      90 | 16.893 ms | 0.2200 ms | 0.2058 ms |
| ImageSharp |     100 | 11.016 ms | 0.2087 ms | 0.1850 ms |
|  SkiaSharp |     100 | 20.410 ms | 0.2583 ms | 0.2290 ms |
*/
