// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Benchmarks.General.BasicMath;

#pragma warning disable SA1649 // File name should match first type name
public class TensorPrimitivesJpegMultiplyAssemblyComparison
#pragma warning restore SA1649 // File name should match first type name
{
    private readonly float multiplier = -1F;
    private float[] legacyValues = null!;
    private float[] tensorValues = null!;

    /// <summary>
    /// Creates equivalent stable inputs for both implementations.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.legacyValues = new float[256];
        this.tensorValues = new float[256];

        for (int i = 0; i < this.legacyValues.Length; i++)
        {
            float value = ((i * 17) % 251) + 1;
            this.legacyValues[i] = value;
            this.tensorValues[i] = value;
        }
    }

    /// <summary>
    /// Multiplies the row with the retired JPEG AVX pipeline.
    /// </summary>
    /// <returns>The first result, which keeps the writes observable to the benchmark harness.</returns>
    [Benchmark(Baseline = true)]
    public float Legacy()
    {
        LegacyMultiply(this.legacyValues, this.multiplier);
        return this.legacyValues[0];
    }

    /// <summary>
    /// Multiplies the row with the tensor compatibility pipeline.
    /// </summary>
    /// <returns>The first result, which keeps the writes observable to the benchmark harness.</returns>
    [Benchmark]
    public float Tensor()
    {
        TensorPrimitives_.Multiply(this.tensorValues, this.multiplier, this.tensorValues);
        return this.tensorValues[0];
    }

    /// <summary>
    /// Reproduces the retired JPEG multiplication loop for assembly comparison.
    /// </summary>
    /// <param name="target">The row to multiply.</param>
    /// <param name="multiplier">The scalar multiplier.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void LegacyMultiply(Span<float> target, float multiplier)
    {
        ref Vector256<float> targetVector = ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(target));
        nuint count = (uint)target.Length / (uint)Vector256<float>.Count;
        Vector256<float> multiplierVector = Vector256.Create(multiplier);

        for (nuint i = 0; i < count; i++)
        {
            Unsafe.Add(ref targetVector, i) = Avx.Multiply(Unsafe.Add(ref targetVector, i), multiplierVector);
        }
    }
}

public class TensorPrimitivesNormalizeAssemblyComparison
{
    private readonly float divisor = -1F;
    private float[] legacyValues = null!;
    private float[] tensorValues = null!;

    /// <summary>
    /// Creates equivalent stable inputs for both implementations.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.legacyValues = new float[7];
        this.tensorValues = new float[7];

        for (int i = 0; i < this.legacyValues.Length; i++)
        {
            float value = ((i * 17) % 251) + 1;
            this.legacyValues[i] = value;
            this.tensorValues[i] = value;
        }
    }

    /// <summary>
    /// Normalizes the values with the retired fixed-width pipeline.
    /// </summary>
    /// <returns>The first result, which keeps the writes observable to the benchmark harness.</returns>
    [Benchmark(Baseline = true)]
    public float Legacy()
    {
        LegacyNormalize(this.legacyValues, this.divisor);
        return this.legacyValues[0];
    }

    /// <summary>
    /// Normalizes the values with the tensor compatibility pipeline.
    /// </summary>
    /// <returns>The first result, which keeps the writes observable to the benchmark harness.</returns>
    [Benchmark]
    public float Tensor()
    {
        Numerics.Normalize(this.tensorValues, this.divisor);
        return this.tensorValues[0];
    }

    /// <summary>
    /// Reproduces the retired normalization loop for assembly comparison.
    /// </summary>
    /// <param name="span">The values to normalize.</param>
    /// <param name="sum">The scalar divisor.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void LegacyNormalize(Span<float> span, float sum)
    {
        ref float start = ref MemoryMarshal.GetReference(span);
        ref float vectorEnd = ref Unsafe.Add(ref start, span.Length & ~7);
        Vector256<float> sum256 = Vector256.Create(sum);

        while (Unsafe.IsAddressLessThan(ref start, ref vectorEnd))
        {
            Unsafe.As<float, Vector256<float>>(ref start) /= sum256;
            start = ref Unsafe.Add(ref start, (nuint)8);
        }

        if ((span.Length & 7) >= 4)
        {
            Unsafe.As<float, Vector128<float>>(ref start) /= sum256.GetLower();
            start = ref Unsafe.Add(ref start, (nuint)4);
        }

        ref float end = ref Unsafe.Add(ref start, span.Length & 3);

        while (Unsafe.IsAddressLessThan(ref start, ref end))
        {
            start /= sum;
            start = ref Unsafe.Add(ref start, (nuint)1);
        }
    }
}

