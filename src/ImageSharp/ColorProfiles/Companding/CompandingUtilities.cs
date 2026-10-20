// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.PixelFormats.Utils;

namespace SixLabors.ImageSharp.ColorProfiles.Companding;

/// <summary>
/// Companding utilities that allow the accelerated compression-expansion of color channels.
/// </summary>
/// <remarks>
/// Inputs use normalized color component units. SDR components use [0,1]; extended components use [-65504,65504].
/// The supplied transfer curve determines whether input components are linear or nonlinear.
/// Vector4 APIs transform RGB in X, Y, Z and retain alpha in W. Component APIs transform every SIMD lane.
/// </remarks>
public static class CompandingUtilities
{
    private const int Length = Scale + 2; // 256kb @ 16bit precision.
    private const int Scale = (1 << 16) - 1;
    private static readonly ConcurrentDictionary<(Type, double), float[]> CompressLookupTables = new();
    private static readonly ConcurrentDictionary<(Type, double), float[]> ExpandLookupTables = new();

    /// <summary>
    /// Lazily creates and stores a companding compression lookup table using the given function and modifier.
    /// </summary>
    /// <typeparam name="T">The type of companding function.</typeparam>
    /// <param name="compandingFunction">The companding function.</param>
    /// <param name="modifier">A modifier to pass to the function.</param>
    /// <returns>The <see cref="float"/> array.</returns>
    public static float[] GetCompressLookupTable<T>(Func<double, double, double> compandingFunction, double modifier = 0)
    {
        // The static factory receives the function only on a cache miss, avoiding a closure for each lookup hit.
        return CompressLookupTables.GetOrAdd((typeof(T), modifier), static (args, function) => CreateLookupTableImpl(function, args.Item2), compandingFunction);
    }

    /// <summary>
    /// Lazily creates and stores a companding expanding lookup table using the given function and modifier.
    /// </summary>
    /// <typeparam name="T">The type of companding function.</typeparam>
    /// <param name="compandingFunction">The companding function.</param>
    /// <param name="modifier">A modifier to pass to the function.</param>
    /// <returns>The <see cref="float"/> array.</returns>
    public static float[] GetExpandLookupTable<T>(Func<double, double, double> compandingFunction, double modifier = 0)
    {
        // The static factory receives the function only on a cache miss, avoiding a closure for each lookup hit.
        return ExpandLookupTables.GetOrAdd((typeof(T), modifier), static (args, function) => CreateLookupTableImpl(function, args.Item2), compandingFunction);
    }

    /// <summary>
    /// Creates a companding lookup table using the given function.
    /// </summary>
    /// <param name="compandingFunction">The companding function.</param>
    /// <param name="modifier">A modifier to pass to the function.</param>
    /// <returns>The <see cref="float"/> array.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float[] CreateLookupTableImpl(Func<double, double, double> compandingFunction, double modifier = 0)
    {
        float[] result = new float[Length];

        for (int i = 0; i < result.Length; i++)
        {
            double d = (double)i / Scale;
            d = compandingFunction(d, modifier);

            // These tables serve SDR colors. Bound each sample before interpolation so invalid samples
            // cannot create NaNs between table entries, including at an exact endpoint.
            result[i] = Compand((float)d, default(Vector4Converters.IdentityOperator), PixelColorRange.Sdr);
        }

        return result;
    }

