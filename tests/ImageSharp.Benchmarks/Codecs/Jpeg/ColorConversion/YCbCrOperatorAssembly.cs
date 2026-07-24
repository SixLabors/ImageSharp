// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using SixLabors.ImageSharp.Formats.Jpeg.Components;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg;

/// <summary>
/// Exposes every YCbCr operator overload directly to the disassembly diagnoser.
/// </summary>
[Config(typeof(Config.Analysis))]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class YCbCrOperatorAssembly
{
    private const float MaximumValue = 255F;
    private const float HalfValue = 128F;
    private const float Scale = 1F / MaximumValue;

    private float scalarC0 = 64F;
    private float scalarC1 = 96F;
    private float scalarC2 = 160F;

    private readonly Vector128<float> vector128C0 = Vector128.Create(64F);
    private readonly Vector128<float> vector128C1 = Vector128.Create(96F);
    private readonly Vector128<float> vector128C2 = Vector128.Create(160F);
    private readonly Vector128<float> vector128Maximum = Vector128.Create(MaximumValue);
    private readonly Vector128<float> vector128Half = Vector128.Create(HalfValue);
    private readonly Vector128<float> vector128Scale = Vector128.Create(Scale);

    private readonly Vector256<float> vector256C0 = Vector256.Create(64F);
    private readonly Vector256<float> vector256C1 = Vector256.Create(96F);
    private readonly Vector256<float> vector256C2 = Vector256.Create(160F);
    private readonly Vector256<float> vector256Maximum = Vector256.Create(MaximumValue);
    private readonly Vector256<float> vector256Half = Vector256.Create(HalfValue);
    private readonly Vector256<float> vector256Scale = Vector256.Create(Scale);

    private readonly Vector512<float> vector512C0 = Vector512.Create(64F);
    private readonly Vector512<float> vector512C1 = Vector512.Create(96F);
    private readonly Vector512<float> vector512C2 = Vector512.Create(160F);
    private readonly Vector512<float> vector512Maximum = Vector512.Create(MaximumValue);
    private readonly Vector512<float> vector512Half = Vector512.Create(HalfValue);
    private readonly Vector512<float> vector512Scale = Vector512.Create(Scale);

    /// <summary>
    /// Invokes the scalar JPEG-to-RGB operator.
    /// </summary>
    /// <returns>A checksum containing all three converted channels.</returns>
    [Benchmark]
    [BenchmarkCategory("ToRgb")]
    public float ToRgbScalar()
    {
        float c0 = this.scalarC0;
        float c1 = this.scalarC1;
        float c2 = this.scalarC2;

        JpegColorConverterBase.YCbCrOperator.ConvertToRgb(
            ref c0,
            ref c1,
            ref c2,
            0,
            MaximumValue,
            HalfValue,
            Scale);

        // Returning the channel sum keeps every output live in the generated assembly.
        return c0 + c1 + c2;
    }

    /// <summary>
    /// Invokes the Vector128 JPEG-to-RGB operator.
    /// </summary>
    /// <returns>A checksum containing all three converted channel vectors.</returns>
    [Benchmark]
    [BenchmarkCategory("ToRgb")]
    public Vector128<float> ToRgbVector128()
    {
        Vector128<float> c0 = this.vector128C0;
        Vector128<float> c1 = this.vector128C1;
        Vector128<float> c2 = this.vector128C2;

        JpegColorConverterBase.YCbCrOperator.ConvertToRgb(
            ref c0,
            ref c1,
            ref c2,
            default,
            this.vector128Maximum,
            this.vector128Half,
            this.vector128Scale);

        // The vector sum makes all RGB results observable without adding stores to the measured body.
        return c0 + c1 + c2;
    }

    /// <summary>
    /// Invokes the Vector256 JPEG-to-RGB operator.
    /// </summary>
    /// <returns>A checksum containing all three converted channel vectors.</returns>
    [Benchmark]
    [BenchmarkCategory("ToRgb")]
    public Vector256<float> ToRgbVector256()
    {
        Vector256<float> c0 = this.vector256C0;
        Vector256<float> c1 = this.vector256C1;
        Vector256<float> c2 = this.vector256C2;

        JpegColorConverterBase.YCbCrOperator.ConvertToRgb(
            ref c0,
            ref c1,
            ref c2,
            default,
            this.vector256Maximum,
            this.vector256Half,
            this.vector256Scale);

        // The vector sum makes all RGB results observable without adding stores to the measured body.
        return c0 + c1 + c2;
    }

    /// <summary>
    /// Invokes the Vector512 JPEG-to-RGB operator.
    /// </summary>
    /// <returns>A checksum containing all three converted channel vectors.</returns>
    [Benchmark]
    [BenchmarkCategory("ToRgb")]
    public Vector512<float> ToRgbVector512()
    {
        Vector512<float> c0 = this.vector512C0;
        Vector512<float> c1 = this.vector512C1;
        Vector512<float> c2 = this.vector512C2;

        JpegColorConverterBase.YCbCrOperator.ConvertToRgb(
            ref c0,
            ref c1,
            ref c2,
            default,
            this.vector512Maximum,
            this.vector512Half,
            this.vector512Scale);

        // The vector sum makes all RGB results observable without adding stores to the measured body.
        return c0 + c1 + c2;
    }

    /// <summary>
    /// Invokes the scalar RGB-to-JPEG operator.
    /// </summary>
    /// <returns>A checksum containing all converted components.</returns>
    [Benchmark]
    [BenchmarkCategory("FromRgb")]
    public float FromRgbScalar()
    {
        JpegColorConverterBase.YCbCrOperator.ConvertFromRgb(
            this.scalarC0,
            this.scalarC1,
            this.scalarC2,
            MaximumValue,
            HalfValue,
            Scale,
            out float c0,
            out float c1,
            out float c2,
            out float c3);

        // c3 is deliberately included so a future four-component implementation remains observable.
        return c0 + c1 + c2 + c3;
    }

    /// <summary>
    /// Invokes the Vector128 RGB-to-JPEG operator.
    /// </summary>
    /// <returns>A checksum containing all converted component vectors.</returns>
    [Benchmark]
    [BenchmarkCategory("FromRgb")]
    public Vector128<float> FromRgbVector128()
    {
        JpegColorConverterBase.YCbCrOperator.ConvertFromRgb(
            this.vector128C0,
            this.vector128C1,
            this.vector128C2,
            this.vector128Maximum,
            this.vector128Half,
            this.vector128Scale,
            out Vector128<float> c0,
            out Vector128<float> c1,
            out Vector128<float> c2,
            out Vector128<float> c3);

        // Include all planar results in the returned vector so the JIT retains every calculation.
        return c0 + c1 + c2 + c3;
    }

    /// <summary>
    /// Invokes the Vector256 RGB-to-JPEG operator.
    /// </summary>
    /// <returns>A checksum containing all converted component vectors.</returns>
    [Benchmark]
    [BenchmarkCategory("FromRgb")]
    public Vector256<float> FromRgbVector256()
    {
        JpegColorConverterBase.YCbCrOperator.ConvertFromRgb(
            this.vector256C0,
            this.vector256C1,
            this.vector256C2,
            this.vector256Maximum,
            this.vector256Half,
            this.vector256Scale,
            out Vector256<float> c0,
            out Vector256<float> c1,
            out Vector256<float> c2,
            out Vector256<float> c3);

        // Include all planar results in the returned vector so the JIT retains every calculation.
        return c0 + c1 + c2 + c3;
    }

    /// <summary>
    /// Invokes the Vector512 RGB-to-JPEG operator.
    /// </summary>
    /// <returns>A checksum containing all converted component vectors.</returns>
    [Benchmark]
    [BenchmarkCategory("FromRgb")]
    public Vector512<float> FromRgbVector512()
    {
        JpegColorConverterBase.YCbCrOperator.ConvertFromRgb(
            this.vector512C0,
            this.vector512C1,
            this.vector512C2,
            this.vector512Maximum,
            this.vector512Half,
            this.vector512Scale,
            out Vector512<float> c0,
            out Vector512<float> c1,
            out Vector512<float> c2,
            out Vector512<float> c3);

        // Include all planar results in the returned vector so the JIT retains every calculation.
        return c0 + c1 + c2 + c3;
    }
}