public class TensorPrimitivesUInt32AssemblyComparison
{
    private uint[] x = null!;
    private uint[] y = null!;
    private uint[] legacyDestination = null!;
    private uint[] tensorDestination = null!;

    /// <summary>
    /// Creates deterministic histogram inputs and independent destinations.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.x = new uint[2048];
        this.y = new uint[2048];
        this.legacyDestination = new uint[2048];
        this.tensorDestination = new uint[2048];

        for (int i = 0; i < this.x.Length; i++)
        {
            this.x[i] = (uint)((i * 17) + 31);
            this.y[i] = (uint)((i * 29) + 7);
        }
    }

    /// <summary>
    /// Adds histogram bins with the retired four-vector AVX2 pipeline.
    /// </summary>
    /// <returns>The first result, which keeps the writes observable to the benchmark harness.</returns>
    [Benchmark(Baseline = true)]
    public uint Legacy()
    {
        LegacyAdd(this.x, this.y, this.legacyDestination);
        return this.legacyDestination[0];
    }

    /// <summary>
    /// Adds histogram bins with the tensor compatibility pipeline.
    /// </summary>
    /// <returns>The first result, which keeps the writes observable to the benchmark harness.</returns>
    [Benchmark]
    public uint Tensor()
    {
        TensorPrimitives_.Add<uint>(this.x, this.y, this.tensorDestination);
        return this.tensorDestination[0];
    }

    /// <summary>
    /// Reproduces the retired WebP histogram addition loop for assembly comparison.
    /// </summary>
    /// <param name="x">The first histogram.</param>
    /// <param name="y">The second histogram.</param>
    /// <param name="destination">The destination receiving the sums.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void LegacyAdd(ReadOnlySpan<uint> x, ReadOnlySpan<uint> y, Span<uint> destination)
    {
        ref uint xRef = ref MemoryMarshal.GetReference(x);
        ref uint yRef = ref MemoryMarshal.GetReference(y);
        ref uint destinationRef = ref MemoryMarshal.GetReference(destination);

        nuint index = 0;

        do
        {
            Vector256<uint> x0 = Unsafe.As<uint, Vector256<uint>>(ref Unsafe.Add(ref xRef, index));
            Vector256<uint> x1 = Unsafe.As<uint, Vector256<uint>>(ref Unsafe.Add(ref xRef, index + 8));
            Vector256<uint> x2 = Unsafe.As<uint, Vector256<uint>>(ref Unsafe.Add(ref xRef, index + 16));
            Vector256<uint> x3 = Unsafe.As<uint, Vector256<uint>>(ref Unsafe.Add(ref xRef, index + 24));
            Vector256<uint> y0 = Unsafe.As<uint, Vector256<uint>>(ref Unsafe.Add(ref yRef, index));
            Vector256<uint> y1 = Unsafe.As<uint, Vector256<uint>>(ref Unsafe.Add(ref yRef, index + 8));
            Vector256<uint> y2 = Unsafe.As<uint, Vector256<uint>>(ref Unsafe.Add(ref yRef, index + 16));
            Vector256<uint> y3 = Unsafe.As<uint, Vector256<uint>>(ref Unsafe.Add(ref yRef, index + 24));

            Unsafe.As<uint, Vector256<uint>>(ref Unsafe.Add(ref destinationRef, index)) = Avx2.Add(x0, y0);
            Unsafe.As<uint, Vector256<uint>>(ref Unsafe.Add(ref destinationRef, index + 8)) = Avx2.Add(x1, y1);
            Unsafe.As<uint, Vector256<uint>>(ref Unsafe.Add(ref destinationRef, index + 16)) = Avx2.Add(x2, y2);
            Unsafe.As<uint, Vector256<uint>>(ref Unsafe.Add(ref destinationRef, index + 24)) = Avx2.Add(x3, y3);
            index += 32;
        }
        while (index <= (uint)x.Length - 32);

        for (int i = (int)index; i < x.Length; i++)
        {
            destination[i] = x[i] + y[i];
        }
    }
}

