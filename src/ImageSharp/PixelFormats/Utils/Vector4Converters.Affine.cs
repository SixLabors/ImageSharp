// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.PixelFormats.Utils;

internal static partial class Vector4Converters
{
    /// <summary>
    /// Multiplies each vector component and then adds the corresponding offset component.
    /// </summary>
    /// <param name="vectors">The vectors to transform in place.</param>
    /// <param name="multiplier">The component-wise multiplier.</param>
    /// <param name="offset">The component-wise offset applied after multiplication.</param>
    internal static void MultiplyThenAdd(Span<Vector4> vectors, Vector4 multiplier, Vector4 offset)
        => Apply(vectors, new MultiplyThenAddOperator(multiplier, offset));

    /// <summary>
    /// Adds the corresponding offset component and then divides each vector component by its divisor.
    /// </summary>
    /// <param name="vectors">The vectors to transform in place.</param>
    /// <param name="offset">The component-wise offset applied before division.</param>
    /// <param name="divisor">The component-wise divisor.</param>
    internal static void AddThenDivide(Span<Vector4> vectors, Vector4 offset, Vector4 divisor)
        => Apply(vectors, new AddThenDivideOperator(offset, divisor));

    /// <summary>
    /// Applies a stateful component transform to a vector buffer in place.
    /// </summary>
    /// <typeparam name="TOperator">The transform selected for this closed traversal.</typeparam>
    /// <param name="vectors">The vectors to transform.</param>
    /// <param name="transform">The transform and its component-wise state.</param>
    // Closing and inlining the traversal lets the JIT devirtualize every Invoke call,
    // specialize the active hardware-width branches, and discard unused operator state.
    [MethodImpl(InliningOptions.AlwaysInline)]
    private static void Apply<TOperator>(Span<Vector4> vectors, TOperator transform)
        where TOperator : struct, IStatefulVector4Operator
    {
        ref Vector4 vectorBase = ref MemoryMarshal.GetReference(vectors);
        int index = 0;

        // A Vector4 is one complete pixel. Descending register widths therefore consume groups
        // of four, two, and one pixels without splitting a pixel across traversal boundaries.
        if (Vector512.IsHardwareAccelerated)
        {
            int vectorsPerRegister = Vector512<float>.Count / Vector128<float>.Count;
            int oneRegisterFromEnd = vectors.Length - vectorsPerRegister;

            for (; index <= oneRegisterFromEnd; index += vectorsPerRegister)
            {
                ref Vector512<float> vector = ref Unsafe.As<Vector4, Vector512<float>>(
                    ref Unsafe.Add(ref vectorBase, (uint)index));

                vector = transform.Invoke(vector);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            int vectorsPerRegister = Vector256<float>.Count / Vector128<float>.Count;
            int oneRegisterFromEnd = vectors.Length - vectorsPerRegister;

            for (; index <= oneRegisterFromEnd; index += vectorsPerRegister)
            {
                ref Vector256<float> vector = ref Unsafe.As<Vector4, Vector256<float>>(
                    ref Unsafe.Add(ref vectorBase, (uint)index));

                vector = transform.Invoke(vector);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            // Vector128<float> and Vector4 have the same four-lane layout, so this stage
            // consumes every remaining complete pixel and leaves no scalar remainder.
            for (; index < vectors.Length; index++)
            {
                ref Vector128<float> vector = ref Unsafe.As<Vector4, Vector128<float>>(
                    ref Unsafe.Add(ref vectorBase, (uint)index));

                vector = transform.Invoke(vector);
            }

            return;
        }

        // Vector4 retains the same component order and expression ordering when hardware
        // intrinsics are unavailable, preserving the format-specific conversion contract.
        for (; index < vectors.Length; index++)
        {
            ref Vector4 vector = ref Unsafe.Add(ref vectorBase, (uint)index);
            vector = transform.Invoke(vector);
        }
    }
}
