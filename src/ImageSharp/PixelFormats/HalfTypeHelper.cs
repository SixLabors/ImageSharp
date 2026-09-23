// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Helper methods for packing and unpacking floating point values
/// </summary>
internal static class HalfTypeHelper
{
    // These constants mirror the binary16 conversion used by System.Half. Keeping the vector conversion
    // bit-for-bit equivalent to the scalar runtime conversion makes SIMD a pure throughput optimization.
    private const uint HalfExponentMask = 0x7C00;
    private const uint HalfQuietNaNMask = 0x0200;
    private const uint HalfSignMask = 0x8000;
    private const uint HalfToSingleBitsMask = 0x0FFF_E000;
    private const uint SingleExponentLowerBound = 0x3880_0000;
    private const uint SingleExponentOffset = 0x3800_0000;
    private const uint SingleExponent126 = 0x3F00_0000;
    private const uint SingleBiasedExponentMask = 0x7F80_0000;
    private const uint SingleExponent13 = 0x0680_0000;
    private const uint SingleSignMask = 0x8000_0000;
    private const float MaxHalfValueBelowInfinity = 65520F;

    /// <summary>
    /// Packs a <see cref="float"/> into an <see cref="ushort"/>
    /// </summary>
    /// <param name="value">The float to pack</param>
    /// <returns>The <see cref="ushort"/></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ushort Pack(float value) => BitConverter.HalfToUInt16Bits((Half)value);

    /// <summary>
    /// Unpacks a <see cref="ushort"/> into a <see cref="float"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The <see cref="float"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float Unpack(ushort value) => (float)BitConverter.UInt16BitsToHalf(value);

    /// <summary>
    /// Unpacks eight binary16 values into two vectors of single-precision values.
    /// </summary>
    /// <param name="value">The packed binary16 values.</param>
    /// <returns>The unpacked lower and upper values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static (Vector128<float> Lower, Vector128<float> Upper) Unpack(Vector128<ushort> value)
    {
        (Vector128<uint> lower, Vector128<uint> upper) = Vector128.Widen(value);
        return (ConvertHalfBitsToSingle(lower), ConvertHalfBitsToSingle(upper));
    }

    /// <summary>
    /// Unpacks sixteen binary16 values into two vectors of single-precision values.
    /// </summary>
    /// <param name="value">The packed binary16 values.</param>
    /// <returns>The unpacked lower and upper values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static (Vector256<float> Lower, Vector256<float> Upper) Unpack(Vector256<ushort> value)
    {
        (Vector256<uint> lower, Vector256<uint> upper) = Vector256.Widen(value);
        return (ConvertHalfBitsToSingle(lower), ConvertHalfBitsToSingle(upper));
    }

    /// <summary>
    /// Unpacks thirty-two binary16 values into two vectors of single-precision values.
    /// </summary>
    /// <param name="value">The packed binary16 values.</param>
    /// <returns>The unpacked lower and upper values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static (Vector512<float> Lower, Vector512<float> Upper) Unpack(Vector512<ushort> value)
    {
        (Vector512<uint> lower, Vector512<uint> upper) = Vector512.Widen(value);
        return (ConvertHalfBitsToSingle(lower), ConvertHalfBitsToSingle(upper));
    }

    /// <summary>
    /// Packs eight single-precision values into binary16 storage.
    /// </summary>
    /// <param name="lower">The lower single-precision values.</param>
    /// <param name="upper">The upper single-precision values.</param>
    /// <returns>The packed binary16 values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector128<ushort> Pack(Vector128<float> lower, Vector128<float> upper)
        => Vector128.Narrow(ConvertSingleToHalfBits(lower), ConvertSingleToHalfBits(upper));

    /// <summary>
    /// Packs sixteen single-precision values into binary16 storage.
    /// </summary>
    /// <param name="lower">The lower single-precision values.</param>
    /// <param name="upper">The upper single-precision values.</param>
    /// <returns>The packed binary16 values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector256<ushort> Pack(Vector256<float> lower, Vector256<float> upper)
        => Vector256.Narrow(ConvertSingleToHalfBits(lower), ConvertSingleToHalfBits(upper));

    /// <summary>
    /// Packs thirty-two single-precision values into binary16 storage.
    /// </summary>
    /// <param name="lower">The lower single-precision values.</param>
    /// <param name="upper">The upper single-precision values.</param>
    /// <returns>The packed binary16 values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector512<ushort> Pack(Vector512<float> lower, Vector512<float> upper)
        => Vector512.Narrow(ConvertSingleToHalfBits(lower), ConvertSingleToHalfBits(upper));

