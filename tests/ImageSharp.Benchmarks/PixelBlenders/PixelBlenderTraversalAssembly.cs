// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.PixelFormats.PixelBlenders;

namespace SixLabors.ImageSharp.Benchmarks.PixelBlenders;

/// <summary>
/// Exposes every shared pixel-blender traversal shape for assembly inspection.
/// </summary>
/// <remarks>
/// Seven pixels leave three scalar pixels after AVX-512 or one scalar pixel after AVX2, so each
/// hardware job contains both its widest supported loop and the portable Vector4 remainder.
/// </remarks>
[Config(typeof(Config.Analysis))]
public class PixelBlenderTraversalAssembly
{
    private const int Count = 7;
    private const float Amount = .625F;

    private readonly ExposedNormalSrcOverBlender blender = new();
    private readonly Vector4[] destination = new Vector4[Count];
    private readonly Vector4[] background = new Vector4[Count];
    private readonly Vector4[] source = new Vector4[Count];
    private readonly float[] amounts = new float[Count];
    private readonly float[] coverage = new float[Count];
    private Vector4 constantSource;

    /// <summary>
    /// Populates all lanes with deterministic, non-constant values.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        Random random = new(42);

        for (int i = 0; i < Count; i++)
        {
            // Distinct RGBA lanes make incorrect pixel grouping visible in both results and assembly.
            this.background[i] = CreatePixel(random);
            this.source[i] = CreatePixel(random);
            this.amounts[i] = random.NextSingle();
            this.coverage[i] = random.NextSingle();
        }

