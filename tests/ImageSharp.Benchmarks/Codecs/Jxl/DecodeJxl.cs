// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;
using NativeJxl = JxlNet.Jxl;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jxl;

/// <summary>
/// Compares complete, single-threaded JPEG XL decoding to unassociated sRGB float pixels.
/// </summary>
[MarkdownExporter]
[Config(typeof(Config.Short))]
public class DecodeJxl
{
    // Reference adapters return pixels, without EXIF/XMP. Skip those ancillary profiles while retaining color data needed for decoding.
    private readonly DecoderOptions options = new(Configuration.Default) { SkipMetadata = true };
    private byte[] bytes;

    /// <summary>
    /// Gets or sets the stored input that covers lossless, progressive, grouped or alpha reconstruction.
    /// </summary>
    [Params(TestImages.Jxl.EmptyPalette, TestImages.Jxl.PermutedSections, TestImages.Jxl.ProgressiveAc, TestImages.Jxl.Dice)]
    public string TestImage { get; set; }

    /// <summary>
    /// Loads the encoded bytes and checks both native adapters against ImageSharp before measurement.
    /// </summary>
    [GlobalSetup]
    public void ReadImages()
    {
        this.bytes = File.ReadAllBytes(Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImage));
        using Image<RgbaVector> image = Image.Load<RgbaVector>(this.options, this.bytes);
        using JxlReferenceImage libjxl = JxlReferenceImage.DecodeLibjxl(this.bytes);
        using JxlReferenceImage rust = JxlReferenceImage.DecodeRust(this.bytes);
        ValidatePixels(image, libjxl);
        ValidatePixels(image, rust);