    /// <summary>
    /// Expands a span of binary16 components into single-precision components without changing their values.
    /// </summary>
    /// <param name="source">The packed binary16 components.</param>
    /// <param name="destination">The expanded components.</param>
    internal static void Unpack(ReadOnlySpan<ushort> source, Span<float> destination)
    {
        ref ushort sourceBase = ref MemoryMarshal.GetReference(source);
        ref float destinationBase = ref MemoryMarshal.GetReference(destination);
        int i = 0;

        // Widening retains component order across each register. Full registers use
        // SIMD; the scalar tail handles any remaining components in that same order.
        if (Vector512.IsHardwareAccelerated)
        {
            nuint vectorCount = source[i..].Vector512Count<ushort>();
            for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++, i += Vector512<ushort>.Count)
            {
                (Vector512<float> lower, Vector512<float> upper) = Unpack(Vector512.LoadUnsafe(ref sourceBase, (nuint)i));
                Vector512.StoreUnsafe(lower, ref destinationBase, (nuint)i);
                Vector512.StoreUnsafe(upper, ref destinationBase, (nuint)(i + Vector512<float>.Count));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint vectorCount = source[i..].Vector256Count<ushort>();
            for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++, i += Vector256<ushort>.Count)
            {
                (Vector256<float> lower, Vector256<float> upper) = Unpack(Vector256.LoadUnsafe(ref sourceBase, (nuint)i));
                Vector256.StoreUnsafe(lower, ref destinationBase, (nuint)i);
                Vector256.StoreUnsafe(upper, ref destinationBase, (nuint)(i + Vector256<float>.Count));
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint vectorCount = source[i..].Vector128Count<ushort>();
            for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++, i += Vector128<ushort>.Count)
            {
                (Vector128<float> lower, Vector128<float> upper) = Unpack(Vector128.LoadUnsafe(ref sourceBase, (nuint)i));
                Vector128.StoreUnsafe(lower, ref destinationBase, (nuint)i);
                Vector128.StoreUnsafe(upper, ref destinationBase, (nuint)(i + Vector128<float>.Count));
            }
        }

