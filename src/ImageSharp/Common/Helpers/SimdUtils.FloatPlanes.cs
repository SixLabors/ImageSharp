// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp;

/// <content>
/// Converts planar floating-point components to and from contiguous four-component vectors.
/// </content>
internal static partial class SimdUtils
{
    /// <summary>
    /// Interleaves equally sized component planes into four-component vectors.
    /// Empty Y or Z planes supply zero. An empty W plane supplies one.
    /// </summary>
    /// <param name="component0">The X components. Their count determines the number of output vectors.</param>
    /// <param name="component1">The Y components, or an empty span to supply zero.</param>
    /// <param name="component2">The Z components, or an empty span to supply zero.</param>
    /// <param name="component3">The W components, or an empty span to supply one.</param>
    /// <param name="destination">The output vectors. Its capacity must cover every nonempty input plane.</param>
    public static void InterleaveFloatPlanes(
        ReadOnlySpan<float> component0,
        ReadOnlySpan<float> component1,
        ReadOnlySpan<float> component2,
        ReadOnlySpan<float> component3,
        Span<Vector4> destination)
    {
        Guard.IsTrue(component1.IsEmpty || component1.Length == component0.Length, nameof(component1), "Components must be of same size!");
        Guard.IsTrue(component2.IsEmpty || component2.Length == component0.Length, nameof(component2), "Components must be of same size!");
        Guard.IsTrue(component3.IsEmpty || component3.Length == component0.Length, nameof(component3), "Components must be of same size!");
        Guard.DestinationShouldNotBeTooShort(component0, destination, nameof(destination));

        ref float c0 = ref MemoryMarshal.GetReference(component0);
        ref float c1 = ref MemoryMarshal.GetReference(component1);
        ref float c2 = ref MemoryMarshal.GetReference(component2);
        ref float c3 = ref MemoryMarshal.GetReference(component3);
        ref float output = ref Unsafe.As<Vector4, float>(ref MemoryMarshal.GetReference(destination));
        int i = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = (component0.Length - i).Vector512Count<float>();

            // Registers hold consecutive values from each plane. The existing transpose writes complete [X,Y,Z,W] groups.
            // Missing planes use constants in registers; no dummy rows or copied samples are needed.
            for (nuint block = 0; block < count; block++, i += Vector512<float>.Count)
            {
                Vector512<float> x = Vector512.LoadUnsafe(ref c0, (uint)i);
                Vector512<float> y = component1.IsEmpty ? Vector512<float>.Zero : Vector512.LoadUnsafe(ref c1, (uint)i);
                Vector512<float> z = component2.IsEmpty ? Vector512<float>.Zero : Vector512.LoadUnsafe(ref c2, (uint)i);
                Vector512<float> w = component3.IsEmpty ? Vector512.Create(1F) : Vector512.LoadUnsafe(ref c3, (uint)i);
                Interleave4(x, y, z, w, ref Unsafe.Add(ref output, (uint)i * 4));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (component0.Length - i).Vector256Count<float>();

            // Registers hold consecutive values from each plane. The existing transpose writes complete [X,Y,Z,W] groups.
            // Missing planes use constants in registers; no dummy rows or copied samples are needed.
            for (nuint block = 0; block < count; block++, i += Vector256<float>.Count)
            {
                Vector256<float> x = Vector256.LoadUnsafe(ref c0, (uint)i);
                Vector256<float> y = component1.IsEmpty ? Vector256<float>.Zero : Vector256.LoadUnsafe(ref c1, (uint)i);
                Vector256<float> z = component2.IsEmpty ? Vector256<float>.Zero : Vector256.LoadUnsafe(ref c2, (uint)i);
                Vector256<float> w = component3.IsEmpty ? Vector256.Create(1F) : Vector256.LoadUnsafe(ref c3, (uint)i);
                Interleave4(x, y, z, w, ref Unsafe.Add(ref output, (uint)i * 4));
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (component0.Length - i).Vector128Count<float>();

            // Registers hold consecutive values from each plane. The existing transpose writes complete [X,Y,Z,W] groups.
            // Missing planes use constants in registers; no dummy rows or copied samples are needed.
            for (nuint block = 0; block < count; block++, i += Vector128<float>.Count)
            {
                Vector128<float> x = Vector128.LoadUnsafe(ref c0, (uint)i);
                Vector128<float> y = component1.IsEmpty ? Vector128<float>.Zero : Vector128.LoadUnsafe(ref c1, (uint)i);
                Vector128<float> z = component2.IsEmpty ? Vector128<float>.Zero : Vector128.LoadUnsafe(ref c2, (uint)i);
                Vector128<float> w = component3.IsEmpty ? Vector128.Create(1F) : Vector128.LoadUnsafe(ref c3, (uint)i);
                Interleave4(x, y, z, w, ref Unsafe.Add(ref output, (uint)i * 4));
            }
        }

        // The scalar remainder handles short tails and all samples without hardware intrinsics. It copies the same bits and constants.
        for (; i < component0.Length; i++)
        {
            destination[i] = new Vector4(
                component0[i],
                component1.IsEmpty ? 0F : component1[i],
                component2.IsEmpty ? 0F : component2[i],
                component3.IsEmpty ? 1F : component3[i]);
        }
    }

    /// <summary>
    /// Deinterleaves four-component vectors into the supplied component planes without changing component bits.
    /// Empty destination planes discard their corresponding components.
    /// </summary>
    /// <param name="source">The input vectors.</param>
    /// <param name="component0">The X destination. Its capacity must cover the source vectors.</param>
    /// <param name="component1">The Y destination, or an empty span to discard Y.</param>
    /// <param name="component2">The Z destination, or an empty span to discard Z.</param>
    /// <param name="component3">The W destination, or an empty span to discard W.</param>
    public static void DeinterleaveFloatPlanes(
        ReadOnlySpan<Vector4> source,
        Span<float> component0,
        Span<float> component1,
        Span<float> component2,
        Span<float> component3)
    {
        Guard.IsTrue(component1.IsEmpty || component1.Length == component0.Length, nameof(component1), "Components must be of same size!");
        Guard.IsTrue(component2.IsEmpty || component2.Length == component0.Length, nameof(component2), "Components must be of same size!");
        Guard.IsTrue(component3.IsEmpty || component3.Length == component0.Length, nameof(component3), "Components must be of same size!");
        Guard.DestinationShouldNotBeTooShort(source, component0, nameof(component0));

        ref float c0 = ref MemoryMarshal.GetReference(component0);
        ref float c1 = ref MemoryMarshal.GetReference(component1);
        ref float c2 = ref MemoryMarshal.GetReference(component2);
        ref float c3 = ref MemoryMarshal.GetReference(component3);
        int i = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = (source.Length - i).Vector512Count<float>();

            // Read a complete interleaved block before storing any plane. Optional outputs add no temporary storage.
            for (nuint block = 0; block < count; block++, i += Vector512<float>.Count)
            {
                Deinterleave4(source, i, out Vector512<float> x, out Vector512<float> y, out Vector512<float> z, out Vector512<float> w);
                x.StoreUnsafe(ref c0, (uint)i);

                if (!component1.IsEmpty)
                {
                    y.StoreUnsafe(ref c1, (uint)i);
                }

                if (!component2.IsEmpty)
                {
                    z.StoreUnsafe(ref c2, (uint)i);
                }

                if (!component3.IsEmpty)
                {
                    w.StoreUnsafe(ref c3, (uint)i);
                }
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (source.Length - i).Vector256Count<float>();

            // Read a complete interleaved block before storing any plane. Optional outputs add no temporary storage.
            for (nuint block = 0; block < count; block++, i += Vector256<float>.Count)
            {
                Deinterleave4(source, i, out Vector256<float> x, out Vector256<float> y, out Vector256<float> z, out Vector256<float> w);
                x.StoreUnsafe(ref c0, (uint)i);

                if (!component1.IsEmpty)
                {
                    y.StoreUnsafe(ref c1, (uint)i);
                }

                if (!component2.IsEmpty)
                {
                    z.StoreUnsafe(ref c2, (uint)i);
                }

                if (!component3.IsEmpty)
                {
                    w.StoreUnsafe(ref c3, (uint)i);
                }
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (source.Length - i).Vector128Count<float>();

            // Read a complete interleaved block before storing any plane. Optional outputs add no temporary storage.
            for (nuint block = 0; block < count; block++, i += Vector128<float>.Count)
            {
                Deinterleave4(source, i, out Vector128<float> x, out Vector128<float> y, out Vector128<float> z, out Vector128<float> w);
                x.StoreUnsafe(ref c0, (uint)i);

                if (!component1.IsEmpty)
                {
                    y.StoreUnsafe(ref c1, (uint)i);
                }

                if (!component2.IsEmpty)
                {
                    z.StoreUnsafe(ref c2, (uint)i);
                }

                if (!component3.IsEmpty)
                {
                    w.StoreUnsafe(ref c3, (uint)i);
                }
            }
        }

        // The scalar remainder uses the same optional-plane rules for the final samples.
        for (; i < source.Length; i++)
        {
            Vector4 value = source[i];
            component0[i] = value.X;

            if (!component1.IsEmpty)
            {
                component1[i] = value.Y;
            }

            if (!component2.IsEmpty)
            {
                component2[i] = value.Z;
            }

            if (!component3.IsEmpty)
            {
                component3[i] = value.W;
            }
        }
    }
}
