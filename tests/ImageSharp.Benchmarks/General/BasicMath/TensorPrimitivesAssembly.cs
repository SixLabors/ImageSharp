// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Benchmarks.General.BasicMath;

/// <summary>
/// Exposes every floating-point tensor compatibility operation for assembly inspection.
/// </summary>
[Config(typeof(Config.Analysis))]
public class TensorPrimitivesAssembly
{
    private const int Count = 2048;

    private readonly float[] x = new float[Count];
    private readonly float[] y = new float[Count];
    private readonly float[] destination = new float[Count];

    /// <summary>
    /// Populates the input spans with deterministic non-uniform values.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        for (int i = 0; i < Count; i++)
        {
            this.x[i] = ((i * 17) % 251) + 1;
            this.y[i] = ((i * 29) % 251) + 1;
        }
    }

    /// <summary>
    /// Adds two floating-point spans.
    /// </summary>
    /// <returns>The first result, which keeps the destination observable.</returns>
    [Benchmark]
    public float Add()
    {
        TensorPrimitives_.Add<float>(this.x, this.y, this.destination);
        return this.destination[0];
    }

    /// <summary>
    /// Clamps a floating-point span between scalar bounds.
    /// </summary>
    /// <returns>The first result, which keeps the destination observable.</returns>
    [Benchmark]
    public float Clamp()
    {
        TensorPrimitives_.Clamp(this.x, 64F, 128F, this.destination);
        return this.destination[0];
    }

    /// <summary>
    /// Divides a floating-point span by a scalar.
    /// </summary>
    /// <returns>The first result, which keeps the destination observable.</returns>
    [Benchmark]
    public float Divide()
    {
        TensorPrimitives_.Divide(this.x, 4096F, this.destination);
        return this.destination[0];
    }

    /// <summary>
    /// Computes the element-wise maximum of a floating-point span and a scalar.
    /// </summary>
    /// <returns>The first result, which keeps the destination observable.</returns>
    [Benchmark]
    public float Max()
    {
        TensorPrimitives_.Max(this.x, 64F, this.destination);
        return this.destination[0];
    }

    /// <summary>
    /// Multiplies a floating-point span by a scalar.
    /// </summary>
    /// <returns>The first result, which keeps the destination observable.</returns>
    [Benchmark]
    public float Multiply()
    {
        TensorPrimitives_.Multiply(this.x, 0.5F, this.destination);
        return this.destination[0];
    }
}

/// <summary>
/// Exposes integral addition specializations for assembly inspection.
/// </summary>
/// <typeparam name="T">The integral element type.</typeparam>
[Config(typeof(Config.Analysis))]
[GenericTypeArguments(typeof(byte))]
[GenericTypeArguments(typeof(uint))]
public class TensorPrimitivesIntegralAddAssembly<T>
    where T : unmanaged, INumber<T>
{
    private const int Count = 2048;

    private readonly T[] x = new T[Count];
    private readonly T[] y = new T[Count];
    private readonly T[] destination = new T[Count];

    /// <summary>
    /// Populates the input spans with deterministic non-uniform values.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        for (int i = 0; i < Count; i++)
        {
            this.x[i] = T.CreateTruncating((i * 17) + 31);
            this.y[i] = T.CreateTruncating((i * 29) + 7);
        }
    }

    /// <summary>
    /// Adds two integral spans.
    /// </summary>
    /// <returns>The first result, which keeps the destination observable.</returns>
    [Benchmark]
    public T Add()
    {
        TensorPrimitives_.Add<T>(this.x, this.y, this.destination);
        return this.destination[0];
    }
}

/// <summary>
/// Exposes integral clamp specializations for assembly inspection.
/// </summary>
/// <typeparam name="T">The integral element type.</typeparam>
[Config(typeof(Config.Analysis))]
[GenericTypeArguments(typeof(byte))]
[GenericTypeArguments(typeof(uint))]
[GenericTypeArguments(typeof(int))]
public class TensorPrimitivesIntegralClampAssembly<T>
    where T : unmanaged, INumber<T>
{
    private const int Count = 2048;

    private readonly T[] source = new T[Count];
    private readonly T[] destination = new T[Count];
    private T min;
    private T max;

    /// <summary>
    /// Populates the input span and scalar bounds with deterministic values.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.min = T.CreateTruncating(64);
        this.max = T.CreateTruncating(128);

        for (int i = 0; i < Count; i++)
        {
            this.source[i] = T.CreateTruncating((i * 31) % 257);
        }
    }

    /// <summary>
    /// Clamps an integral span between scalar bounds.
    /// </summary>
    /// <returns>The first result, which keeps the destination observable.</returns>
    [Benchmark]
    public T Clamp()
    {
        TensorPrimitives_.Clamp(this.source, this.min, this.max, this.destination);
        return this.destination[0];
    }
}