public class TensorPrimitivesByteAssemblyComparison
{
    private byte[] x = null!;
    private byte[] y = null!;
    private byte[] legacyDestination = null!;
    private byte[] tensorDestination = null!;

    /// <summary>
    /// Creates deterministic byte inputs and independent destinations.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.x = new byte[2048];
        this.y = new byte[2048];
        this.legacyDestination = new byte[2048];
        this.tensorDestination = new byte[2048];

        for (int i = 0; i < this.x.Length; i++)
        {
            this.x[i] = (byte)((i * 17) + 31);
            this.y[i] = (byte)((i * 29) + 7);
        }
    }

    /// <summary>
    /// Adds bytes with the retired WebP AVX2 pipeline.
    /// </summary>
    /// <returns>The first result, which keeps the writes observable to the benchmark harness.</returns>
    [Benchmark(Baseline = true)]
    public byte Legacy()
    {
        LegacyAdd(this.x, this.y, this.legacyDestination);
        return this.legacyDestination[0];
    }

    /// <summary>
    /// Adds bytes with the tensor compatibility pipeline.
    /// </summary>
    /// <returns>The first result, which keeps the writes observable to the benchmark harness.</returns>
    [Benchmark]
    public byte Tensor()
    {
        TensorPrimitives_.Add<byte>(this.x, this.y, this.tensorDestination);
        return this.tensorDestination[0];
    }

    /// <summary>
    /// Reproduces the retired WebP byte addition loop for assembly comparison.
    /// </summary>
    /// <param name="x">The first input.</param>
    /// <param name="y">The second input.</param>
    /// <param name="destination">The destination receiving modulo-256 sums.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void LegacyAdd(ReadOnlySpan<byte> x, ReadOnlySpan<byte> y, Span<byte> destination)
    {
        ref byte xRef = ref MemoryMarshal.GetReference(x);
        ref byte yRef = ref MemoryMarshal.GetReference(y);
        ref byte destinationRef = ref MemoryMarshal.GetReference(destination);

        nuint i;
        int maxPosition = x.Length & ~31;

        for (i = 0; i < (uint)maxPosition; i += 32)
        {
            Vector256<int> x0 = Unsafe.As<byte, Vector256<int>>(ref Unsafe.Add(ref xRef, i));
            Vector256<int> y0 = Unsafe.As<byte, Vector256<int>>(ref Unsafe.Add(ref yRef, i));
            Vector256<byte> result = x0.AsByte() + y0.AsByte();
            Unsafe.As<byte, Vector256<byte>>(ref Unsafe.Add(ref destinationRef, i)) = result;
        }

        for (; i < (uint)x.Length; i++)
        {
            Unsafe.Add(ref destinationRef, i) = (byte)(Unsafe.Add(ref xRef, i) + Unsafe.Add(ref yRef, i));
        }
    }
}

public class TensorPrimitivesSingleAddAssemblyComparison
{
    private float[] legacyTarget = null!;
    private float[] tensorTarget = null!;
    private float[] source = null!;

    /// <summary>
    /// Creates deterministic JPEG row inputs.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.legacyTarget = new float[2048];
        this.tensorTarget = new float[2048];
        this.source = new float[2048];

