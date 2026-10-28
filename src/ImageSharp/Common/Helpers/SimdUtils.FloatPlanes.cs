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
    /// Interleaves three equally sized component planes and an optional fourth plane into four-component vectors.
    /// Values are copied without changing their floating-point representation.
    /// </summary>
    /// <param name="component0">The values for <see cref="Vector4.X"/>.</param>
    /// <param name="component1">The values for <see cref="Vector4.Y"/>.</param>
    /// <param name="component2">The values for <see cref="Vector4.Z"/>.</param>
    /// <param name="component3">The values for <see cref="Vector4.W"/>, or an empty span to use 1 for every value.</param>
    /// <param name="destination">The destination vectors.</param>
    internal static void InterleaveFloatPlanes(
        ReadOnlySpan<float> component0,
        ReadOnlySpan<float> component1,
        ReadOnlySpan<float> component2,
        ReadOnlySpan<float> component3,
        Span<Vector4> destination)
    {
        Guard.IsTrue(component1.Length == component0.Length, nameof(component1), "Components must be of same size!");
        Guard.IsTrue(component2.Length == component0.Length, nameof(component2), "Components must be of same size!");
        Guard.IsTrue(component3.IsEmpty || component3.Length == component0.Length, nameof(component3), "Components must be of same size!");
        Guard.DestinationShouldNotBeTooShort(component0, destination, nameof(destination));

        ref float c0 = ref MemoryMarshal.GetReference(component0);
        ref float c1 = ref MemoryMarshal.GetReference(component1);
        ref float c2 = ref MemoryMarshal.GetReference(component2);
        ref float c3 = ref MemoryMarshal.GetReference(component3);
        ref float d = ref Unsafe.As<Vector4, float>(ref MemoryMarshal.GetReference(destination));
        bool hasComponent3 = !component3.IsEmpty;
        int i = 0;

        if (Vector128.IsHardwareAccelerated)
        {
            // Load four values from each plane: v0=[c0.0,c0.1,c0.2,c0.3] through
            // v3=[c3.0,c3.1,c3.2,c3.3]. An absent fourth plane supplies four 1.0 values.
            // The unpack helpers transpose component bits without arithmetic, preserving
            // signed zero, infinities, and NaN payload bits.
            for (; i <= component0.Length - 4; i += 4)
            {
                Vector128<float> v0 = Vector128.LoadUnsafe(ref c0, (nuint)i);
                Vector128<float> v1 = Vector128.LoadUnsafe(ref c1, (nuint)i);
                Vector128<float> v2 = Vector128.LoadUnsafe(ref c2, (nuint)i);
                Vector128<float> v3 = hasComponent3 ? Vector128.LoadUnsafe(ref c3, (nuint)i) : Vector128.Create(1F);

                // The 32-bit zips produce c01Low=[c0.0,c1.0,c0.1,c1.1] and
                // c01High=[c0.2,c1.2,c0.3,c1.3], with equivalent pairs for components 2 and 3.
                Vector128<float> c01Low = Vector128_.UnpackLow(v0, v1);
                Vector128<float> c01High = Vector128_.UnpackHigh(v0, v1);
                Vector128<float> c23Low = Vector128_.UnpackLow(v2, v3);
                Vector128<float> c23High = Vector128_.UnpackHigh(v2, v3);

                // Each 64-bit zip joins the two pairs for one Vector4. The stores emit
                // four consecutive vectors in component order 0, 1, 2, 3.
                Vector128.StoreUnsafe(Vector128_.UnpackLow(c01Low.AsDouble(), c23Low.AsDouble()).AsSingle(), ref d, (nuint)(i * 4));
                Vector128.StoreUnsafe(Vector128_.UnpackHigh(c01Low.AsDouble(), c23Low.AsDouble()).AsSingle(), ref d, (nuint)((i + 1) * 4));
                Vector128.StoreUnsafe(Vector128_.UnpackLow(c01High.AsDouble(), c23High.AsDouble()).AsSingle(), ref d, (nuint)((i + 2) * 4));
                Vector128.StoreUnsafe(Vector128_.UnpackHigh(c01High.AsDouble(), c23High.AsDouble()).AsSingle(), ref d, (nuint)((i + 3) * 4));
            }
        }

        // Fewer than four remaining pixels cannot be loaded as a full register. The tail
        // writes the identical component order, including the implicit fourth value.
        for (; i < component0.Length; i++)
        {
            destination[i] = new Vector4(component0[i], component1[i], component2[i], hasComponent3 ? component3[i] : 1F);
        }
    }

    /// <summary>
    /// Deinterleaves four-component vectors into equally sized component planes.
    /// Values are copied without changing their floating-point representation.
    /// </summary>
    /// <param name="source">The source vectors.</param>
    /// <param name="component0">The destination for <see cref="Vector4.X"/> values.</param>
    /// <param name="component1">The destination for <see cref="Vector4.Y"/> values.</param>
    /// <param name="component2">The destination for <see cref="Vector4.Z"/> values.</param>
    /// <param name="component3">The destination for <see cref="Vector4.W"/> values.</param>
    internal static void DeinterleaveFloatPlanes(
        ReadOnlySpan<Vector4> source,
        Span<float> component0,
        Span<float> component1,
        Span<float> component2,
        Span<float> component3)
    {
        Guard.IsTrue(component1.Length == component0.Length, nameof(component1), "Components must be of same size!");
        Guard.IsTrue(component2.Length == component0.Length, nameof(component2), "Components must be of same size!");
        Guard.IsTrue(component3.Length == component0.Length, nameof(component3), "Components must be of same size!");
        Guard.DestinationShouldNotBeTooShort(source, component0, nameof(component0));

        ref float s = ref Unsafe.As<Vector4, float>(ref MemoryMarshal.GetReference(source));
        ref float c0 = ref MemoryMarshal.GetReference(component0);
        ref float c1 = ref MemoryMarshal.GetReference(component1);
        ref float c2 = ref MemoryMarshal.GetReference(component2);
        ref float c3 = ref MemoryMarshal.GetReference(component3);
        int i = 0;

        if (Vector128.IsHardwareAccelerated)
        {
            // Each loaded register is one vector: p0=[c0.0,c1.0,c2.0,c3.0] through
            // p3=[c0.3,c1.3,c2.3,c3.3]. The unpack helpers make this a bitwise
            // transpose, preserving nonfinite values and signed zero exactly.
            for (; i <= source.Length - 4; i += 4)
            {
                Vector128<float> p0 = Vector128.LoadUnsafe(ref s, (nuint)(i * 4));
                Vector128<float> p1 = Vector128.LoadUnsafe(ref s, (nuint)((i + 1) * 4));
                Vector128<float> p2 = Vector128.LoadUnsafe(ref s, (nuint)((i + 2) * 4));
                Vector128<float> p3 = Vector128.LoadUnsafe(ref s, (nuint)((i + 3) * 4));

                // The first 32-bit zips pair adjacent vectors. c01Low contains
                // [c0.0,c0.1,c1.0,c1.1], and c01High contains the next two vectors.
                // c23Low and c23High hold the corresponding third and fourth components.
                Vector128<float> c01Low = Vector128_.UnpackLow(p0, p1);
                Vector128<float> c01High = Vector128_.UnpackLow(p2, p3);
                Vector128<float> c23Low = Vector128_.UnpackHigh(p0, p1);
                Vector128<float> c23High = Vector128_.UnpackHigh(p2, p3);

                // A 64-bit zip combines the matching two-component groups into
                // four values for each component plane in source vector order.
                Vector128.StoreUnsafe(Vector128_.UnpackLow(c01Low.AsDouble(), c01High.AsDouble()).AsSingle(), ref c0, (nuint)i);
                Vector128.StoreUnsafe(Vector128_.UnpackHigh(c01Low.AsDouble(), c01High.AsDouble()).AsSingle(), ref c1, (nuint)i);
                Vector128.StoreUnsafe(Vector128_.UnpackLow(c23Low.AsDouble(), c23High.AsDouble()).AsSingle(), ref c2, (nuint)i);
                Vector128.StoreUnsafe(Vector128_.UnpackHigh(c23Low.AsDouble(), c23High.AsDouble()).AsSingle(), ref c3, (nuint)i);
            }
        }

        // The scalar remainder uses the same component mapping for up to three pixels.
        for (; i < source.Length; i++)
        {
            Vector4 value = source[i];
            component0[i] = value.X;
            component1[i] = value.Y;
            component2[i] = value.Z;
            component3[i] = value.W;
        }
    }
}
