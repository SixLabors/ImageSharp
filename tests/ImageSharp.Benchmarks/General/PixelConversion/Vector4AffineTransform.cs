// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats.Utils;

namespace SixLabors.ImageSharp.Benchmarks.General.PixelConversion;

/// <summary>
/// Compares operator-driven affine vector transforms with the duplicated traversals they replace.
/// </summary>
[Config(typeof(Config.Short))]
public class Vector4AffineTransform
{
    private static readonly Vector4 Multiplier = new(255F, 2F, 65535F, .5F);
    private static readonly Vector4 Offset = new(17F, -1F, 32768F, 3F);
    private static readonly Vector4 Divisor = new(255F, 2F, 65535F, .5F);

    private Vector4[] current;
    private Vector4[] baseline;

    /// <summary>
    /// Gets or sets the number of vectors transformed by each invocation.
    /// </summary>
    [Params(1, 3, 4, 17, 256, 4096)]
    public int Count { get; set; }

    /// <summary>
    /// Creates identical non-uniform buffers for the current and baseline traversals.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.current = new Vector4[this.Count];

        for (int i = 0; i < this.current.Length; i++)
        {
            this.current[i] = new Vector4(i + .25F, i + .5F, i + .75F, i + 1F);
        }

        this.baseline = [.. this.current];
    }

    /// <summary>
    /// Executes the operator-driven multiply-then-add traversal.
    /// </summary>
    [Benchmark]
    public void CurrentMultiplyThenAdd()
        => Vector4Converters.MultiplyThenAdd(this.current, Multiplier, Offset);

    /// <summary>
    /// Executes the duplicated multiply-then-add traversal.
    /// </summary>
    [Benchmark(Baseline = true)]
    public void BaselineMultiplyThenAdd()
        => BaselineMultiplyThenAdd(this.baseline, Multiplier, Offset);

    /// <summary>
    /// Executes the operator-driven add-then-divide traversal.
    /// </summary>
    [Benchmark]
    public void CurrentAddThenDivide()
        => Vector4Converters.AddThenDivide(this.current, Offset, Divisor);

    /// <summary>
    /// Executes the duplicated add-then-divide traversal.
    /// </summary>
    [Benchmark]
    public void BaselineAddThenDivide()
        => BaselineAddThenDivide(this.baseline, Offset, Divisor);

    /// <summary>
    /// Retains the multiply-then-add traversal being replaced for direct measurement.
    /// </summary>
    /// <param name="vectors">The vectors to transform.</param>
    /// <param name="multiplier">The component-wise multiplier.</param>
    /// <param name="offset">The component-wise offset.</param>
    internal static void BaselineMultiplyThenAdd(Span<Vector4> vectors, Vector4 multiplier, Vector4 offset)
    {
        ref Vector4 vectorBase = ref MemoryMarshal.GetReference(vectors);
        int index = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            int vectorsPerVector = Vector512<float>.Count / Vector128<float>.Count;
            Vector256<float> multiplier256 = Vector256.Create(multiplier.AsVector128(), multiplier.AsVector128());
            Vector256<float> offset256 = Vector256.Create(offset.AsVector128(), offset.AsVector128());
            Vector512<float> multiplier512 = Vector512.Create(multiplier256, multiplier256);
            Vector512<float> offset512 = Vector512.Create(offset256, offset256);

            for (; index <= vectors.Length - vectorsPerVector; index += vectorsPerVector)
            {
                ref Vector512<float> vector = ref Unsafe.As<Vector4, Vector512<float>>(
                    ref Unsafe.Add(ref vectorBase, (uint)index));

                vector = (vector * multiplier512) + offset512;
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            int vectorsPerVector = Vector256<float>.Count / Vector128<float>.Count;
            Vector256<float> multiplier256 = Vector256.Create(multiplier.AsVector128(), multiplier.AsVector128());
            Vector256<float> offset256 = Vector256.Create(offset.AsVector128(), offset.AsVector128());

            for (; index <= vectors.Length - vectorsPerVector; index += vectorsPerVector)
            {
                ref Vector256<float> vector = ref Unsafe.As<Vector4, Vector256<float>>(
                    ref Unsafe.Add(ref vectorBase, (uint)index));

                vector = (vector * multiplier256) + offset256;
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<float> multiplier128 = multiplier.AsVector128();
            Vector128<float> offset128 = offset.AsVector128();

            for (; index < vectors.Length; index++)
            {
                ref Vector128<float> vector = ref Unsafe.As<Vector4, Vector128<float>>(
                    ref Unsafe.Add(ref vectorBase, (uint)index));

                vector = (vector * multiplier128) + offset128;
            }

            return;
        }

        for (; index < vectors.Length; index++)
        {
            ref Vector4 vector = ref Unsafe.Add(ref vectorBase, (uint)index);
            vector = (vector * multiplier) + offset;
        }
    }

    /// <summary>
    /// Retains the add-then-divide traversal being replaced for direct measurement.
    /// </summary>
    /// <param name="vectors">The vectors to transform.</param>
    /// <param name="offset">The component-wise offset.</param>
    /// <param name="divisor">The component-wise divisor.</param>
    internal static void BaselineAddThenDivide(Span<Vector4> vectors, Vector4 offset, Vector4 divisor)
    {
        ref Vector4 vectorBase = ref MemoryMarshal.GetReference(vectors);
        int index = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            int vectorsPerVector = Vector512<float>.Count / Vector128<float>.Count;
            Vector256<float> offset256 = Vector256.Create(offset.AsVector128(), offset.AsVector128());
            Vector256<float> divisor256 = Vector256.Create(divisor.AsVector128(), divisor.AsVector128());
            Vector512<float> offset512 = Vector512.Create(offset256, offset256);
            Vector512<float> divisor512 = Vector512.Create(divisor256, divisor256);

            for (; index <= vectors.Length - vectorsPerVector; index += vectorsPerVector)
            {
                ref Vector512<float> vector = ref Unsafe.As<Vector4, Vector512<float>>(
                    ref Unsafe.Add(ref vectorBase, (uint)index));

                vector = (vector + offset512) / divisor512;
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            int vectorsPerVector = Vector256<float>.Count / Vector128<float>.Count;
            Vector256<float> offset256 = Vector256.Create(offset.AsVector128(), offset.AsVector128());
            Vector256<float> divisor256 = Vector256.Create(divisor.AsVector128(), divisor.AsVector128());

            for (; index <= vectors.Length - vectorsPerVector; index += vectorsPerVector)
            {
                ref Vector256<float> vector = ref Unsafe.As<Vector4, Vector256<float>>(
                    ref Unsafe.Add(ref vectorBase, (uint)index));

                vector = (vector + offset256) / divisor256;
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<float> offset128 = offset.AsVector128();
            Vector128<float> divisor128 = divisor.AsVector128();

            for (; index < vectors.Length; index++)
            {
                ref Vector128<float> vector = ref Unsafe.As<Vector4, Vector128<float>>(
                    ref Unsafe.Add(ref vectorBase, (uint)index));

                vector = (vector + offset128) / divisor128;
            }

            return;
        }

        for (; index < vectors.Length; index++)
        {
            ref Vector4 vector = ref Unsafe.Add(ref vectorBase, (uint)index);
            vector = (vector + offset) / divisor;
        }
    }
}