        for (int i = 0; i < this.source.Length; i++)
        {
            float value = ((i * 17) % 251) + 1;
            this.legacyTarget[i] = value;
            this.tensorTarget[i] = value;
            this.source[i] = ((i * 29) % 31) - 15;
        }
    }

    /// <summary>
    /// Adds JPEG row values with the retired AVX pipeline.
    /// </summary>
    /// <returns>The first result, which keeps the writes observable to the benchmark harness.</returns>
    [Benchmark(Baseline = true)]
    public float Legacy()
    {
        LegacyAdd(this.legacyTarget, this.source);
        return this.legacyTarget[0];
    }

    /// <summary>
    /// Adds JPEG row values with the tensor compatibility pipeline.
    /// </summary>
    /// <returns>The first result, which keeps the writes observable to the benchmark harness.</returns>
    [Benchmark]
    public float Tensor()
    {
        TensorPrimitives_.Add<float>(this.tensorTarget, this.source, this.tensorTarget);
        return this.tensorTarget[0];
    }

    /// <summary>
    /// Reproduces the retired JPEG row addition loop for assembly comparison.
    /// </summary>
    /// <param name="target">The destination row.</param>
    /// <param name="source">The row added to the destination.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void LegacyAdd(Span<float> target, ReadOnlySpan<float> source)
    {
        ref Vector256<float> targetVector = ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(target));
        ref Vector256<float> sourceVector = ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(source));
        nuint count = (uint)source.Length / (uint)Vector256<float>.Count;

        for (nuint i = 0; i < count; i++)
        {
            Unsafe.Add(ref targetVector, i) = Avx.Add(Unsafe.Add(ref targetVector, i), Unsafe.Add(ref sourceVector, i));
        }
    }
}

[GenericTypeArguments(typeof(byte))]
[GenericTypeArguments(typeof(uint))]
[GenericTypeArguments(typeof(int))]
[GenericTypeArguments(typeof(float))]
[GenericTypeArguments(typeof(double))]
public class TensorPrimitivesClampAssemblyComparison<T>
    where T : unmanaged, INumber<T>
{
    private T[] legacyValues = null!;
    private T[] tensorValues = null!;
    private T min;
    private T max;

    /// <summary>
    /// Creates deterministic clamp inputs for the current element type.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.legacyValues = new T[2048];
        this.tensorValues = new T[2048];
        this.min = T.CreateTruncating(64);
        this.max = T.CreateTruncating(128);

        for (int i = 0; i < this.legacyValues.Length; i++)
        {
            T value = T.CreateTruncating((i * 31) % 257);
            this.legacyValues[i] = value;
            this.tensorValues[i] = value;
        }
    }

    /// <summary>
    /// Clamps values with the retired <see cref="Vector{T}"/> pipeline.
    /// </summary>
    /// <returns>The first result, which keeps the writes observable to the benchmark harness.</returns>
    [Benchmark(Baseline = true)]
    public T Legacy()
    {
        LegacyClamp(this.legacyValues, this.min, this.max);
        return this.legacyValues[0];
    }

    /// <summary>
    /// Clamps values with the tensor compatibility pipeline.
    /// </summary>
    /// <returns>The first result, which keeps the writes observable to the benchmark harness.</returns>
    [Benchmark]
    public T Tensor()
    {
        TensorPrimitives_.Clamp(this.tensorValues, this.min, this.max, this.tensorValues);
        return this.tensorValues[0];
    }

    /// <summary>
    /// Reproduces the retired clamp pipeline for assembly comparison.
    /// </summary>
    /// <param name="span">The values to clamp.</param>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The inclusive upper bound.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void LegacyClamp(Span<T> span, T min, T max)
    {
        int remainder = Numerics.ModuloP2(span.Length, Vector<T>.Count);
        int adjustedCount = span.Length - remainder;

        if (adjustedCount > 0)
        {
            Vector<T> vectorMin = new(min);
            Vector<T> vectorMax = new(max);
            nint vectorCount = (nint)(uint)adjustedCount / Vector<T>.Count;
            nint remainingVectors = Numerics.Modulo4(vectorCount);
            nint unrolledVectors = vectorCount - remainingVectors;

            ref Vector<T> current0 = ref Unsafe.As<T, Vector<T>>(ref MemoryMarshal.GetReference(span));
            ref Vector<T> current1 = ref Unsafe.Add(ref current0, 1);
            ref Vector<T> current2 = ref Unsafe.Add(ref current0, 2);
            ref Vector<T> current3 = ref Unsafe.Add(ref current0, 3);
            ref Vector<T> end = ref Unsafe.Add(ref current0, unrolledVectors);

            while (Unsafe.IsAddressLessThan(ref current0, ref end))
            {
                current0 = Vector.Min(Vector.Max(vectorMin, current0), vectorMax);
                current1 = Vector.Min(Vector.Max(vectorMin, current1), vectorMax);
                current2 = Vector.Min(Vector.Max(vectorMin, current2), vectorMax);
                current3 = Vector.Min(Vector.Max(vectorMin, current3), vectorMax);

                current0 = ref Unsafe.Add(ref current0, 4);
                current1 = ref Unsafe.Add(ref current1, 4);
                current2 = ref Unsafe.Add(ref current2, 4);
                current3 = ref Unsafe.Add(ref current3, 4);
            }

            if (remainingVectors > 0)
            {
                current0 = ref end;
                end = ref Unsafe.Add(ref end, remainingVectors);

                while (Unsafe.IsAddressLessThan(ref current0, ref end))
                {
                    current0 = Vector.Min(Vector.Max(vectorMin, current0), vectorMax);
                    current0 = ref Unsafe.Add(ref current0, 1);
                }
            }
        }

        for (int i = adjustedCount; i < span.Length; i++)
        {
            T value = span[i];
            span[i] = value > max ? max : value < min ? min : value;
        }
    }
}