        uint version = NativeJxl.JxlDecoderVersion();
        Console.WriteLine($"libjxl {version / 1000000}.{(version / 1000) % 1000}.{version % 1000}; JxlNet 0.12.0.2.");
        Console.WriteLine("jxl-rs d7a831fae15b903f1b56a76668030c19ac07bb6f; high_precision=true; sequential runner.");
        Console.WriteLine($"{this.TestImage}: {image.Width} x {image.Height}, float RGBA sRGB, unassociated alpha, full pixel validation passed.");
    }

    /// <summary>
    /// Decodes and disposes one image with ImageSharp, including its final pixel packing.
    /// </summary>
    /// <returns>The decoded image height.</returns>
    [Benchmark]
    public int ImageSharp()
    {
        using Image<RgbaVector> image = Image.Load<RgbaVector>(this.options, this.bytes);
        return image.Height;
    }

    /// <summary>
    /// Decodes one image with libjxl and releases its decoder and float output allocation.
    /// </summary>
    /// <returns>The decoded image height.</returns>
    [Benchmark(Baseline = true)]
    public int Libjxl()
    {
        using JxlReferenceImage image = JxlReferenceImage.DecodeLibjxl(this.bytes);
        return image.Height;
    }

    /// <summary>
    /// Decodes one image with the original jxl-rs API and releases its decoder and float output allocation.
    /// </summary>
    /// <returns>The decoded image height.</returns>
    [Benchmark]
    public int JxlRs()
    {
        using JxlReferenceImage image = JxlReferenceImage.DecodeRust(this.bytes);
        return image.Height;
    }

    /// <summary>
    /// Checks every output component outside the timed operation using the established shared companding accuracy bound.
    /// </summary>
    /// <param name="image">The complete ImageSharp float output.</param>
    /// <param name="reference">The independent decoder's float output after the same pixel bounds.</param>
    private static void ValidatePixels(Image<RgbaVector> image, JxlReferenceImage reference)
    {
        if (image.Width != reference.Width || image.Height != reference.Height)
        {
            throw new InvalidOperationException("The JPEG XL benchmark decoders returned different dimensions.");
        }

        // The shared analytic power's existing bound is 5e-4 in normalized RGB units, scaled for extended samples.
        // Alpha bypasses that power. Its 5e-7 bound covers the independently measured reference upsampling differences.
        ReadOnlySpan<Vector4> expected = reference.Pixels;
        for (int y = 0; y < image.Height; y++)
        {
            ReadOnlySpan<RgbaVector> actual = image.DangerousGetPixelRowMemory(y).Span;
            for (int x = 0; x < image.Width; x++)
            {
                Vector4 value = actual[x].ToVector4();
                Vector4 native = expected[(y * image.Width) + x];
                for (int component = 0; component < 4; component++)
                {
                    float bound = component == 3 ? 0.0000005F : 0.0005F * MathF.Max(1F, MathF.Abs(native[component]));
                    if (!float.IsFinite(value[component]) || !float.IsFinite(native[component]) || MathF.Abs(value[component] - native[component]) > bound)
                    {
                        throw new InvalidOperationException(
                            $"JPEG XL benchmark output differs at ({x},{y}), component {component}: "
                            + $"ImageSharp {value[component]:R}, reference {native[component]:R}.");
                    }
                }
            }
        }
    }

    /* Results 02.11.2026
    BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9818)
    AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
    .NET SDK 11.0.100-preview.7.26381.103
      [Host]     : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
      Job-RCQXAA : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

    Arguments=/p:DebugType=portable  IterationCount=3  LaunchCount=1
    WarmupCount=3

    | Method     | TestImage                 |      Mean |     Error |   StdDev | Ratio | RatioSD |      Gen0 |      Gen1 |      Gen2 | Allocated | Alloc Ratio |
    | ---------- | ------------------------- | --------: | --------: | -------: | ----: | ------: | --------: | --------: | --------: | --------: | ----------: |
    | ImageSharp | Jxl/dice.jxl              |  43.81 ms | 11.661 ms | 0.639 ms |  1.41 |    0.02 |  272.7273 |  272.7273 |  272.7273 |  429774 B |    5,969.08 |
    | Libjxl     | Jxl/dice.jxl              |  31.12 ms |  8.000 ms | 0.439 ms |  1.00 |    0.02 |         - |         - |         - |      72 B |        1.00 |
    | JxlRs      | Jxl/dice.jxl              |  19.23 ms |  3.088 ms | 0.169 ms |  0.62 |    0.01 |         - |         - |         - |      72 B |        1.00 |
    |            |                           |           |           |          |       |         |           |           |           |           |             |
    | ImageSharp | Jxl/has_permutation.jxl   |  49.89 ms | 17.354 ms | 0.951 ms |  0.93 |    0.03 |  300.0000 |  300.0000 |  300.0000 |  260175 B |    3,613.54 |
    | Libjxl     | Jxl/has_permutation.jxl   |  53.88 ms | 23.952 ms | 1.313 ms |  1.00 |    0.03 |         - |         - |         - |      72 B |        1.00 |
    | JxlRs      | Jxl/has_permutation.jxl   |  26.43 ms |  6.763 ms | 0.371 ms |  0.49 |    0.01 |         - |         - |         - |      72 B |        1.00 |
    |            |                           |           |           |          |       |         |           |           |           |           |             |
    | ImageSharp | Jxl/issue648_palette0.jxl | 205.23 ms | 86.420 ms | 4.737 ms |  1.50 |    0.03 | 1000.0000 | 1000.0000 | 1000.0000 | 1430051 B |   19,861.82 |
    | Libjxl     | Jxl/issue648_palette0.jxl | 137.09 ms |  1.609 ms | 0.088 ms |  1.00 |    0.00 |         - |         - |         - |      72 B |        1.00 |
    | JxlRs      | Jxl/issue648_palette0.jxl | 124.34 ms | 18.609 ms | 1.020 ms |  0.91 |    0.01 |         - |         - |         - |      72 B |        1.00 |
    |            |                           |           |           |          |       |         |           |           |           |           |             |
    | ImageSharp | Jxl/progressive_ac.jxl    | 120.72 ms | 15.069 ms | 0.826 ms |  1.16 |    0.01 |  400.0000 |  400.0000 |  400.0000 |  492013 B |    6,150.16 |
    | Libjxl     | Jxl/progressive_ac.jxl    | 103.97 ms | 15.285 ms | 0.838 ms |  1.00 |    0.01 |         - |         - |         - |      80 B |        1.00 |
    | JxlRs      | Jxl/progressive_ac.jxl    |  61.08 ms | 31.312 ms | 1.716 ms |  0.59 |    0.01 |         - |         - |         - |      72 B |        0.90 |

    libjxl 0.12.0 through JxlNet 0.12.0.2; jxl-rs d7a831fae15b903f1b56a76668030c19ac07bb6f with high_precision=true.
    Each operation includes decoder/output allocation, complete decode, shared final pixel bounds and disposal.
    Output is unassociated sRGB float RGBA in codestream order. Ancillary EXIF/XMP is skipped by every decoder.
    Full-image output validation runs before measurement. The existing Config.Short and .NET JIT/thread settings are unchanged.

    Allocated reports new managed allocations only. It excludes native allocations and reused pooled buffers, and does not measure total working memory.
    Every output contains 16 bytes per pixel. Error is half the 99.9% confidence interval; StdDev describes the three measured iterations.
    */
}
