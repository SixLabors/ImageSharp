// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using SixLabors.ImageSharp.Formats.Jpeg.Components;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg;

/// <summary>
/// Compares the previous scalar JPEG packing loops with the SIMD register-transpose implementation.
/// </summary>
[Config(typeof(Config.Short))]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class JpegColorPacking
{
    private const float MaximumValue = 255F;
    private const float Scale = 1F / MaximumValue;

    private float[] x = null!;
    private float[] y = null!;
    private float[] z = null!;
    private float[] w = null!;
    private Vector3[] packed3 = null!;
    private float[] destination3 = null!;
    private float[] destination4 = null!;

    /// <summary>
    /// Gets or sets the number of pixels transformed by each benchmark invocation.
    /// </summary>
    [Params(128, 1024, 4096)]
    public int Length { get; set; }

    /// <summary>
    /// Creates deterministic source and destination buffers outside the measured operations.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.x = CreateSamples(this.Length, 1);
        this.y = CreateSamples(this.Length, 2);
        this.z = CreateSamples(this.Length, 3);
        this.w = CreateSamples(this.Length, 4);
        this.packed3 = new Vector3[this.Length];
        this.destination3 = new float[this.Length * 3];
        this.destination4 = new float[this.Length * 4];

        for (int i = 0; i < this.packed3.Length; i++)
        {
            this.packed3[i] = new Vector3(this.x[i], this.y[i], this.z[i]);
        }
    }

    /// <summary>
    /// Measures the previous scalar three-plane normalization and interleave loop.
    /// </summary>
    /// <returns>The last destination value, keeping the writes observable.</returns>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Pack3")]
    public float PackedNormalizeInterleave3Scalar()
    {
        JpegColorPackingScalar.PackedNormalizeInterleave3(this.x, this.y, this.z, this.destination3, Scale);

        return this.destination3[^1];
    }

    /// <summary>
    /// Measures the SIMD three-plane normalization and interleave implementation.
    /// </summary>
    /// <returns>The last destination value, keeping the writes observable.</returns>
    [Benchmark]
    [BenchmarkCategory("Pack3")]
    public float PackedNormalizeInterleave3Simd()
    {
        JpegColorConverterBase.PackedNormalizeInterleave3(this.x, this.y, this.z, this.destination3, Scale);

        return this.destination3[^1];
    }

    /// <summary>
    /// Measures the previous scalar packed-three-channel deinterleave loop.
    /// </summary>
    /// <returns>A checksum containing the last value written to every destination plane.</returns>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Unpack3")]
    public float UnpackDeinterleave3Scalar()
    {
        JpegColorPackingScalar.UnpackDeinterleave3(this.packed3, this.x, this.y, this.z);

        return this.x[^1] + this.y[^1] + this.z[^1];
    }

    /// <summary>
    /// Measures the SIMD packed-three-channel deinterleave implementation.
    /// </summary>
    /// <returns>A checksum containing the last value written to every destination plane.</returns>
    [Benchmark]
    [BenchmarkCategory("Unpack3")]
    public float UnpackDeinterleave3Simd()
    {
        JpegColorConverterBase.UnpackDeinterleave3(this.packed3, this.x, this.y, this.z);

        return this.x[^1] + this.y[^1] + this.z[^1];
    }

    /// <summary>
    /// Measures the previous scalar four-plane normalization and interleave loop.
    /// </summary>
    /// <returns>The last destination value, keeping the writes observable.</returns>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Pack4")]
    public float PackedNormalizeInterleave4Scalar()
    {
        JpegColorPackingScalar.PackedNormalizeInterleave4(this.x, this.y, this.z, this.w, this.destination4, MaximumValue);

        return this.destination4[^1];
    }

    /// <summary>
    /// Measures the SIMD four-plane normalization and interleave implementation.
    /// </summary>
    /// <returns>The last destination value, keeping the writes observable.</returns>
    [Benchmark]
    [BenchmarkCategory("Pack4")]
    public float PackedNormalizeInterleave4Simd()
    {
        JpegColorConverterBase.PackedNormalizeInterleave4(this.x, this.y, this.z, this.w, this.destination4, MaximumValue);

        return this.destination4[^1];
    }

    /// <summary>
    /// Measures the previous scalar inverted four-plane normalization and interleave loop.
    /// </summary>
    /// <returns>The last destination value, keeping the writes observable.</returns>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("InvertPack4")]
    public float PackedInvertNormalizeInterleave4Scalar()
    {
        JpegColorPackingScalar.PackedInvertNormalizeInterleave4(this.x, this.y, this.z, this.w, this.destination4, MaximumValue);

        return this.destination4[^1];
    }

    /// <summary>
    /// Measures the SIMD inverted four-plane normalization and interleave implementation.
    /// </summary>
    /// <returns>The last destination value, keeping the writes observable.</returns>
    [Benchmark]
    [BenchmarkCategory("InvertPack4")]
    public float PackedInvertNormalizeInterleave4Simd()
    {
        JpegColorConverterBase.PackedInvertNormalizeInterleave4(this.x, this.y, this.z, this.w, this.destination4, MaximumValue);

        return this.destination4[^1];
    }

    /// <summary>
    /// Creates deterministic, non-integral samples for one component plane.
    /// </summary>
    /// <param name="length">The number of samples to create.</param>
    /// <param name="component">The one-based component number used to distinguish the plane.</param>
    /// <returns>The generated samples.</returns>
    private static float[] CreateSamples(int length, int component)
    {
        float[] samples = new float[length];

        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = (((i * 37) + (component * 53)) % 251) + (component * 0.125F);
        }

        return samples;
    }
}