public class TensorPrimitivesIccMaxAssemblyComparison
{
    private Vector4[] legacyValues = null!;
    private Vector4[] tensorValues = null!;

    /// <summary>
    /// Creates deterministic ICC values containing positive and negative channels.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.legacyValues = new Vector4[512];
        this.tensorValues = new Vector4[512];

        for (int i = 0; i < this.legacyValues.Length; i++)
        {
            float value = ((i * 17) % 251) - 125;
            Vector4 vector = new(value, value + 1, value - 1, value + 2);
            this.legacyValues[i] = vector;
            this.tensorValues[i] = vector;
        }
    }

    /// <summary>
    /// Clips negative channels with the retired ICC pipeline.
    /// </summary>
    /// <returns>The first result, which keeps the writes observable to the benchmark harness.</returns>
    [Benchmark(Baseline = true)]
    public float Legacy()
    {
        for (int i = 0; i < this.legacyValues.Length; i++)
        {
            this.legacyValues[i] = Vector4.Max(this.legacyValues[i], Vector4.Zero);
        }

        return this.legacyValues[0].X;
    }

    /// <summary>
    /// Clips negative channels with the tensor compatibility pipeline.
    /// </summary>
    /// <returns>The first result, which keeps the writes observable to the benchmark harness.</returns>
    [Benchmark]
    public float Tensor()
    {
        Span<float> values = MemoryMarshal.Cast<Vector4, float>(this.tensorValues.AsSpan());
        TensorPrimitives_.Max(values, 0F, values);
        return values[0];
    }
}

public class TensorPrimitivesIccMultiplyAssemblyComparison
{
    private readonly float multiplier = 65280F / 65535F;
    private Vector4[] source = null!;
    private Vector4[] legacyDestination = null!;
    private Vector4[] tensorDestination = null!;

    /// <summary>
    /// Creates deterministic ICC inputs and independent destinations.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.source = new Vector4[512];
        this.legacyDestination = new Vector4[512];
        this.tensorDestination = new Vector4[512];