        this.constantSource = CreatePixel(random);
    }

    /// <summary>
    /// Blends a source row with one shared amount.
    /// </summary>
    /// <returns>The final destination pixel.</returns>
    [Benchmark]
    public Vector4 SourceSpanScalarAmount()
    {
        this.blender.BlendSourceSpanScalarAmount(
            this.destination,
            this.background,
            this.source,
            Amount);

        return this.destination[^1];
    }

    /// <summary>
    /// Blends a constant source with one shared amount.
    /// </summary>
    /// <returns>The final destination pixel.</returns>
    [Benchmark]
    public Vector4 ConstantSourceScalarAmount()
    {
        this.blender.BlendConstantSourceScalarAmount(
            this.destination,
            this.background,
            this.constantSource,
            Amount);

        return this.destination[^1];
    }

    /// <summary>
    /// Blends a source row with per-pixel amounts.
    /// </summary>
    /// <returns>The final destination pixel.</returns>
    [Benchmark]
    public Vector4 SourceSpanAmountSpan()
    {
        this.blender.BlendSourceSpanAmountSpan(
            this.destination,
            this.background,
            this.source,
            this.amounts);

        return this.destination[^1];
    }

    /// <summary>
    /// Blends a constant source with per-pixel amounts.
    /// </summary>
    /// <returns>The final destination pixel.</returns>
    [Benchmark]
    public Vector4 ConstantSourceAmountSpan()
    {
        this.blender.BlendConstantSourceAmountSpan(
            this.destination,
            this.background,
            this.constantSource,
            this.amounts);

        return this.destination[^1];
    }

    /// <summary>
    /// Blends a source row with one shared amount and per-pixel coverage.
    /// </summary>
    /// <returns>The final destination pixel.</returns>
    [Benchmark]
    public Vector4 SourceSpanScalarAmountCoverage()
    {
        this.blender.BlendSourceSpanScalarAmountCoverage(
            this.destination,
            this.background,
            this.source,
            Amount,
            this.coverage);

        return this.destination[^1];
    }

    /// <summary>
    /// Blends a constant source with one shared amount and per-pixel coverage.
    /// </summary>
    /// <returns>The final destination pixel.</returns>
    [Benchmark]
    public Vector4 ConstantSourceScalarAmountCoverage()
    {
        this.blender.BlendConstantSourceScalarAmountCoverage(
            this.destination,
            this.background,
            this.constantSource,
            Amount,
            this.coverage);

        return this.destination[^1];
    }

    /// <summary>
    /// Blends a source row with per-pixel amounts and coverage.
    /// </summary>
    /// <returns>The final destination pixel.</returns>
    [Benchmark]
    public Vector4 SourceSpanAmountSpanCoverage()
    {
        this.blender.BlendSourceSpanAmountSpanCoverage(
            this.destination,
            this.background,
            this.source,
            this.amounts,
            this.coverage);

        return this.destination[^1];
    }

    /// <summary>
    /// Blends a constant source with per-pixel amounts and coverage.
    /// </summary>
    /// <returns>The final destination pixel.</returns>
    [Benchmark]
    public Vector4 ConstantSourceAmountSpanCoverage()
    {
        this.blender.BlendConstantSourceAmountSpanCoverage(
            this.destination,
            this.background,
            this.constantSource,
            this.amounts,
            this.coverage);

        return this.destination[^1];
    }

    /// <summary>
    /// Creates one non-constant RGBA sample.
    /// </summary>
    /// <param name="random">The deterministic value source.</param>
    /// <returns>The sample pixel.</returns>
    private static Vector4 CreatePixel(Random random)
        => new(random.NextSingle(), random.NextSingle(), random.NextSingle(), random.NextSingle());

    /// <summary>
    /// Exposes the protected shared traversal overloads without adding benchmark hooks to production APIs.
    /// </summary>
    private sealed class ExposedNormalSrcOverBlender :
        DefaultPixelBlender<RgbaVector, DefaultPixelBlenderOperators.NormalSrcOver>
    {
        /// <summary>
        /// Invokes the source-span, scalar-amount traversal.
        /// </summary>
        /// <param name="destination">The destination vectors.</param>
        /// <param name="background">The background vectors.</param>
        /// <param name="source">The source vectors.</param>
        /// <param name="amount">The shared source amount.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BlendSourceSpanScalarAmount(
            Span<Vector4> destination,
            ReadOnlySpan<Vector4> background,
            ReadOnlySpan<Vector4> source,
            float amount)
            => this.BlendFunction(destination, background, source, amount);

        /// <summary>
        /// Invokes the constant-source, scalar-amount traversal.
        /// </summary>
        /// <param name="destination">The destination vectors.</param>
        /// <param name="background">The background vectors.</param>
        /// <param name="source">The constant source vector.</param>
        /// <param name="amount">The shared source amount.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BlendConstantSourceScalarAmount(
            Span<Vector4> destination,
            ReadOnlySpan<Vector4> background,
            Vector4 source,
            float amount)
            => this.BlendFunction(destination, background, source, amount);

        /// <summary>
        /// Invokes the source-span, amount-span traversal.
        /// </summary>
        /// <param name="destination">The destination vectors.</param>
        /// <param name="background">The background vectors.</param>
        /// <param name="source">The source vectors.</param>
        /// <param name="amount">The per-pixel source amounts.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BlendSourceSpanAmountSpan(
            Span<Vector4> destination,
            ReadOnlySpan<Vector4> background,
            ReadOnlySpan<Vector4> source,
            ReadOnlySpan<float> amount)
            => this.BlendFunction(destination, background, source, amount);

        /// <summary>
        /// Invokes the constant-source, amount-span traversal.
        /// </summary>
        /// <param name="destination">The destination vectors.</param>
        /// <param name="background">The background vectors.</param>
        /// <param name="source">The constant source vector.</param>
        /// <param name="amount">The per-pixel source amounts.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BlendConstantSourceAmountSpan(
            Span<Vector4> destination,
            ReadOnlySpan<Vector4> background,
            Vector4 source,
            ReadOnlySpan<float> amount)
            => this.BlendFunction(destination, background, source, amount);

        /// <summary>
        /// Invokes the source-span, scalar-amount traversal with coverage.
        /// </summary>
        /// <param name="destination">The destination vectors.</param>
        /// <param name="background">The background vectors.</param>
        /// <param name="source">The source vectors.</param>
        /// <param name="amount">The shared source amount.</param>
        /// <param name="coverage">The per-pixel coverage values.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BlendSourceSpanScalarAmountCoverage(
            Span<Vector4> destination,
            ReadOnlySpan<Vector4> background,
            ReadOnlySpan<Vector4> source,
            float amount,
            ReadOnlySpan<float> coverage)
            => this.BlendWithCoverageFunction(destination, background, source, amount, coverage);

        /// <summary>
        /// Invokes the constant-source, scalar-amount traversal with coverage.
        /// </summary>
        /// <param name="destination">The destination vectors.</param>
        /// <param name="background">The background vectors.</param>
        /// <param name="source">The constant source vector.</param>
        /// <param name="amount">The shared source amount.</param>
        /// <param name="coverage">The per-pixel coverage values.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BlendConstantSourceScalarAmountCoverage(
            Span<Vector4> destination,
            ReadOnlySpan<Vector4> background,
            Vector4 source,
            float amount,
            ReadOnlySpan<float> coverage)
            => this.BlendWithCoverageFunction(destination, background, source, amount, coverage);

        /// <summary>
        /// Invokes the source-span, amount-span traversal with coverage.
        /// </summary>
        /// <param name="destination">The destination vectors.</param>
        /// <param name="background">The background vectors.</param>
        /// <param name="source">The source vectors.</param>
        /// <param name="amount">The per-pixel source amounts.</param>
        /// <param name="coverage">The per-pixel coverage values.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BlendSourceSpanAmountSpanCoverage(
            Span<Vector4> destination,
            ReadOnlySpan<Vector4> background,
            ReadOnlySpan<Vector4> source,
            ReadOnlySpan<float> amount,
            ReadOnlySpan<float> coverage)
            => this.BlendWithCoverageFunction(destination, background, source, amount, coverage);

        /// <summary>
        /// Invokes the constant-source, amount-span traversal with coverage.
        /// </summary>
        /// <param name="destination">The destination vectors.</param>
        /// <param name="background">The background vectors.</param>
        /// <param name="source">The constant source vector.</param>
        /// <param name="amount">The per-pixel source amounts.</param>
        /// <param name="coverage">The per-pixel coverage values.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BlendConstantSourceAmountSpanCoverage(
            Span<Vector4> destination,
            ReadOnlySpan<Vector4> background,
            Vector4 source,
            ReadOnlySpan<float> amount,
            ReadOnlySpan<float> coverage)
            => this.BlendWithCoverageFunction(destination, background, source, amount, coverage);
    }
}
