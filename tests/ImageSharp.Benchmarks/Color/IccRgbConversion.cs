// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks;

/// <summary>
/// Measures the conversion from Adobe RGB (1998) to sRGB that the decoders do by default.
/// The setup of the transform for each image and the conversion of the pixel rows have separate benchmarks.
/// </summary>
[Config(typeof(Config.Short))]
public class IccRgbConversion
{
    private ColorConversionOptions options;
    private ColorProfileConverter converter;
    private Vector4[] pixels;
    private Vector4[] row;
    private int width;

    /// <summary>
    /// Reads the Adobe RGB profile and the pixels of the image. Then it makes one converter before the measurement.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        string path = Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, TestImages.Jpeg.ICC.AdobeRgb);
        IccProfile profile = Image.Identify(path).Metadata.IccProfile;

        this.options = new ColorConversionOptions
        {
            SourceIccProfile = profile,
            TargetIccProfile = CompactSrgbV4Profile.Profile,
        };

        this.converter = new ColorProfileConverter(this.options);
        _ = this.converter.GetIccTransform();

        // The array holds one straight RGB vector with opaque alpha for each pixel.
        // The decoder converts these pixels to sRGB, so they are not the Adobe RGB samples of the file.
        // They are real image colors in the range [0, 1].
        using Image<RgbaVector> image = Image.Load<RgbaVector>(path);
        this.width = image.Width;
        this.pixels = new Vector4[image.Width * image.Height];
        image.CopyPixelDataTo(MemoryMarshal.Cast<Vector4, RgbaVector>(this.pixels.AsSpan()));
        this.row = new Vector4[image.Width];
    }

    /// <summary>
    /// Makes the transform from the profile to sRGB. The decoder makes one transform for each image that has a profile.
    /// </summary>
    /// <returns>The transform.</returns>
    [Benchmark]
    public object CreateTransform() => new ColorProfileConverter(this.options).GetIccTransform();

    /// <summary>
    /// Converts all rows of the image in place with the row API that the TIFF, HEIF and WebP decoders use.
    /// </summary>
    [Benchmark]
    public void ConvertImageRows()
    {
        for (int y = 0; y < this.pixels.Length; y += this.width)
        {
            this.pixels.AsSpan(y, this.width).CopyTo(this.row);
            this.converter.Convert(this.row);
        }
    }

    /* Results 10.11.2026, Jpg/icc-profiles/Momiji-AdobeRGB-yes.jpg
    BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9818)
    AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
    .NET SDK 11.0.100-preview.7.26381.103
      [Host]     : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
      Job-RCQXAA : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

    Arguments=/p:DebugType=portable  IterationCount=3  LaunchCount=1
    WarmupCount=3

    | Method           | Mean       | Error       | StdDev    | Gen0   | Allocated |
    |----------------- |-----------:|------------:|----------:|-------:|----------:|
    | CreateTransform  |   1.069 us |   0.1250 us | 0.0069 us | 0.2804 |    4720 B |
    | ConvertImageRows | 912.072 us | 170.9662 us | 9.3712 us |      - |         - |
    */
}