        for (int i = 0; i < this.source.Length; i++)
        {
            float value = ((i * 17) % 251) + 1;
            this.source[i] = new Vector4(value, value + 1, value + 2, value + 3);
        }
    }

    /// <summary>
    /// Multiplies ICC channels with the retired <see cref="Vector{T}"/> pipeline.
    /// </summary>
    /// <returns>The first result, which keeps the writes observable to the benchmark harness.</returns>
    [Benchmark(Baseline = true)]
    public float Legacy()
    {
        Span<float> source = MemoryMarshal.Cast<Vector4, float>(this.source.AsSpan());
        Span<float> destination = MemoryMarshal.Cast<Vector4, float>(this.legacyDestination.AsSpan());
        ref Vector<float> sourceVector = ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(source));
        ref Vector<float> destinationVector = ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(destination));
        Vector<float> scale = new(this.multiplier);
        nuint count = (uint)source.Length / (uint)Vector<float>.Count;

        for (nuint i = 0; i < count; i++)
        {
            Unsafe.Add(ref destinationVector, i) = Unsafe.Add(ref sourceVector, i) * scale;
        }

        return destination[0];
    }

    /// <summary>
    /// Multiplies ICC channels with the tensor compatibility pipeline.
    /// </summary>
    /// <returns>The first result, which keeps the writes observable to the benchmark harness.</returns>
    [Benchmark]
    public float Tensor()
    {
        Span<float> source = MemoryMarshal.Cast<Vector4, float>(this.source.AsSpan());
        Span<float> destination = MemoryMarshal.Cast<Vector4, float>(this.tensorDestination.AsSpan());
        TensorPrimitives_.Multiply(source, this.multiplier, destination);
        return destination[0];
    }
}

#if NET10_0_OR_GREATER
[GenericTypeArguments(typeof(byte))]
[GenericTypeArguments(typeof(uint))]
[GenericTypeArguments(typeof(float))]
public class TensorPrimitivesRuntimeAddAssemblyComparison<T>
    where T : unmanaged, INumber<T>
{
    private T[] x = null!;
    private T[] y = null!;
    private T[] compatibilityDestination = null!;
    private T[] runtimeDestination = null!;

    /// <summary>
    /// Creates deterministic inputs and independent destinations.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.x = new T[2048];
        this.y = new T[2048];
        this.compatibilityDestination = new T[2048];
        this.runtimeDestination = new T[2048];

        for (int i = 0; i < this.x.Length; i++)
        {
            this.x[i] = T.CreateTruncating((i * 17) + 31);
            this.y[i] = T.CreateTruncating((i * 29) + 7);
        }
    }

    /// <summary>
    /// Adds values with the compatibility implementation.
    /// </summary>
    /// <returns>The first result, which keeps the writes observable to the benchmark harness.</returns>
    [Benchmark(Baseline = true)]
    public T Compatibility()
    {
        TensorPrimitives_.Add<T>(this.x, this.y, this.compatibilityDestination);
        return this.compatibilityDestination[0];
    }

    /// <summary>
    /// Adds values with the .NET runtime implementation.
    /// </summary>
    /// <returns>The first result, which keeps the writes observable to the benchmark harness.</returns>
    [Benchmark]
    public T Runtime()
    {
        System.Numerics.Tensors.TensorPrimitives.Add<T>(this.x, this.y, this.runtimeDestination);
        return this.runtimeDestination[0];
    }
}

