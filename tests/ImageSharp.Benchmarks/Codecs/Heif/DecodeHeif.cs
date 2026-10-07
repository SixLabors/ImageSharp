// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using ImageMagick;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.Codecs;

/// <summary>
/// Compares AVIF decoding in ImageSharp and Magick.NET.
/// </summary>
[MarkdownExporter]
[HtmlExporter]
[Config(typeof(Config.Short))]
public class DecodeHeif
{
    private byte[] avifBytes;

    /// <summary>
    /// Gets or sets the AVIF test image.
    /// </summary>
    [Params(TestImages.Heif.IrvineAvif, TestImages.Heif.Av1Deblocking8BitAvif)]
    public string TestImage { get; set; }

    private string TestImageFullPath => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImage);

    /// <summary>
    /// Reads the encoded test image.
    /// </summary>
    [GlobalSetup]
    public void ReadImages() => this.avifBytes ??= File.ReadAllBytes(this.TestImageFullPath);

    /// <summary>
    /// Decodes the image with Magick.NET.
    /// </summary>
    /// <returns>The image width.</returns>
    [Benchmark(Baseline = true, Description = "Magick Avif")]
    public uint AvifMagick()
    {
        MagickReadSettings settings = new() { Format = MagickFormat.Avif };
        using MemoryStream memoryStream = new(this.avifBytes);
        using MagickImage image = new(memoryStream, settings);
        return image.Width;
    }

    /// <summary>
    /// Decodes the image with ImageSharp.
    /// </summary>
    /// <returns>The image height.</returns>
    [Benchmark(Description = "ImageSharp Avif")]
    public int Avif()
    {
        using MemoryStream memoryStream = new(this.avifBytes);
        using Image<Rgba32> image = Image.Load<Rgba32>(memoryStream);
        return image.Height;
    }
}
