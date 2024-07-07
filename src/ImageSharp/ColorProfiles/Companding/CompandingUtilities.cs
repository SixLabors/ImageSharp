// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SixLabors.ImageSharp.ColorProfiles.Companding;

/// <summary>
/// Companding utilities that allow the accelerated compression-expansion of color channels.
/// </summary>
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
        => CompressLookupTables.GetOrAdd((typeof(T), modifier), args => CreateLookupTableImpl(compandingFunction, args.Item2));

    /// <summary>
    /// Lazily creates and stores a companding expanding lookup table using the given function and modifier.
    /// </summary>
    /// <typeparam name="T">The type of companding function.</typeparam>
    /// <param name="compandingFunction">The companding function.</param>
    /// <param name="modifier">A modifier to pass to the function.</param>
    /// <returns>The <see cref="float"/> array.</returns>
    public static float[] GetExpandLookupTable<T>(Func<double, double, double> compandingFunction, double modifier = 0)
        => ExpandLookupTables.GetOrAdd((typeof(T), modifier), args => CreateLookupTableImpl(compandingFunction, args.Item2));

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
            result[i] = (float)d;
        }

        return result;
    }

    /// <summary>
    /// Performs the companding operation on the given vectors using the given table.
    /// </summary>
    /// <param name="vectors">The span of vectors.</param>
    /// <param name="table">The lookup table.</param>
    public static void Compand(Span<Vector4> vectors, float[] table)
    {
        DebugGuard.MustBeGreaterThanOrEqualTo(table.Length, Length, nameof(table));

        if (Avx2.IsSupported && vectors.Length >= 2)
        {
            CompandAvx2(vectors, table);

            if (Numerics.Modulo2(vectors.Length) != 0)
            {
                // Vector4 fits neatly in pairs. Any overlap has to be equal to 1.
                ref Vector4 last = ref MemoryMarshal.GetReference(vectors[^1..]);
                last = Compand(last, table);
            }
        }
        else
        {
            CompandScalar(vectors, table);
        }
    }

    /// <summary>
    /// Performs the companding operation on the given vector using the given table.
    /// </summary>
    /// <param name="vector">The vector.</param>
    /// <param name="table">The lookup table.</param>
    /// <returns>The <see cref="Vector4"/></returns>
    public static Vector4 Compand(Vector4 vector, float[] table)
    {
        DebugGuard.MustBeGreaterThanOrEqualTo(table.Length, Length, nameof(table));

        Vector4 zero = Vector4.Zero;
        Vector4 scale = new(Scale);

        Vector4 multiplied = Numerics.Clamp(vector * Scale, zero, scale);

        float f0 = multiplied.X;
        float f1 = multiplied.Y;
        float f2 = multiplied.Z;

        uint i0 = (uint)f0;
        uint i1 = (uint)f1;
        uint i2 = (uint)f2;

        // Alpha is already a linear representation of opacity so we do not want to convert it.
        vector.X = Numerics.Lerp(table[i0], table[i0 + 1], f0 - (int)i0);
        vector.Y = Numerics.Lerp(table[i1], table[i1 + 1], f1 - (int)i1);
        vector.Z = Numerics.Lerp(table[i2], table[i2 + 1], f2 - (int)i2);

        return vector;
    }

    private static unsafe void CompandAvx2(Span<Vector4> vectors, float[] table)
    {
        fixed (float* tablePointer = &MemoryMarshal.GetArrayDataReference(table))
        {
            Vector256<float> scale = Vector256.Create((float)Scale);
            Vector256<float> zero = Vector256<float>.Zero;
            Vector256<int> offset = Vector256.Create(1);

            // Divide by 2 as 4 elements per Vector4 and 8 per Vector256<float>
            ref Vector256<float> vectorsBase = ref Unsafe.As<Vector4, Vector256<float>>(ref MemoryMarshal.GetReference(vectors));
            ref Vector256<float> vectorsLast = ref Unsafe.Add(ref vectorsBase, (uint)vectors.Length / 2u);

            while (Unsafe.IsAddressLessThan(ref vectorsBase, ref vectorsLast))
            {
                Vector256<float> multiplied = Avx.Multiply(scale, vectorsBase);
                multiplied = Avx.Min(Avx.Max(zero, multiplied), scale);

                Vector256<int> truncated = Avx.ConvertToVector256Int32WithTruncation(multiplied);
                Vector256<float> truncatedF = Avx.ConvertToVector256Single(truncated);

                Vector256<float> low = Avx2.GatherVector256(tablePointer, truncated, sizeof(float));
                Vector256<float> high = Avx2.GatherVector256(tablePointer, Avx2.Add(truncated, offset), sizeof(float));

                // Alpha is already a linear representation of opacity so we do not want to convert it.
                Vector256<float> companded = Numerics.Lerp(low, high, Avx.Subtract(multiplied, truncatedF));
                vectorsBase = Avx.Blend(companded, vectorsBase, Numerics.BlendAlphaControl);
                vectorsBase = ref Unsafe.Add(ref vectorsBase, 1);
            }
        }
    }

    private static unsafe void CompandScalar(Span<Vector4> vectors, float[] table)
    {
        fixed (float* tablePointer = &MemoryMarshal.GetArrayDataReference(table))
        {
            Vector4 zero = Vector4.Zero;
            Vector4 scale = new(Scale);
            ref Vector4 vectorsBase = ref MemoryMarshal.GetReference(vectors);
            ref Vector4 vectorsLast = ref Unsafe.Add(ref vectorsBase, (uint)vectors.Length);

            while (Unsafe.IsAddressLessThan(ref vectorsBase, ref vectorsLast))
            {
                Vector4 multiplied = Numerics.Clamp(vectorsBase * Scale, zero, scale);

                float f0 = multiplied.X;
                float f1 = multiplied.Y;
                float f2 = multiplied.Z;

                uint i0 = (uint)f0;
                uint i1 = (uint)f1;
                uint i2 = (uint)f2;

                // Alpha is already a linear representation of opacity so we do not want to convert it.
                vectorsBase.X = Numerics.Lerp(tablePointer[i0], tablePointer[i0 + 1], f0 - (int)i0);
                vectorsBase.Y = Numerics.Lerp(tablePointer[i1], tablePointer[i1 + 1], f1 - (int)i1);
                vectorsBase.Z = Numerics.Lerp(tablePointer[i2], tablePointer[i2 + 1], f2 - (int)i2);

                vectorsBase = ref Unsafe.Add(ref vectorsBase, 1);
            }
        }
    }
}