    /// <summary>
    /// Transforms RGB in place and retains each color's alpha.
    /// </summary>
    /// <remarks>
    /// This traversal treats each vector as RGBA. The component operator has no knowledge of that layout.
    /// Alpha remains unchanged because transfer curves apply to RGB, not opacity.
    /// Results use the requested color range. NaN maps to its lower bound; infinities map to the corresponding bounds.
    /// </remarks>
    /// <typeparam name="TOperator">The companding operation.</typeparam>
    /// <param name="values">The normalized RGB colors to transform. Each vector stores RGB in X, Y, Z and alpha in W.</param>
    /// <param name="operation">The configured transfer curve.</param>
    /// <param name="colorRange">SDR bounds RGB to [0,1]. Extended RGB uses the shared finite color range.</param>
    internal static void Compand<TOperator>(Span<Vector4> values, TOperator operation, PixelColorRange colorRange)
        where TOperator : struct, TensorPrimitives_.IUnaryOperator
    {
        Span<float> components = MemoryMarshal.Cast<Vector4, float>(values);
        ref float first = ref MemoryMarshal.GetReference(components);
        int i = 0;

        // The curve can overflow even when its input is bounded. Limit its result before any caller consumes it.
        // Bounds are selected once per span; alpha is restored after clamping the independent component lanes.
        BoundedOperator<TOperator> bounded = new(operation, colorRange);

        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = (components.Length - i).Vector512Count<float>();
            Vector512<float> alphaMask = Vector512.Create(0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1).AsSingle();

            // Each register holds four RGBA colors. Restore lanes 3, 7, 11 and 15 from the input to retain alpha.
            for (nuint block = 0; block < count; block++, i += Vector512<float>.Count)
            {
                Vector512<float> input = Vector512.LoadUnsafe(ref first, (uint)i);
                Vector512<float> result = bounded.Invoke(input);
                Vector512.ConditionalSelect(alphaMask, input, result).StoreUnsafe(ref first, (uint)i);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (components.Length - i).Vector256Count<float>();
            Vector256<float> alphaMask = Vector256.Create(0, 0, 0, -1, 0, 0, 0, -1).AsSingle();

            // Each register holds two RGBA colors. Restore lanes 3 and 7 without a separate pass over alpha.
            for (nuint block = 0; block < count; block++, i += Vector256<float>.Count)
            {
                Vector256<float> input = Vector256.LoadUnsafe(ref first, (uint)i);
                Vector256<float> result = bounded.Invoke(input);
                Vector256.ConditionalSelect(alphaMask, input, result).StoreUnsafe(ref first, (uint)i);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (components.Length - i).Vector128Count<float>();
            Vector128<float> alphaMask = Vector128.Create(0, 0, 0, -1).AsSingle();

            // Each register holds one complete color. The fourth lane always remains alpha.
            for (nuint block = 0; block < count; block++, i += Vector128<float>.Count)
            {
                Vector128<float> input = Vector128.LoadUnsafe(ref first, (uint)i);
                Vector128<float> result = bounded.Invoke(input);
                Vector128.ConditionalSelect(alphaMask, input, result).StoreUnsafe(ref first, (uint)i);
            }
        }

        // Every SIMD width ends on a color boundary. The scalar fallback transforms RGB and leaves alpha in place.
        for (; i < components.Length; i += 4)
        {
            Unsafe.Add(ref first, (uint)i) = bounded.Invoke(Unsafe.Add(ref first, (uint)i));
            Unsafe.Add(ref first, (uint)i + 1) = bounded.Invoke(Unsafe.Add(ref first, (uint)i + 1));
            Unsafe.Add(ref first, (uint)i + 2) = bounded.Invoke(Unsafe.Add(ref first, (uint)i + 2));
        }
    }

    /// <summary>
    /// Transforms RGB in one color and retains its alpha.
    /// </summary>
    /// <typeparam name="TOperator">The component operation.</typeparam>
    /// <param name="value">The normalized RGB color. X, Y, Z contain RGB; W contains alpha.</param>
    /// <param name="operation">The configured operation.</param>
    /// <param name="colorRange">SDR bounds RGB to [0,1]. Extended RGB uses the shared finite color range.</param>
    /// <returns>The transformed color with its original alpha.</returns>
    internal static Vector4 Compand<TOperator>(Vector4 value, TOperator operation, PixelColorRange colorRange)
        where TOperator : struct, TensorPrimitives_.IUnaryOperator
    {
        BoundedOperator<TOperator> bounded = new(operation, colorRange);

        if (Vector128.IsHardwareAccelerated)
        {
            Vector4 result = bounded.Invoke(value.AsVector128()).AsVector4();

            // Restore alpha after the component operation. Transfer curves apply to RGB, not opacity.
            result.W = value.W;
            return result;
        }

        return new Vector4(
            bounded.Invoke(value.X),
            bounded.Invoke(value.Y),
            bounded.Invoke(value.Z),
            value.W);
    }

    /// <summary>
    /// Transforms one independent component and applies the requested result range.
    /// </summary>
    /// <typeparam name="TOperator">The component operation.</typeparam>
    /// <param name="value">The normalized input component in the transfer curve's domain.</param>
    /// <param name="operation">The configured transfer curve.</param>
    /// <param name="colorRange">The range applied to the converted component.</param>
    /// <returns>The finite component within the requested range.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float Compand<TOperator>(float value, TOperator operation, PixelColorRange colorRange)
        where TOperator : struct, TensorPrimitives_.IUnaryOperator
        => new BoundedOperator<TOperator>(operation, colorRange).Invoke(value);

    /// <summary>
    /// Transforms independent 128-bit component lanes through the bounded companding operation.
    /// </summary>
    /// <typeparam name="TOperator">The component operation.</typeparam>
    /// <param name="value">The normalized input components in the transfer curve's domain. Every lane is color data.</param>
    /// <param name="operation">The configured transfer curve.</param>
    /// <param name="colorRange">The range applied to every converted component.</param>
    /// <returns>The finite components within the requested range.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector128<float> Compand<TOperator>(Vector128<float> value, TOperator operation, PixelColorRange colorRange)
        where TOperator : struct, TensorPrimitives_.IUnaryOperator
        => new BoundedOperator<TOperator>(operation, colorRange).Invoke(value);

    /// <summary>
    /// Transforms independent 256-bit component lanes through the bounded companding operation.
    /// </summary>
    /// <typeparam name="TOperator">The component operation.</typeparam>
    /// <param name="value">The normalized input components in the transfer curve's domain. Every lane is color data.</param>
    /// <param name="operation">The configured transfer curve.</param>
    /// <param name="colorRange">The range applied to every converted component.</param>
    /// <returns>The finite components within the requested range.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector256<float> Compand<TOperator>(Vector256<float> value, TOperator operation, PixelColorRange colorRange)
        where TOperator : struct, TensorPrimitives_.IUnaryOperator
        => new BoundedOperator<TOperator>(operation, colorRange).Invoke(value);

    /// <summary>
    /// Transforms independent 512-bit component lanes through the bounded companding operation.
    /// </summary>
    /// <typeparam name="TOperator">The component operation.</typeparam>
    /// <param name="value">The normalized input components in the transfer curve's domain. Every lane is color data.</param>
    /// <param name="operation">The configured transfer curve.</param>
    /// <param name="colorRange">The range applied to every converted component.</param>
    /// <returns>The finite components within the requested range.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector512<float> Compand<TOperator>(Vector512<float> value, TOperator operation, PixelColorRange colorRange)
        where TOperator : struct, TensorPrimitives_.IUnaryOperator
        => new BoundedOperator<TOperator>(operation, colorRange).Invoke(value);

    /// <summary>
    /// Performs the companding operation on the given vectors using the given table.
    /// </summary>
    /// <param name="vectors">The normalized SDR colors with RGB in [0,1]. Alpha remains unchanged.</param>
    /// <param name="table">The lookup table.</param>
    public static void Compand(Span<Vector4> vectors, float[] table)
    {
        DebugGuard.MustBeGreaterThanOrEqualTo(table.Length, Length, nameof(table));

        // Exclude the padding sample from the input coordinate range. Shared traversal retains alpha at every SIMD width.
        Compand(vectors, new LutOperator(table, false, Scale + 1), PixelColorRange.Sdr);
    }

    /// <summary>
    /// Performs the companding operation on the given vector using the given table.
    /// </summary>
    /// <param name="vector">The normalized SDR color with RGB in [0,1]. Alpha remains unchanged.</param>
    /// <param name="table">The lookup table.</param>
    /// <returns>The <see cref="Vector4"/></returns>
    public static Vector4 Compand(Vector4 vector, float[] table)
    {
        DebugGuard.MustBeGreaterThanOrEqualTo(table.Length, Length, nameof(table));

        // Match the span's logical table range and retain alpha through the same companding operation.
        return Compand(vector, new LutOperator(table, false, Scale + 1), PixelColorRange.Sdr);
    }

    /// <summary>
    /// Evaluates a transfer curve and enforces the result contract for every companding entry point.
    /// </summary>
    /// <typeparam name="TOperator">The curve arithmetic, independent of color range and pixel layout.</typeparam>
    private readonly struct BoundedOperator<TOperator> : TensorPrimitives_.IUnaryOperator
        where TOperator : struct, TensorPrimitives_.IUnaryOperator
    {
        private readonly TOperator operation;
        private readonly float minimum;
        private readonly float maximum;

        /// <summary>
        /// Initializes a new instance of the <see cref="BoundedOperator{TOperator}"/> struct.
        /// </summary>
        /// <param name="operation">The transfer curve.</param>
        /// <param name="colorRange">The range applied after evaluating the curve.</param>
        public BoundedOperator(TOperator operation, PixelColorRange colorRange)
        {
            this.operation = operation;

            // Normalized color components use [0,1] for SDR and [-65504,65504] for extended RGB.
            // Select these limits once per operation. ClampToFinite maps NaN and negative infinity to the lower limit,
            // and positive infinity to the upper limit.
            this.minimum = colorRange == PixelColorRange.Sdr ? 0F : ColorNumerics.MinColorValue;
            this.maximum = colorRange == PixelColorRange.Sdr ? 1F : ColorNumerics.MaxColorValue;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Invoke(float value) => ColorNumerics.ClampToFinite(this.operation.Invoke(value), this.minimum, this.maximum);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(Vector128<float> value)
            => ColorNumerics.ClampToFinite(this.operation.Invoke(value), Vector128.Create(this.minimum), Vector128.Create(this.maximum));

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(Vector256<float> value)
            => ColorNumerics.ClampToFinite(this.operation.Invoke(value), Vector256.Create(this.minimum), Vector256.Create(this.maximum));

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(Vector512<float> value)
            => ColorNumerics.ClampToFinite(this.operation.Invoke(value), Vector512.Create(this.minimum), Vector512.Create(this.maximum));
    }
}
