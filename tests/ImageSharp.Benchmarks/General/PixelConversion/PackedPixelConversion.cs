// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats.Utils;

namespace SixLabors.ImageSharp.Benchmarks.General.PixelConversion;

/// <summary>
/// Measures every optimized conversion between the six byte-packed RGB and RGBA pixel layouts.
/// </summary>
[Config(typeof(Config.Short))]
public class PackedPixelConversion
{
    private byte[] source3;
    private byte[] source4;
    private byte[] destination3;
    private byte[] destination4;

    /// <summary>
    /// Gets or sets the number of pixels converted by each invocation.
    /// </summary>
    [Params(7, 256, 4096)]
    public int Count { get; set; }

    /// <summary>
    /// Creates deterministic source buffers and correctly sized destination buffers.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.source3 = new byte[this.Count * 3];
        this.source4 = new byte[this.Count * 4];
        this.destination3 = new byte[this.Count * 3];
        this.destination4 = new byte[this.Count * 4];

        // Non-repeating channel values prevent the JIT or hardware from benefiting from
        // zero-filled inputs while keeping both benchmark revisions byte-for-byte identical.
        new Random(42).NextBytes(this.source3);
        new Random(42).NextBytes(this.source4);
    }

    /// <summary>
    /// Converts RGBA pixels to ARGB pixels.
    /// </summary>
    [Benchmark]
    public void Rgba32ToArgb32() => PixelConverter.FromRgba32.ToArgb32(this.source4, this.destination4);

    /// <summary>
    /// Converts RGBA pixels to ABGR pixels.
    /// </summary>
    [Benchmark]
    public void Rgba32ToAbgr32() => PixelConverter.FromRgba32.ToAbgr32(this.source4, this.destination4);

    /// <summary>
    /// Converts RGBA pixels to BGRA pixels.
    /// </summary>
    [Benchmark]
    public void Rgba32ToBgra32() => PixelConverter.FromRgba32.ToBgra32(this.source4, this.destination4);

    /// <summary>
    /// Converts RGBA pixels to RGB pixels.
    /// </summary>
    [Benchmark]
    public void Rgba32ToRgb24() => PixelConverter.FromRgba32.ToRgb24(this.source4, this.destination3);

    /// <summary>
    /// Converts RGBA pixels to BGR pixels.
    /// </summary>
    [Benchmark]
    public void Rgba32ToBgr24() => PixelConverter.FromRgba32.ToBgr24(this.source4, this.destination3);

    /// <summary>
    /// Converts ARGB pixels to RGBA pixels.
    /// </summary>
    [Benchmark]
    public void Argb32ToRgba32() => PixelConverter.FromArgb32.ToRgba32(this.source4, this.destination4);

    /// <summary>
    /// Converts ARGB pixels to ABGR pixels.
    /// </summary>
    [Benchmark]
    public void Argb32ToAbgr32() => PixelConverter.FromArgb32.ToAbgr32(this.source4, this.destination4);

    /// <summary>
    /// Converts ARGB pixels to BGRA pixels.
    /// </summary>
    [Benchmark]
    public void Argb32ToBgra32() => PixelConverter.FromArgb32.ToBgra32(this.source4, this.destination4);

    /// <summary>
    /// Converts ARGB pixels to RGB pixels.
    /// </summary>
    [Benchmark]
    public void Argb32ToRgb24() => PixelConverter.FromArgb32.ToRgb24(this.source4, this.destination3);

    /// <summary>
    /// Converts ARGB pixels to BGR pixels.
    /// </summary>
    [Benchmark]
    public void Argb32ToBgr24() => PixelConverter.FromArgb32.ToBgr24(this.source4, this.destination3);

    /// <summary>
    /// Converts ABGR pixels to RGBA pixels.
    /// </summary>
    [Benchmark]
    public void Abgr32ToRgba32() => PixelConverter.FromAbgr32.ToRgba32(this.source4, this.destination4);

    /// <summary>
    /// Converts ABGR pixels to ARGB pixels.
    /// </summary>
    [Benchmark]
    public void Abgr32ToArgb32() => PixelConverter.FromAbgr32.ToArgb32(this.source4, this.destination4);

    /// <summary>
    /// Converts ABGR pixels to BGRA pixels.
    /// </summary>
    [Benchmark]
    public void Abgr32ToBgra32() => PixelConverter.FromAbgr32.ToBgra32(this.source4, this.destination4);

    /// <summary>
    /// Converts ABGR pixels to RGB pixels.
    /// </summary>
    [Benchmark]
    public void Abgr32ToRgb24() => PixelConverter.FromAbgr32.ToRgb24(this.source4, this.destination3);

    /// <summary>
    /// Converts ABGR pixels to BGR pixels.
    /// </summary>
    [Benchmark]
    public void Abgr32ToBgr24() => PixelConverter.FromAbgr32.ToBgr24(this.source4, this.destination3);

    /// <summary>
    /// Converts BGRA pixels to RGBA pixels.
    /// </summary>
    [Benchmark]
    public void Bgra32ToRgba32() => PixelConverter.FromBgra32.ToRgba32(this.source4, this.destination4);

    /// <summary>
    /// Converts BGRA pixels to ARGB pixels.
    /// </summary>
    [Benchmark]
    public void Bgra32ToArgb32() => PixelConverter.FromBgra32.ToArgb32(this.source4, this.destination4);

    /// <summary>
    /// Converts BGRA pixels to ABGR pixels.
    /// </summary>
    [Benchmark]
    public void Bgra32ToAbgr32() => PixelConverter.FromBgra32.ToAbgr32(this.source4, this.destination4);

    /// <summary>
    /// Converts BGRA pixels to RGB pixels.
    /// </summary>
    [Benchmark]
    public void Bgra32ToRgb24() => PixelConverter.FromBgra32.ToRgb24(this.source4, this.destination3);

    /// <summary>
    /// Converts BGRA pixels to BGR pixels.
    /// </summary>
    [Benchmark]
    public void Bgra32ToBgr24() => PixelConverter.FromBgra32.ToBgr24(this.source4, this.destination3);

    /// <summary>
    /// Converts RGB pixels to RGBA pixels.
    /// </summary>
    [Benchmark]
    public void Rgb24ToRgba32() => PixelConverter.FromRgb24.ToRgba32(this.source3, this.destination4);

    /// <summary>
    /// Converts RGB pixels to ARGB pixels.
    /// </summary>
    [Benchmark]
    public void Rgb24ToArgb32() => PixelConverter.FromRgb24.ToArgb32(this.source3, this.destination4);

    /// <summary>
    /// Converts RGB pixels to ABGR pixels.
    /// </summary>
    [Benchmark]
    public void Rgb24ToAbgr32() => PixelConverter.FromRgb24.ToAbgr32(this.source3, this.destination4);

    /// <summary>
    /// Converts RGB pixels to BGRA pixels.
    /// </summary>
    [Benchmark]
    public void Rgb24ToBgra32() => PixelConverter.FromRgb24.ToBgra32(this.source3, this.destination4);

    /// <summary>
    /// Converts RGB pixels to BGR pixels.
    /// </summary>
    [Benchmark]
    public void Rgb24ToBgr24() => PixelConverter.FromRgb24.ToBgr24(this.source3, this.destination3);

    /// <summary>
    /// Converts BGR pixels to RGBA pixels.
    /// </summary>
    [Benchmark]
    public void Bgr24ToRgba32() => PixelConverter.FromBgr24.ToRgba32(this.source3, this.destination4);

    /// <summary>
    /// Converts BGR pixels to ARGB pixels.
    /// </summary>
    [Benchmark]
    public void Bgr24ToArgb32() => PixelConverter.FromBgr24.ToArgb32(this.source3, this.destination4);

    /// <summary>
    /// Converts BGR pixels to ABGR pixels.
    /// </summary>
    [Benchmark]
    public void Bgr24ToAbgr32() => PixelConverter.FromBgr24.ToAbgr32(this.source3, this.destination4);

    /// <summary>
    /// Converts BGR pixels to BGRA pixels.
    /// </summary>
    [Benchmark]
    public void Bgr24ToBgra32() => PixelConverter.FromBgr24.ToBgra32(this.source3, this.destination4);

    /// <summary>
    /// Converts BGR pixels to RGB pixels.
    /// </summary>
    [Benchmark]
    public void Bgr24ToRgb24() => PixelConverter.FromBgr24.ToRgb24(this.source3, this.destination3);
}