        for (; i < source.Length; i++)
        {
            Unsafe.Add(ref destinationBase, (uint)i) = Unpack(Unsafe.Add(ref sourceBase, (uint)i));
        }
    }

    /// <summary>
    /// Narrows single-precision components into binary16 storage using the same rounding as <see cref="Half"/>.
    /// </summary>
    /// <param name="source">The single-precision components.</param>
    /// <param name="destination">The packed binary16 components.</param>
    internal static void Pack(ReadOnlySpan<float> source, Span<ushort> destination)
    {
        ref float sourceBase = ref MemoryMarshal.GetReference(source);
        ref ushort destinationBase = ref MemoryMarshal.GetReference(destination);
        ReadOnlySpan<ushort> packedDestination = destination[..source.Length];
        int i = 0;

        // Each narrowing operation consumes two float registers and writes one
        // half register, preserving the original component order and bit pattern.
        if (Vector512.IsHardwareAccelerated)
        {
            nuint vectorCount = packedDestination[i..].Vector512Count<ushort>();
            for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++, i += Vector512<ushort>.Count)
            {
                Vector512<float> lower = Vector512.LoadUnsafe(ref sourceBase, (nuint)i);
                Vector512<float> upper = Vector512.LoadUnsafe(ref sourceBase, (nuint)(i + Vector512<float>.Count));
                Vector512.StoreUnsafe(Pack(lower, upper), ref destinationBase, (nuint)i);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint vectorCount = packedDestination[i..].Vector256Count<ushort>();
            for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++, i += Vector256<ushort>.Count)
            {
                Vector256<float> lower = Vector256.LoadUnsafe(ref sourceBase, (nuint)i);
                Vector256<float> upper = Vector256.LoadUnsafe(ref sourceBase, (nuint)(i + Vector256<float>.Count));
                Vector256.StoreUnsafe(Pack(lower, upper), ref destinationBase, (nuint)i);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint vectorCount = packedDestination[i..].Vector128Count<ushort>();
            for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++, i += Vector128<ushort>.Count)
            {
                Vector128<float> lower = Vector128.LoadUnsafe(ref sourceBase, (nuint)i);
                Vector128<float> upper = Vector128.LoadUnsafe(ref sourceBase, (nuint)(i + Vector128<float>.Count));
                Vector128.StoreUnsafe(Pack(lower, upper), ref destinationBase, (nuint)i);
            }
        }

        for (; i < source.Length; i++)
        {
            Unsafe.Add(ref destinationBase, (uint)i) = Pack(Unsafe.Add(ref sourceBase, (uint)i));
        }
    }

    /// <summary>
    /// Expands four-component binary16 pixels and associates their first three components with the fourth.
    /// </summary>
    /// <param name="source">The packed components, grouped in fours.</param>
    /// <param name="destination">The expanded components.</param>
    internal static void UnpackAssociated(ReadOnlySpan<ushort> source, Span<float> destination)
    {
        ref ushort sourceBase = ref MemoryMarshal.GetReference(source);
        ref float destinationBase = ref MemoryMarshal.GetReference(destination);
        int i = 0;

        // A register contains complete four-component pixels. Replicate the fourth
        // component within each pixel, multiply the first three, then restore the fourth.
        if (Vector512.IsHardwareAccelerated)
        {
            nuint vectorCount = source[i..].Vector512Count<ushort>();
            for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++, i += Vector512<ushort>.Count)
            {
                (Vector512<float> lower, Vector512<float> upper) = Unpack(Vector512.LoadUnsafe(ref sourceBase, (nuint)i));
                Vector512.StoreUnsafe(Associate(lower), ref destinationBase, (nuint)i);
                Vector512.StoreUnsafe(Associate(upper), ref destinationBase, (nuint)(i + Vector512<float>.Count));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint vectorCount = source[i..].Vector256Count<ushort>();
            for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++, i += Vector256<ushort>.Count)
            {
                (Vector256<float> lower, Vector256<float> upper) = Unpack(Vector256.LoadUnsafe(ref sourceBase, (nuint)i));
                Vector256.StoreUnsafe(Associate(lower), ref destinationBase, (nuint)i);
                Vector256.StoreUnsafe(Associate(upper), ref destinationBase, (nuint)(i + Vector256<float>.Count));
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint vectorCount = source[i..].Vector128Count<ushort>();
            for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++, i += Vector128<ushort>.Count)
            {
                (Vector128<float> lower, Vector128<float> upper) = Unpack(Vector128.LoadUnsafe(ref sourceBase, (nuint)i));
                Vector128.StoreUnsafe(Associate(lower), ref destinationBase, (nuint)i);
                Vector128.StoreUnsafe(Associate(upper), ref destinationBase, (nuint)(i + Vector128<float>.Count));
            }
        }

        for (; i < source.Length; i += 4)
        {
            Vector4 vector = new(
                Unpack(Unsafe.Add(ref sourceBase, (uint)i)),
                Unpack(Unsafe.Add(ref sourceBase, (uint)(i + 1))),
                Unpack(Unsafe.Add(ref sourceBase, (uint)(i + 2))),
                Unpack(Unsafe.Add(ref sourceBase, (uint)(i + 3))));

            Numerics.Premultiply(ref vector);
            Unsafe.Add(ref destinationBase, (uint)i) = vector.X;
            Unsafe.Add(ref destinationBase, (uint)(i + 1)) = vector.Y;
            Unsafe.Add(ref destinationBase, (uint)(i + 2)) = vector.Z;
            Unsafe.Add(ref destinationBase, (uint)(i + 3)) = vector.W;
        }
    }

    /// <summary>
    /// Unassociates four-component vectors and packs their native values into binary16 storage.
    /// </summary>
    /// <param name="source">The associated components, grouped in fours.</param>
    /// <param name="destination">The packed components.</param>
    internal static void PackFromAssociated(ReadOnlySpan<float> source, Span<ushort> destination)
    {
        ref float sourceBase = ref MemoryMarshal.GetReference(source);
        ref ushort destinationBase = ref MemoryMarshal.GetReference(destination);
        ReadOnlySpan<ushort> packedDestination = destination[..source.Length];
        int i = 0;

        // The fourth component is replicated before division. UnPremultiply restores
        // that component and preserves the first three when it is zero.
        if (Vector512.IsHardwareAccelerated)
        {
            nuint vectorCount = packedDestination[i..].Vector512Count<ushort>();
            for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++, i += Vector512<ushort>.Count)
            {
                Vector512<float> lower = Unassociate(Vector512.LoadUnsafe(ref sourceBase, (nuint)i));
                Vector512<float> upper = Unassociate(Vector512.LoadUnsafe(ref sourceBase, (nuint)(i + Vector512<float>.Count)));
                Vector512.StoreUnsafe(Pack(lower, upper), ref destinationBase, (nuint)i);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint vectorCount = packedDestination[i..].Vector256Count<ushort>();
            for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++, i += Vector256<ushort>.Count)
            {
                Vector256<float> lower = Unassociate(Vector256.LoadUnsafe(ref sourceBase, (nuint)i));
                Vector256<float> upper = Unassociate(Vector256.LoadUnsafe(ref sourceBase, (nuint)(i + Vector256<float>.Count)));
                Vector256.StoreUnsafe(Pack(lower, upper), ref destinationBase, (nuint)i);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint vectorCount = packedDestination[i..].Vector128Count<ushort>();
            for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++, i += Vector128<ushort>.Count)
            {
                Vector128<float> lower = Unassociate(Vector128.LoadUnsafe(ref sourceBase, (nuint)i));
                Vector128<float> upper = Unassociate(Vector128.LoadUnsafe(ref sourceBase, (nuint)(i + Vector128<float>.Count)));
                Vector128.StoreUnsafe(Pack(lower, upper), ref destinationBase, (nuint)i);
            }
        }

        for (; i < source.Length; i += 4)
        {
            Vector4 vector = new(
                Unsafe.Add(ref sourceBase, (uint)i),
                Unsafe.Add(ref sourceBase, (uint)(i + 1)),
                Unsafe.Add(ref sourceBase, (uint)(i + 2)),
                Unsafe.Add(ref sourceBase, (uint)(i + 3)));

            Numerics.UnPremultiply(ref vector);
            Unsafe.Add(ref destinationBase, (uint)i) = Pack(vector.X);
            Unsafe.Add(ref destinationBase, (uint)(i + 1)) = Pack(vector.Y);
            Unsafe.Add(ref destinationBase, (uint)(i + 2)) = Pack(vector.Z);
            Unsafe.Add(ref destinationBase, (uint)(i + 3)) = Pack(vector.W);
        }
    }

    /// <summary>
    /// Expands associated four-component binary16 pixels and unassociates their first three components.
    /// </summary>
    /// <param name="source">The packed components, grouped in fours.</param>
    /// <param name="destination">The expanded components.</param>
    internal static void UnpackUnassociated(ReadOnlySpan<ushort> source, Span<float> destination)
    {
        ref ushort sourceBase = ref MemoryMarshal.GetReference(source);
        ref float destinationBase = ref MemoryMarshal.GetReference(destination);
        int i = 0;

        // Each widened register contains complete four-component pixels, so every
        // alpha stays with its own color components during unassociation.
        if (Vector512.IsHardwareAccelerated)
        {
            nuint vectorCount = source[i..].Vector512Count<ushort>();
            for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++)
            {
                (Vector512<float> lower, Vector512<float> upper) = Unpack(Vector512.LoadUnsafe(ref sourceBase, (nuint)i));
                Vector512.StoreUnsafe(Unassociate(lower), ref destinationBase, (nuint)i);
                Vector512.StoreUnsafe(Unassociate(upper), ref destinationBase, (nuint)(i + Vector512<float>.Count));
                i += Vector512<ushort>.Count;
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint vectorCount = source[i..].Vector256Count<ushort>();
            for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++)
            {
                (Vector256<float> lower, Vector256<float> upper) = Unpack(Vector256.LoadUnsafe(ref sourceBase, (nuint)i));
                Vector256.StoreUnsafe(Unassociate(lower), ref destinationBase, (nuint)i);
                Vector256.StoreUnsafe(Unassociate(upper), ref destinationBase, (nuint)(i + Vector256<float>.Count));
                i += Vector256<ushort>.Count;
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint vectorCount = source[i..].Vector128Count<ushort>();
            for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++)
            {
                (Vector128<float> lower, Vector128<float> upper) = Unpack(Vector128.LoadUnsafe(ref sourceBase, (nuint)i));
                Vector128.StoreUnsafe(Unassociate(lower), ref destinationBase, (nuint)i);
                Vector128.StoreUnsafe(Unassociate(upper), ref destinationBase, (nuint)(i + Vector128<float>.Count));
                i += Vector128<ushort>.Count;
            }
        }

        // The pixel operations own the four-component invariant. Only whole pixels
        // reach this tail, including a single pixel after the 128-bit path.
        for (; i < source.Length; i += 4)
        {
            Vector4 vector = new(
                Unpack(Unsafe.Add(ref sourceBase, (uint)i)),
                Unpack(Unsafe.Add(ref sourceBase, (uint)(i + 1))),
                Unpack(Unsafe.Add(ref sourceBase, (uint)(i + 2))),
                Unpack(Unsafe.Add(ref sourceBase, (uint)(i + 3))));

            Numerics.UnPremultiply(ref vector);
            Unsafe.Add(ref destinationBase, (uint)i) = vector.X;
            Unsafe.Add(ref destinationBase, (uint)(i + 1)) = vector.Y;
            Unsafe.Add(ref destinationBase, (uint)(i + 2)) = vector.Z;
            Unsafe.Add(ref destinationBase, (uint)(i + 3)) = vector.W;
        }
    }

    /// <summary>
    /// Associates four-component vectors with their stored binary16 alpha and packs them.
    /// </summary>
    /// <param name="source">The unassociated components, grouped in fours.</param>
    /// <param name="destination">The packed components.</param>
    internal static void PackAssociated(ReadOnlySpan<float> source, Span<ushort> destination)
    {
        ref float sourceBase = ref MemoryMarshal.GetReference(source);
        ref ushort destinationBase = ref MemoryMarshal.GetReference(destination);
        ReadOnlySpan<ushort> packedDestination = destination[..source.Length];
        int i = 0;

        // Round alpha before multiplication so all SIMD widths associate color
        // with the exact alpha that the destination will store.
        if (Vector512.IsHardwareAccelerated)
        {
            nuint vectorCount = packedDestination[i..].Vector512Count<ushort>();
            for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++)
            {
                Vector512<float> lower = AssociateForStorage(Vector512.LoadUnsafe(ref sourceBase, (nuint)i));
                Vector512<float> upper = AssociateForStorage(Vector512.LoadUnsafe(ref sourceBase, (nuint)(i + Vector512<float>.Count)));
                Vector512.StoreUnsafe(Pack(lower, upper), ref destinationBase, (nuint)i);
                i += Vector512<ushort>.Count;
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint vectorCount = packedDestination[i..].Vector256Count<ushort>();
            for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++)
            {
                Vector256<float> lower = AssociateForStorage(Vector256.LoadUnsafe(ref sourceBase, (nuint)i));
                Vector256<float> upper = AssociateForStorage(Vector256.LoadUnsafe(ref sourceBase, (nuint)(i + Vector256<float>.Count)));
                Vector256.StoreUnsafe(Pack(lower, upper), ref destinationBase, (nuint)i);
                i += Vector256<ushort>.Count;
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint vectorCount = packedDestination[i..].Vector128Count<ushort>();
            for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++)
            {
                Vector128<float> lower = AssociateForStorage(Vector128.LoadUnsafe(ref sourceBase, (nuint)i));
                Vector128<float> upper = AssociateForStorage(Vector128.LoadUnsafe(ref sourceBase, (nuint)(i + Vector128<float>.Count)));
                Vector128.StoreUnsafe(Pack(lower, upper), ref destinationBase, (nuint)i);
                i += Vector128<ushort>.Count;
            }
        }

        for (; i < source.Length; i += 4)
        {
            Vector4 vector = new(
                Unsafe.Add(ref sourceBase, (uint)i),
                Unsafe.Add(ref sourceBase, (uint)(i + 1)),
                Unsafe.Add(ref sourceBase, (uint)(i + 2)),
                Unsafe.Add(ref sourceBase, (uint)(i + 3)));

            vector.W = Unpack(Pack(vector.W));
            Numerics.Premultiply(ref vector);
            Unsafe.Add(ref destinationBase, (uint)i) = Pack(vector.X);
            Unsafe.Add(ref destinationBase, (uint)(i + 1)) = Pack(vector.Y);
            Unsafe.Add(ref destinationBase, (uint)(i + 2)) = Pack(vector.Z);
            Unsafe.Add(ref destinationBase, (uint)(i + 3)) = Pack(vector.W);
        }
    }

    /// <summary>
    /// Associates complete four-component vectors while preserving their fourth component.
    /// </summary>
    /// <param name="source">The unassociated components.</param>
    /// <returns>The associated components.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<float> Associate(Vector128<float> source)
    {
        Vector128<float> alpha = Vector128_.ShuffleNative(source, 0b_11_11_11_11);
        return Vector128.ConditionalSelect(Vector128.Create(0, 0, 0, -1).AsSingle(), alpha, source * alpha);
    }

    /// <inheritdoc cref="Associate(Vector128{float})" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<float> Associate(Vector256<float> source)
    {
        Vector256<float> alpha = Vector256_.ShuffleNative(source, 0b_11_11_11_11);
        return Vector256.ConditionalSelect(Vector256.Create(0, 0, 0, -1, 0, 0, 0, -1).AsSingle(), alpha, source * alpha);
    }

    /// <inheritdoc cref="Associate(Vector128{float})" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<float> Associate(Vector512<float> source)
    {
        Vector512<float> alpha = Vector512_.ShuffleNative(source, 0b_11_11_11_11);
        Vector512<float> alphaMask = Vector512.Create(0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1).AsSingle();
        return Vector512.ConditionalSelect(alphaMask, alpha, source * alpha);
    }

    /// <summary>
    /// Associates the first three components with alpha rounded to binary16 storage.
    /// </summary>
    /// <param name="source">The unassociated components.</param>
    /// <returns>The associated components with their stored alpha.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<float> AssociateForStorage(Vector128<float> source)
    {
        Vector128<float> alpha = RoundToHalf(Vector128_.ShuffleNative(source, 0b_11_11_11_11));
        return Vector128.ConditionalSelect(Vector128.Create(0, 0, 0, -1).AsSingle(), alpha, source * alpha);
    }

    /// <inheritdoc cref="AssociateForStorage(Vector128{float})" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<float> AssociateForStorage(Vector256<float> source)
    {
        Vector256<float> alpha = RoundToHalf(Vector256_.ShuffleNative(source, 0b_11_11_11_11));
        return Vector256.ConditionalSelect(Vector256.Create(0, 0, 0, -1, 0, 0, 0, -1).AsSingle(), alpha, source * alpha);
    }

    /// <inheritdoc cref="AssociateForStorage(Vector128{float})" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<float> AssociateForStorage(Vector512<float> source)
    {
        Vector512<float> alpha = RoundToHalf(Vector512_.ShuffleNative(source, 0b_11_11_11_11));
        Vector512<float> alphaMask = Vector512.Create(0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1).AsSingle();
        return Vector512.ConditionalSelect(alphaMask, alpha, source * alpha);
    }

    /// <summary>
    /// Unassociates complete four-component vectors while preserving their fourth component.
    /// </summary>
    /// <param name="source">The associated components.</param>
    /// <returns>The unassociated components.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<float> Unassociate(Vector128<float> source)
        => Numerics.UnPremultiply(source, Vector128_.ShuffleNative(source, 0b_11_11_11_11));

    /// <inheritdoc cref="Unassociate(Vector128{float})" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<float> Unassociate(Vector256<float> source)
        => Numerics.UnPremultiply(source, Vector256_.ShuffleNative(source, 0b_11_11_11_11));

    /// <inheritdoc cref="Unassociate(Vector128{float})" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<float> Unassociate(Vector512<float> source)
        => Numerics.UnPremultiply(source, Vector512_.ShuffleNative(source, 0b_11_11_11_11));

    /// <summary>
    /// Rounds single-precision values through binary16 without changing the vector width.
    /// </summary>
    /// <param name="value">The single-precision values.</param>
    /// <returns>The values after binary16 quantization.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector128<float> RoundToHalf(Vector128<float> value)
        => ConvertHalfBitsToSingle(ConvertSingleToHalfBits(value));

    /// <summary>
    /// Rounds single-precision values through binary16 without changing the vector width.
    /// </summary>
    /// <param name="value">The single-precision values.</param>
    /// <returns>The values after binary16 quantization.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector256<float> RoundToHalf(Vector256<float> value)
        => ConvertHalfBitsToSingle(ConvertSingleToHalfBits(value));

    /// <summary>
    /// Rounds single-precision values through binary16 without changing the vector width.
    /// </summary>
    /// <param name="value">The single-precision values.</param>
    /// <returns>The values after binary16 quantization.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector512<float> RoundToHalf(Vector512<float> value)
        => ConvertHalfBitsToSingle(ConvertSingleToHalfBits(value));

    /// <summary>
    /// Converts zero-extended binary16 bit patterns to single-precision values.
    /// </summary>
    /// <param name="value">The binary16 bit patterns.</param>
    /// <returns>The converted single-precision values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<float> ConvertHalfBitsToSingle(Vector128<uint> value)
    {
        Vector128<uint> sign = Vector128.ShiftLeft(value & Vector128.Create(HalfSignMask), 16);
        Vector128<uint> exponent = value & Vector128.Create(HalfExponentMask);
        Vector128<uint> subnormalMask = Vector128.Equals(exponent, Vector128<uint>.Zero);
        Vector128<uint> infinityOrNaNMask = Vector128.Equals(exponent, Vector128.Create(HalfExponentMask));
        Vector128<uint> maskedExponentLowerBound = subnormalMask & Vector128.Create(SingleExponentLowerBound);
        Vector128<uint> exponentOffset = Vector128.Create(SingleExponentOffset) | maskedExponentLowerBound;

        // Binary16 and binary32 fraction fields differ by thirteen bits. Subnormals and special values
        // need different exponent offsets before that shared field layout can be reinterpreted as float.
        Vector128<uint> bits = Vector128.ShiftLeft(value, 13) & Vector128.Create(HalfToSingleBitsMask);
        exponentOffset = Vector128.ConditionalSelect(infinityOrNaNMask, Vector128.ShiftLeft(exponentOffset, 1), exponentOffset);
        bits += exponentOffset;
        Vector128<uint> absoluteValue = (bits.AsSingle() - maskedExponentLowerBound.AsSingle()).AsUInt32();
        return (absoluteValue | sign).AsSingle();
    }

    /// <summary>
    /// Converts zero-extended binary16 bit patterns to single-precision values.
    /// </summary>
    /// <param name="value">The binary16 bit patterns.</param>
    /// <returns>The converted single-precision values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<float> ConvertHalfBitsToSingle(Vector256<uint> value)
    {
        Vector256<uint> sign = Vector256.ShiftLeft(value & Vector256.Create(HalfSignMask), 16);
        Vector256<uint> exponent = value & Vector256.Create(HalfExponentMask);
        Vector256<uint> subnormalMask = Vector256.Equals(exponent, Vector256<uint>.Zero);
        Vector256<uint> infinityOrNaNMask = Vector256.Equals(exponent, Vector256.Create(HalfExponentMask));
        Vector256<uint> maskedExponentLowerBound = subnormalMask & Vector256.Create(SingleExponentLowerBound);
        Vector256<uint> exponentOffset = Vector256.Create(SingleExponentOffset) | maskedExponentLowerBound;

        // Binary16 and binary32 fraction fields differ by thirteen bits. Subnormals and special values
        // need different exponent offsets before that shared field layout can be reinterpreted as float.
        Vector256<uint> bits = Vector256.ShiftLeft(value, 13) & Vector256.Create(HalfToSingleBitsMask);
        exponentOffset = Vector256.ConditionalSelect(infinityOrNaNMask, Vector256.ShiftLeft(exponentOffset, 1), exponentOffset);
        bits += exponentOffset;
        Vector256<uint> absoluteValue = (bits.AsSingle() - maskedExponentLowerBound.AsSingle()).AsUInt32();
        return (absoluteValue | sign).AsSingle();
    }

    /// <summary>
    /// Converts zero-extended binary16 bit patterns to single-precision values.
    /// </summary>
    /// <param name="value">The binary16 bit patterns.</param>
    /// <returns>The converted single-precision values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<float> ConvertHalfBitsToSingle(Vector512<uint> value)
    {
        Vector512<uint> sign = Vector512.ShiftLeft(value & Vector512.Create(HalfSignMask), 16);
        Vector512<uint> exponent = value & Vector512.Create(HalfExponentMask);
        Vector512<uint> subnormalMask = Vector512.Equals(exponent, Vector512<uint>.Zero);
        Vector512<uint> infinityOrNaNMask = Vector512.Equals(exponent, Vector512.Create(HalfExponentMask));
        Vector512<uint> maskedExponentLowerBound = subnormalMask & Vector512.Create(SingleExponentLowerBound);
        Vector512<uint> exponentOffset = Vector512.Create(SingleExponentOffset) | maskedExponentLowerBound;

        // Binary16 and binary32 fraction fields differ by thirteen bits. Subnormals and special values
        // need different exponent offsets before that shared field layout can be reinterpreted as float.
        Vector512<uint> bits = Vector512.ShiftLeft(value, 13) & Vector512.Create(HalfToSingleBitsMask);
        exponentOffset = Vector512.ConditionalSelect(infinityOrNaNMask, Vector512.ShiftLeft(exponentOffset, 1), exponentOffset);
        bits += exponentOffset;
        Vector512<uint> absoluteValue = (bits.AsSingle() - maskedExponentLowerBound.AsSingle()).AsUInt32();
        return (absoluteValue | sign).AsSingle();
    }

    /// <summary>
    /// Converts single-precision values to zero-extended binary16 bit patterns.
    /// </summary>
    /// <param name="value">The single-precision values.</param>
    /// <returns>The binary16 bit patterns in 32-bit lanes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> ConvertSingleToHalfBits(Vector128<float> value)
    {
        Vector128<uint> bits = value.AsUInt32();
        Vector128<uint> sign = Vector128.ShiftRightLogical(bits & Vector128.Create(SingleSignMask), 16);
        Vector128<uint> realMask = Vector128.Equals(value, value).AsUInt32();
        value = Vector128.Abs(value);
        value = Vector128.Min(Vector128.Create(MaxHalfValueBelowInfinity), value);
        Vector128<uint> exponentOffset = Vector128.Max(value, Vector128.Create(SingleExponentLowerBound).AsSingle()).AsUInt32();
        exponentOffset &= Vector128.Create(SingleBiasedExponentMask);
        exponentOffset += Vector128.Create(SingleExponent13);

        // Adding an exponent-sized float rounds the significand to binary16 precision using IEEE
        // round-to-nearest-even. The remaining integer operations realign the exponent and sign fields.
        value += exponentOffset.AsSingle();
        bits = value.AsUInt32() - Vector128.Create(SingleExponent126);
        Vector128<uint> newExponent = Vector128.ShiftRightLogical(bits, 13);

        // A NaN needs a nonzero fraction; an all-ones exponent alone encodes infinity.
        Vector128<uint> maskedHalfExponentForNaN = ~realMask & Vector128.Create(HalfExponentMask | HalfQuietNaNMask);
        bits &= realMask;
        bits += newExponent;
        bits &= ~maskedHalfExponentForNaN;
        return bits | maskedHalfExponentForNaN | sign;
    }

    /// <summary>
    /// Converts single-precision values to zero-extended binary16 bit patterns.
    /// </summary>
    /// <param name="value">The single-precision values.</param>
    /// <returns>The binary16 bit patterns in 32-bit lanes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<uint> ConvertSingleToHalfBits(Vector256<float> value)
    {
        Vector256<uint> bits = value.AsUInt32();
        Vector256<uint> sign = Vector256.ShiftRightLogical(bits & Vector256.Create(SingleSignMask), 16);
        Vector256<uint> realMask = Vector256.Equals(value, value).AsUInt32();
        value = Vector256.Abs(value);
        value = Vector256.Min(Vector256.Create(MaxHalfValueBelowInfinity), value);
        Vector256<uint> exponentOffset = Vector256.Max(value, Vector256.Create(SingleExponentLowerBound).AsSingle()).AsUInt32();
        exponentOffset &= Vector256.Create(SingleBiasedExponentMask);
        exponentOffset += Vector256.Create(SingleExponent13);

        // Adding an exponent-sized float rounds the significand to binary16 precision using IEEE
        // round-to-nearest-even. The remaining integer operations realign the exponent and sign fields.
        value += exponentOffset.AsSingle();
        bits = value.AsUInt32() - Vector256.Create(SingleExponent126);
        Vector256<uint> newExponent = Vector256.ShiftRightLogical(bits, 13);

        // A NaN needs a nonzero fraction; an all-ones exponent alone encodes infinity.
        Vector256<uint> maskedHalfExponentForNaN = ~realMask & Vector256.Create(HalfExponentMask | HalfQuietNaNMask);
        bits &= realMask;
        bits += newExponent;
        bits &= ~maskedHalfExponentForNaN;
        return bits | maskedHalfExponentForNaN | sign;
    }

    /// <summary>
    /// Converts single-precision values to zero-extended binary16 bit patterns.
    /// </summary>
    /// <param name="value">The single-precision values.</param>
    /// <returns>The binary16 bit patterns in 32-bit lanes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<uint> ConvertSingleToHalfBits(Vector512<float> value)
    {
        Vector512<uint> bits = value.AsUInt32();
        Vector512<uint> sign = Vector512.ShiftRightLogical(bits & Vector512.Create(SingleSignMask), 16);
        Vector512<uint> realMask = Vector512.Equals(value, value).AsUInt32();
        value = Vector512.Abs(value);
        value = Vector512.Min(Vector512.Create(MaxHalfValueBelowInfinity), value);
        Vector512<uint> exponentOffset = Vector512.Max(value, Vector512.Create(SingleExponentLowerBound).AsSingle()).AsUInt32();
        exponentOffset &= Vector512.Create(SingleBiasedExponentMask);
        exponentOffset += Vector512.Create(SingleExponent13);

        // Adding an exponent-sized float rounds the significand to binary16 precision using IEEE
        // round-to-nearest-even. The remaining integer operations realign the exponent and sign fields.
        value += exponentOffset.AsSingle();
        bits = value.AsUInt32() - Vector512.Create(SingleExponent126);
        Vector512<uint> newExponent = Vector512.ShiftRightLogical(bits, 13);

        // A NaN needs a nonzero fraction; an all-ones exponent alone encodes infinity.
        Vector512<uint> maskedHalfExponentForNaN = ~realMask & Vector512.Create(HalfExponentMask | HalfQuietNaNMask);
        bits &= realMask;
        bits += newExponent;
        bits &= ~maskedHalfExponentForNaN;
        return bits | maskedHalfExponentForNaN | sign;
    }
}
