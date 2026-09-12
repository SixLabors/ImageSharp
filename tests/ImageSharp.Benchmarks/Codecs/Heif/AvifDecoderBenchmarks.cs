// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Heif;

/// <summary>
/// Measures public ImageSharp AVIF parsing, reconstruction, color conversion, and image allocation.
/// </summary>
[MemoryDiagnoser]
public class AvifDecoderBenchmarks
{
    private byte[] payload;
    private DecoderOptions options;

    /// <summary>
    /// Gets or sets the independently encoded AVIF input.
    /// </summary>
    [Params(TestImages.Heif.Av1Deblocking8BitAvif, TestImages.Heif.Av1Deblocking10BitAvif)]
    public string FileName { get; set; }

    /// <summary>
    /// Loads the retained payload and validates the public decoder before measurement.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        Configuration configuration = Configuration.Default.Clone();
        configuration.MaxDegreeOfParallelism = 1;
        this.options = new DecoderOptions
        {
            Configuration = configuration,
            SkipMetadata = true
        };

        this.payload = File.ReadAllBytes(Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.FileName));
        using Image<Rgb48> image = Image.Load<Rgb48>(this.options, this.payload);
        if (image.Width <= 0 || image.Height <= 0)
        {
            throw new InvalidOperationException("The AVIF benchmark input produced an empty presentation.");
        }
    }

    /// <summary>
    /// Decodes the complete AVIF through the public image-load contract.
    /// </summary>
    /// <returns>The decoded presentation size.</returns>
    [Benchmark]
    public Size Decode()
    {
        using Image<Rgb48> image = Image.Load<Rgb48>(this.options, this.payload);
        return image.Size;
    }
}
