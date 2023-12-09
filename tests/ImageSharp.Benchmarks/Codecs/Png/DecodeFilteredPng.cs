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
}