[GenericTypeArguments(typeof(byte))]
[GenericTypeArguments(typeof(uint))]
[GenericTypeArguments(typeof(int))]
[GenericTypeArguments(typeof(float))]
[GenericTypeArguments(typeof(double))]
public class TensorPrimitivesRuntimeClampAssemblyComparison<T>
    where T : unmanaged, INumber<T>
{
    private T[] compatibilityValues = null!;
    private T[] runtimeValues = null!;
    private T min;
    private T max;

    /// <summary>
    /// Creates deterministic inputs for both implementations.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.compatibilityValues = new T[2048];
        this.runtimeValues = new T[2048];
        this.min = T.CreateTruncating(64);
        this.max = T.CreateTruncating(128);

        for (int i = 0; i < this.compatibilityValues.Length; i++)
        {
            T value = T.CreateTruncating((i * 31) % 257);
            this.compatibilityValues[i] = value;
            this.runtimeValues[i] = value;
        }
    }

    /// <summary>
    /// Clamps values with the compatibility implementation.
    /// </summary>
    /// <returns>The first result, which keeps the writes observable to the benchmark harness.</returns>
    [Benchmark(Baseline = true)]
    public T Compatibility()
    {
        TensorPrimitives_.Clamp(this.compatibilityValues, this.min, this.max, this.compatibilityValues);
        return this.compatibilityValues[0];
    }

    /// <summary>
    /// Clamps values with the .NET runtime implementation.
    /// </summary>
    /// <returns>The first result, which keeps the writes observable to the benchmark harness.</returns>
    [Benchmark]
    public T Runtime()
    {
        System.Numerics.Tensors.TensorPrimitives.Clamp(this.runtimeValues, this.min, this.max, this.runtimeValues);
        return this.runtimeValues[0];
    }
}

public class TensorPrimitivesRuntimeSingleScalarAssemblyComparison
{
    private readonly float scalar = -1F;
    private float[] compatibilityValues = null!;
    private float[] runtimeValues = null!;

    /// <summary>
    /// Creates equivalent stable inputs for both implementations.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.compatibilityValues = new float[2048];
        this.runtimeValues = new float[2048];

        for (int i = 0; i < this.compatibilityValues.Length; i++)
        {
            float value = ((i * 17) % 251) + 1;
            this.compatibilityValues[i] = value;
            this.runtimeValues[i] = value;
        }
    }

    /// <summary>
    /// Divides values with the compatibility implementation.
    /// </summary>
    /// <returns>The first result, which keeps the writes observable to the benchmark harness.</returns>
    [Benchmark]
    public float CompatibilityDivide()
    {
        TensorPrimitives_.Divide(this.compatibilityValues, this.scalar, this.compatibilityValues);
        return this.compatibilityValues[0];
    }

    /// <summary>
    /// Divides values with the .NET runtime implementation.
    /// </summary>
    /// <returns>The first result, which keeps the writes observable to the benchmark harness.</returns>
    [Benchmark]
    public float RuntimeDivide()
    {
        System.Numerics.Tensors.TensorPrimitives.Divide(this.runtimeValues, this.scalar, this.runtimeValues);
        return this.runtimeValues[0];
    }

    /// <summary>
    /// Computes maximum values with the compatibility implementation.
    /// </summary>
    /// <returns>The first result, which keeps the writes observable to the benchmark harness.</returns>
    [Benchmark]
    public float CompatibilityMax()
    {
        TensorPrimitives_.Max(this.compatibilityValues, 0F, this.compatibilityValues);
        return this.compatibilityValues[0];
    }

    /// <summary>
    /// Computes maximum values with the .NET runtime implementation.
    /// </summary>
    /// <returns>The first result, which keeps the writes observable to the benchmark harness.</returns>
    [Benchmark]
    public float RuntimeMax()
    {
        System.Numerics.Tensors.TensorPrimitives.Max(this.runtimeValues, 0F, this.runtimeValues);
        return this.runtimeValues[0];
    }

    /// <summary>
    /// Multiplies values with the compatibility implementation.
    /// </summary>
    /// <returns>The first result, which keeps the writes observable to the benchmark harness.</returns>
    [Benchmark]
    public float CompatibilityMultiply()
    {
        TensorPrimitives_.Multiply(this.compatibilityValues, this.scalar, this.compatibilityValues);
        return this.compatibilityValues[0];
    }

    /// <summary>
    /// Multiplies values with the .NET runtime implementation.
    /// </summary>
    /// <returns>The first result, which keeps the writes observable to the benchmark harness.</returns>
    [Benchmark]
    public float RuntimeMultiply()
    {
        System.Numerics.Tensors.TensorPrimitives.Multiply(this.runtimeValues, this.scalar, this.runtimeValues);
        return this.runtimeValues[0];
    }
}
#endif
