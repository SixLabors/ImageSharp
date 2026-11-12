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
        Console.WriteLine($"libjxl {version / 1000000}.{(version / 1000) % 1000}.{version % 1000}; JxlNet 0.12.0.5.");
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

    /* Results 05.11.2026
    BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9818)
    AMD RYZEN AI MAX+ 395 w/ Radeon 8060S 3.00GHz, 1 CPU, 32 logical and 16 physical cores
    .NET SDK 11.0.100-preview.7.26381.103
      [Host]     : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
      Job-RCQXAA : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

    Arguments=/p:DebugType=portable  IterationCount=3  LaunchCount=1
    WarmupCount=3

    Without NativeMemoryProfiler (21:06:39):

    | Method     | TestImage                 |      Mean |    Error |   StdDev | Ratio | Allocated | Alloc Ratio |
    | ---------- | ------------------------- | --------: | -------: | -------: | ----: | --------: | ----------: |
    | ImageSharp | Jxl/dice.jxl              |  19.28 ms | 2.964 ms | 0.162 ms |  0.64 |   37624 B |      522.56 |
    | Libjxl     | Jxl/dice.jxl              |  29.92 ms | 4.678 ms | 0.256 ms |  1.00 |      72 B |        1.00 |
    | JxlRs      | Jxl/dice.jxl              |  19.35 ms | 3.873 ms | 0.212 ms |  0.65 |      72 B |        1.00 |
    |            |                           |           |          |          |       |           |             |
    | ImageSharp | Jxl/has_permutation.jxl   |  17.74 ms | 2.636 ms | 0.145 ms |  0.34 |   27440 B |      356.36 |
    | Libjxl     | Jxl/has_permutation.jxl   |  51.67 ms | 5.092 ms | 0.279 ms |  1.00 |      77 B |        1.00 |
    | JxlRs      | Jxl/has_permutation.jxl   |  25.65 ms | 3.830 ms | 0.210 ms |  0.50 |      72 B |        0.94 |
    |            |                           |           |          |          |       |           |             |
    | ImageSharp | Jxl/issue648_palette0.jxl | 114.89 ms | 2.645 ms | 0.145 ms |  0.88 |   30040 B |      417.22 |
    | Libjxl     | Jxl/issue648_palette0.jxl | 130.66 ms | 7.784 ms | 0.427 ms |  1.00 |      72 B |        1.00 |
    | JxlRs      | Jxl/issue648_palette0.jxl | 120.32 ms | 5.550 ms | 0.304 ms |  0.92 |      72 B |        1.00 |
    |            |                           |           |          |          |       |           |             |
    | ImageSharp | Jxl/progressive_ac.jxl    |  49.93 ms | 4.589 ms | 0.252 ms |  0.49 |   26688 B |      370.67 |
    | Libjxl     | Jxl/progressive_ac.jxl    | 100.92 ms | 8.644 ms | 0.474 ms |  1.00 |      72 B |        1.00 |
    | JxlRs      | Jxl/progressive_ac.jxl    |  59.63 ms | 6.806 ms | 0.373 ms |  0.59 |      72 B |        1.00 |

    With NativeMemoryProfiler (21:11:13):

    | Method     | TestImage                 |      Mean |     Error |   StdDev | Ratio | Allocated native memory | Native memory leak | Allocated | Alloc Ratio |
    | ---------- | ------------------------- | --------: | --------: | -------: | ----: | ----------------------: | -----------------: | --------: | ----------: |
    | ImageSharp | Jxl/dice.jxl              |  20.44 ms |  7.559 ms | 0.414 ms |  0.65 |             5,243,136 B |        5,243,136 B |   37624 B |      522.56 |
    | Libjxl     | Jxl/dice.jxl              |  31.29 ms |  3.379 ms | 0.185 ms |  1.00 |            53,902,572 B |                  - |      72 B |        1.00 |
    | JxlRs      | Jxl/dice.jxl              |  19.50 ms |  9.298 ms | 0.510 ms |  0.62 |            16,017,583 B |                  - |      72 B |        1.00 |
    |            |                           |           |           |          |       |                         |                    |           |             |
    | ImageSharp | Jxl/has_permutation.jxl   |  18.73 ms | 12.245 ms | 0.671 ms |  0.36 |                   356 B |              242 B |   27440 B |      356.36 |
    | Libjxl     | Jxl/has_permutation.jxl   |  51.83 ms |  6.651 ms | 0.365 ms |  1.00 |            49,736,696 B |               33 B |      77 B |        1.00 |
    | JxlRs      | Jxl/has_permutation.jxl   |  27.67 ms |  0.992 ms | 0.054 ms |  0.53 |            39,813,244 B |                  - |      72 B |        0.94 |
    |            |                           |           |           |          |       |                         |                    |           |             |
    | ImageSharp | Jxl/issue648_palette0.jxl | 119.53 ms | 11.650 ms | 0.639 ms |  0.89 |             1,281,548 B |            1,420 B |   30040 B |      417.22 |
    | Libjxl     | Jxl/issue648_palette0.jxl | 134.28 ms | 20.583 ms | 1.128 ms |  1.00 |           119,047,387 B |                  - |      72 B |        1.00 |
    | JxlRs      | Jxl/issue648_palette0.jxl | 124.08 ms | 15.763 ms | 0.864 ms |  0.92 |            50,752,370 B |                  - |      72 B |        1.00 |
    |            |                           |           |           |          |       |                         |                    |           |             |
    | ImageSharp | Jxl/progressive_ac.jxl    |  52.61 ms | 12.969 ms | 0.711 ms |  0.51 |               796,806 B |               28 B |   26688 B |      333.60 |
    | Libjxl     | Jxl/progressive_ac.jxl    | 102.91 ms | 15.121 ms | 0.829 ms |  1.00 |           106,975,397 B |                  - |      80 B |        1.00 |
    | JxlRs      | Jxl/progressive_ac.jxl    |  60.25 ms |  8.617 ms | 0.472 ms |  0.59 |            61,091,654 B |                  - |      72 B |        0.90 |

    libjxl 0.12.0 through JxlNet 0.12.0.5; jxl-rs d7a831fae15b903f1b56a76668030c19ac07bb6f with high_precision=true.
    Each operation includes decoder/output allocation, complete decode, shared final pixel bounds and disposal.
    Output is unassociated sRGB float RGBA in codestream order. Ancillary EXIF/XMP is skipped by every decoder.
    Full-image output validation runs before measurement. The existing Config.Short and .NET JIT/thread settings are unchanged.

    Allocated reports new managed allocations. Allocated native memory reports new native allocations.
    Reused pooled buffers are not new allocations. These columns do not measure peak working memory.
    ImageSharp's native leak values are entirely retained pooled storage.
    Every output contains 16 bytes per pixel. Error is half the 99.9% confidence interval; StdDev describes the three measured iterations.
    */
}
