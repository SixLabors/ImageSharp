// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using SixLabors.ImageSharp.Formats.Jpeg.Components;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg;

/// <summary>
/// Compares the shared YCbCr operator traversal with the Vector512 implementation it replaces.
/// </summary>
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class YCbCrOperatorComparison
{
    private JpegColorConverterBase.YCbCrVector512 legacy;
    private JpegColorConverterBase.JpegColorConverter<JpegColorConverterBase.YCbCrOperator> operatorConverter;
    private float[] legacyC0;
    private float[] legacyC1;
    private float[] legacyC2;
    private float[] operatorC0;
    private float[] operatorC1;
    private float[] operatorC2;
    private float[] r;
    private float[] g;
    private float[] b;

    /// <summary>
    /// Gets or sets the number of pixels converted by each invocation.
    /// </summary>
    [Params(8, 128, 1024)]
    public int Count { get; set; }

    /// <summary>
    /// Creates equivalent converter inputs in independent component buffers.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.legacy = new JpegColorConverterBase.YCbCrVector512(8);
        this.operatorConverter =
            new JpegColorConverterBase.JpegColorConverter<JpegColorConverterBase.YCbCrOperator>(8);

        Random random = new(42);
        this.legacyC0 = CreateRandomValues(this.Count, random);
        this.legacyC1 = CreateRandomValues(this.Count, random);
        this.legacyC2 = CreateRandomValues(this.Count, random);
        this.operatorC0 = this.legacyC0.ToArray();
        this.operatorC1 = this.legacyC1.ToArray();
        this.operatorC2 = this.legacyC2.ToArray();
        this.r = CreateRandomValues(this.Count, random);
        this.g = CreateRandomValues(this.Count, random);
        this.b = CreateRandomValues(this.Count, random);
    }

    /// <summary>
    /// Converts YCbCr components to RGB using the Vector512 implementation.
    /// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("ToRgb")]
    public void LegacyToRgb()
    {
        JpegColorConverterBase.ComponentValues values =
            new(3, this.legacyC0, this.legacyC1, this.legacyC2, []);

        this.legacy.ConvertToRgbInPlace(values);
    }

    /// <summary>
    /// Converts YCbCr components to RGB using the shared operator traversal.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("ToRgb")]
    public void OperatorToRgb()
    {
        JpegColorConverterBase.ComponentValues values =
            new(3, this.operatorC0, this.operatorC1, this.operatorC2, []);

        this.operatorConverter.ConvertToRgbInPlace(values);
    }

    /// <summary>
    /// Converts RGB to YCbCr components using the Vector512 implementation.
    /// </summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("FromRgb")]
    public void LegacyFromRgb()
    {
        JpegColorConverterBase.ComponentValues values =
            new(3, this.legacyC0, this.legacyC1, this.legacyC2, []);

        this.legacy.ConvertFromRgb(values, this.r, this.g, this.b);
    }

    /// <summary>
    /// Converts RGB to YCbCr components using the shared operator traversal.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("FromRgb")]
    public void OperatorFromRgb()
    {
        JpegColorConverterBase.ComponentValues values =
            new(3, this.operatorC0, this.operatorC1, this.operatorC2, []);

        this.operatorConverter.ConvertFromRgb(values, this.r, this.g, this.b);
    }

    /// <summary>
    /// Creates deterministic sample-domain values for one component plane.
    /// </summary>
    /// <param name="length">The number of samples to create.</param>
    /// <param name="random">The deterministic random source shared by setup.</param>
    /// <returns>The populated component plane.</returns>
    private static float[] CreateRandomValues(int length, Random random)
    {
        float[] values = new float[length];

        for (int i = 0; i < values.Length; i++)
        {
            values[i] = (float)random.NextDouble() * 255F;
        }

        return values;
    }
}
