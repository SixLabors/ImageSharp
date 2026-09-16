// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Heif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Heif;

/// <summary>
/// Measures public ImageSharp AVIF encoding from packed pixels through complete container output.
/// </summary>
[MemoryDiagnoser]
public class AvifEncoderBenchmarks
{
    private Image<Rgba32> image;
    private MemoryStream output;
    private HeifEncoder encoder;

    /// <summary>
    /// Gets or sets the square source dimension.
    /// </summary>
    [Params(256, 512)]
    public int Dimension { get; set; }

    /// <summary>
    /// Gets or sets the native-valued encoder speed.
    /// </summary>
    [Params(HeifEncodingSpeed.Level0, HeifEncodingSpeed.Level6, HeifEncodingSpeed.Level9)]
    public HeifEncodingSpeed Speed { get; set; }

    /// <summary>
    /// Loads the source image and validates one complete public encode and decode before measurement.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        string sourcePath = Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, TestImages.Png.Bike);
        this.image = Image.Load<Rgba32>(sourcePath);
        this.image.Mutate(context => context.Resize(this.Dimension, this.Dimension));
        this.output = new MemoryStream();
        this.encoder = new HeifEncoder
        {
            Quality = 75,
            Speed = this.Speed,
            BitDepth = HeifBitDepth.Bit8,
            ChromaSubsampling = HeifChromaSubsampling.Yuv420
        };

        this.Encode();
        if (!this.output.TryGetBuffer(out ArraySegment<byte> segment))
        {
            throw new InvalidOperationException("The AVIF benchmark output does not expose its retained buffer.");
        }

        using Image<Rgba32> decoded = Image.Load<Rgba32>(segment.AsSpan(0, checked((int)this.output.Length)));
        if (decoded.Size != this.image.Size || decoded.Frames.Count != this.image.Frames.Count)
        {
            throw new InvalidOperationException("The encoded AVIF does not preserve the source presentation geometry.");
        }
    }

    /// <summary>
    /// Releases the retained source and output storage.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        this.output?.Dispose();
        this.image?.Dispose();
    }

    /// <summary>
    /// Encodes one complete AVIF through the public image-save contract.
    /// </summary>
    /// <returns>The complete AVIF length.</returns>
    [Benchmark]
    public long Encode()
    {
        this.output.Position = 0;
        this.output.SetLength(0);
        this.image.Save(this.output, this.encoder);
        return this.output.Length;
    }
}
