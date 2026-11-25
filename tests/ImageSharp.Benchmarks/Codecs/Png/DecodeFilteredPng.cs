// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.Codecs;

[Config(typeof(Config.Short))]
public class DecodeFilteredPng
{
    private byte[] filter0;
    private byte[] filter1;
    private byte[] filter2;
    private byte[] filter3;
    private byte[] averageFilter3bpp;
    private byte[] averageFilter4bpp;

    [GlobalSetup]
    public void ReadImages()
    {
        this.filter0 = File.ReadAllBytes(TestImageFullPath(TestImages.Png.Filter0));
        this.filter1 = File.ReadAllBytes(TestImageFullPath(TestImages.Png.SubFilter3BytesPerPixel));
        this.filter2 = File.ReadAllBytes(TestImageFullPath(TestImages.Png.UpFilter));
        this.filter3 = File.ReadAllBytes(TestImageFullPath(TestImages.Png.PaethFilter3BytesPerPixel));
        this.averageFilter3bpp = File.ReadAllBytes(TestImageFullPath(TestImages.Png.AverageFilter3BytesPerPixel));
        this.averageFilter4bpp = File.ReadAllBytes(TestImageFullPath(TestImages.Png.AverageFilter4BytesPerPixel));
    }

    [Benchmark(Baseline = true, Description = "None-filtered PNG file")]
    public Size PngFilter0()
        => LoadPng(this.filter0);

    [Benchmark(Description = "Sub-filtered PNG file")]
    public Size PngFilter1()
        => LoadPng(this.filter1);

    [Benchmark(Description = "Up-filtered PNG file")]
    public Size PngFilter2()
        => LoadPng(this.filter2);

    [Benchmark(Description = "Average-filtered PNG file (3bpp)")]
    public Size PngAvgFilter1()
        => LoadPng(this.averageFilter3bpp);

    [Benchmark(Description = "Average-filtered PNG file (4bpp)")]
    public Size PngAvgFilter2()
        => LoadPng(this.averageFilter4bpp);

    [Benchmark(Description = "Paeth-filtered PNG file")]
    public Size PngFilter4()
        => LoadPng(this.filter3);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Size LoadPng(byte[] bytes)
    {
        using Image<Rgba32> image = Image.Load<Rgba32>(bytes);
        return image.Size;
    }

    private static string TestImageFullPath(string path)
        => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, path);

    /* Results 25.11.2026
    BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.10075)
    AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
    .NET SDK 11.0.100
      [Host]     : .NET 10.0.15 (10.0.15, 10.0.1526.53008), X64 RyuJIT x86-64-v4
      Job-RCQXAA : .NET 10.0.15 (10.0.15, 10.0.1526.53008), X64 RyuJIT x86-64-v4
    Arguments=/p:DebugType=portable  IterationCount=3  LaunchCount=1
    WarmupCount=3

    | Method                             | Mean        | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
    |----------------------------------- |------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
    | 'None-filtered PNG file'           |    14.73 us |   2.910 us |  0.160 us |  1.00 |    0.01 | 0.2136 |   3.85 KB |        1.00 |
    | 'Sub-filtered PNG file'            |    10.75 us |   3.948 us |  0.216 us |  0.73 |    0.01 | 0.2289 |   3.85 KB |        1.00 |
    | 'Up-filtered PNG file'             |    11.97 us |   2.952 us |  0.162 us |  0.81 |    0.01 | 0.2289 |   3.85 KB |        1.00 |
    | 'Average-filtered PNG file (3bpp)' |    11.59 us |  14.281 us |  0.783 us |  0.79 |    0.05 | 0.2289 |   3.85 KB |        1.00 |
    | 'Average-filtered PNG file (4bpp)' | 1,221.16 us | 345.161 us | 18.919 us | 82.92 |    1.36 |      - |  23.86 KB |        6.19 |
    | 'Paeth-filtered PNG file'          |    12.05 us |   2.665 us |  0.146 us |  0.82 |    0.01 | 0.2289 |   3.85 KB |        1.00 |

    Allocated reports managed memory only.
    */
}
