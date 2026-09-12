// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Heif;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Color;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Heif;

/// <summary>
/// Compares complete AV1 elementary-stream decoding through native reconstruction and packed RGB output.
/// </summary>
[MemoryDiagnoser]
public class Av1DecoderBenchmarks
{
    private Configuration configuration;
    private byte[] payload;

    /// <summary>
    /// Gets or sets the existing photographic conformance input.
    /// </summary>
    [Params("libavif-kodim23-8b.bit", "libavif-cosmos1650-10b.bit")]
    public string FileName { get; set; }

    /// <summary>
    /// Loads the encoded bytes and verifies exact packed-pixel agreement before timing either decoder.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.configuration = Configuration.Default.Clone();
        this.configuration.MaxDegreeOfParallelism = 1;
        this.payload = File.ReadAllBytes(Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, "Heif", "Av1", "Conformance", this.FileName));
        using Av1Decoder managed = new(this.configuration);
        using Av1FrameBuffer<byte> expectedPlanes = managed.DecodeFrameBuffer(this.payload, null, null, out _);
        using ImageFrame<Rgb48> expected = new(this.configuration, expectedPlanes.Width, expectedPlanes.Height);
        Av1YuvConverter.ConvertToRgb(
            this.configuration,
            expectedPlanes,
            expected.Bounds,
            expected.PixelBuffer.GetRegion(expected.Bounds),
            expected.Size,
            default,
            null,
            null,
            default,
            default,
            false,
            HeifChromaUpsampling.Auto,
            expectedPlanes.ColorConfig.ColorRange);
        using LibaomBenchmarkDecoder native = LibaomBenchmarkDecoder.Open();
        using ImageFrame<Rgb48> actual = native.Decode(this.configuration, this.payload);
        if (expected.Size != actual.Size)
        {
            throw new InvalidOperationException("The decoders returned different presentation dimensions.");
        }

        // Validate real RGB samples, not a checksum or a self-roundtrip. This work is outside measurement;
        // both measured paths still allocate their own destination and perform every conversion again.
        for (int y = 0; y < expected.Height; y++)
        {
            if (!expected.PixelBuffer.DangerousGetRowSpan(y).SequenceEqual(actual.PixelBuffer.DangerousGetRowSpan(y)))
            {
                throw new InvalidOperationException($"The decoders disagree on packed RGB row {y}.");
            }
        }
    }

    /// <summary>
    /// Decodes the complete bitstream to independently owned RGB pixels and disposes all operation state.
    /// </summary>
    /// <returns>The decoded presentation size.</returns>
    [Benchmark]
    public Size ImageSharp()
    {
        using Av1Decoder decoder = new(this.configuration);
        using Av1FrameBuffer<byte> framePlanes = decoder.DecodeFrameBuffer(this.payload, null, null, out _);
        using ImageFrame<Rgb48> frame = new(this.configuration, framePlanes.Width, framePlanes.Height);
        Av1YuvConverter.ConvertToRgb(
            this.configuration,
            framePlanes,
            frame.Bounds,
            frame.PixelBuffer.GetRegion(frame.Bounds),
            frame.Size,
            default,
            null,
            null,
            default,
            default,
            false,
            HeifChromaUpsampling.Auto,
            framePlanes.ColorConfig.ColorRange);
        return frame.Size;
    }

    /// <summary>
    /// Decodes the identical bitstream with libaom and converts its output to the identical RGB format.
    /// </summary>
    /// <returns>The decoded presentation size.</returns>
    [Benchmark(Baseline = true)]
    public Size Libaom()
    {
        using LibaomBenchmarkDecoder decoder = LibaomBenchmarkDecoder.Open();
        using ImageFrame<Rgb48> frame = decoder.Decode(this.configuration, this.payload);
        return frame.Size;
    }
}
