// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.PixelFormats.Utils;

/// <summary>
/// Provides shared SIMD conversion for pixel layouts containing four signed 16-bit components.
/// </summary>
internal static class SignedShort4PixelOperations
{
    private const byte RestorePackedPixelOrder = 0b_11_01_10_00;
    private const float ShortMaximum = short.MaxValue;
    private const float SignedNormalizedMagnitude = ShortMaximum * 2F;
    private const float SignedIntegerMinimum = short.MinValue;
    private const float SignedIntegerRange = ushort.MaxValue;

    /// <summary>
    /// Expands signed 16-bit components to native or scaled vectors.
    /// </summary>
    /// <param name="source">The source components. Each consecutive group of four components represents one pixel.</param>
    /// <param name="destination">The destination vectors.</param>
    /// <param name="normalized">Whether native components use signed-normalized values rather than integer values.</param>
    /// <param name="scaled">Whether to map native components to the scaled range.</param>
    internal static void ToVector4(ReadOnlySpan<short> source, Span<Vector4> destination, bool normalized, bool scaled)
    {
        ref short sourceBase = ref MemoryMarshal.GetReference(source);
        ref Vector4 destinationBase = ref MemoryMarshal.GetReference(destination);
        int index = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            int pixelsPerVector = Vector512<float>.Count / Vector128<float>.Count;

            for (; index <= destination.Length - pixelsPerVector; index += pixelsPerVector)
            {
                // Packed shorts occupy half the expanded width, so one 256-bit load feeds a complete 512-bit conversion.
                Vector256<short> packed = Vector256.LoadUnsafe(ref sourceBase, (nuint)(index * Vector128<float>.Count));
                Vector512<int> integers = Vector512.Create(Vector256.WidenLower(packed), Vector256.WidenUpper(packed));
                Unsafe.As<Vector4, Vector512<float>>(ref Unsafe.Add(ref destinationBase, (uint)index)) = ConvertToVector4(Vector512.ConvertToSingle(integers), normalized, scaled);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            int pixelsPerVector = Vector256<float>.Count / Vector128<float>.Count;

            for (; index <= destination.Length - pixelsPerVector; index += pixelsPerVector)
            {
                Vector128<short> packed = Vector128.LoadUnsafe(ref sourceBase, (nuint)(index * Vector128<float>.Count));
                Vector256<int> integers = Vector256.Create(Vector128.WidenLower(packed), Vector128.WidenUpper(packed));
                Unsafe.As<Vector4, Vector256<float>>(ref Unsafe.Add(ref destinationBase, (uint)index)) = ConvertToVector4(Vector256.ConvertToSingle(integers), normalized, scaled);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            ref byte sourceBytes = ref Unsafe.As<short, byte>(ref sourceBase);

            for (; index < destination.Length; index++)
            {
                ulong packed = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref sourceBytes, (uint)(index * sizeof(ulong))));
                Vector128<int> integers = Vector128.WidenLower(Vector128.CreateScalarUnsafe(packed).AsInt16());
                Unsafe.Add(ref destinationBase, (uint)index) = ConvertToVector4(Vector128.ConvertToSingle(integers), normalized, scaled).AsVector4();
            }

            return;
        }

        // The fallback preserves the format implementations' operation order on platforms without vector acceleration.
        for (; index < destination.Length; index++)
        {
            int componentIndex = index * Vector128<float>.Count;
            Vector4 vector = new(source[componentIndex], source[componentIndex + 1], source[componentIndex + 2], source[componentIndex + 3]);

            if (normalized)
            {
                // Both minimum two's-complement SNORM encodings represent -1.
                vector = Vector4.Max(vector, new Vector4(-ShortMaximum));

                if (scaled)
                {
                    // Offset exact integer components before division to avoid cancellation near the signed-normalized lower bound.
                    vector += new Vector4(ShortMaximum);
                    vector /= SignedNormalizedMagnitude;
                }
                else
                {
                    vector /= ShortMaximum;
                }
            }
            else if (scaled)
            {
                // SINT uses every two's-complement code, unlike SNORM where both minimum encodings represent -1.
                vector -= new Vector4(SignedIntegerMinimum);
                vector /= SignedIntegerRange;
            }

            Unsafe.Add(ref destinationBase, (uint)index) = vector;
        }
    }

    /// <summary>
    /// Packs native or scaled vectors into signed 16-bit components.
    /// </summary>
    /// <param name="source">The source vectors.</param>
    /// <param name="destination">The destination components. Each consecutive group of four components represents one pixel.</param>
    /// <param name="normalized">Whether native components use signed-normalized values rather than integer values.</param>
    /// <param name="scaled">Whether the source vectors use the scaled range.</param>
    internal static void FromVector4(Span<Vector4> source, Span<short> destination, bool normalized, bool scaled)
    {
        ref Vector4 sourceBase = ref MemoryMarshal.GetReference(source);
        ref short destinationBase = ref MemoryMarshal.GetReference(destination);
        int index = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            int pixelsPerVector = Vector512<float>.Count / Vector128<float>.Count;

            for (; index <= source.Length - pixelsPerVector; index += pixelsPerVector)
            {
                Vector512<float> vectors = Unsafe.As<Vector4, Vector512<float>>(ref Unsafe.Add(ref sourceBase, (uint)index));
                Vector512<int> integers = ConvertToInt32(vectors, normalized, scaled);

                // AVX2 packing operates independently in each 128-bit lane, producing pixels 0, 2, 1, 3. Restore the
                // source order with a native lane permutation; the portable PackSignedSaturate fallback is already ordered.
                Vector256<short> packed = Vector256_.PackSignedSaturate(integers.GetLower(), integers.GetUpper());

                if (Avx2.IsSupported)
                {
                    packed = Avx2.Permute4x64(packed.AsInt64(), RestorePackedPixelOrder).AsInt16();
                }

                Unsafe.As<short, Vector256<short>>(ref Unsafe.Add(ref destinationBase, (uint)(index * Vector128<float>.Count))) = packed;
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            int pixelsPerVector = Vector256<float>.Count / Vector128<float>.Count;

            for (; index <= source.Length - pixelsPerVector; index += pixelsPerVector)
            {
                Vector256<float> vectors = Unsafe.As<Vector4, Vector256<float>>(ref Unsafe.Add(ref sourceBase, (uint)index));
                Vector256<int> integers = ConvertToInt32(vectors, normalized, scaled);
                Vector128<short> packed = Vector128_.PackSignedSaturate(integers.GetLower(), integers.GetUpper());
                Unsafe.As<short, Vector128<short>>(ref Unsafe.Add(ref destinationBase, (uint)(index * Vector128<float>.Count))) = packed;
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            ref byte destinationBytes = ref Unsafe.As<short, byte>(ref destinationBase);

            for (; index < source.Length; index++)
            {
                Vector128<float> vector = Unsafe.As<Vector4, Vector128<float>>(ref Unsafe.Add(ref sourceBase, (uint)index));
                Vector128<int> integers = ConvertToInt32(vector, normalized, scaled);
                Vector128<short> packed = Vector128_.PackSignedSaturate(integers, integers);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref destinationBytes, (uint)(index * sizeof(ulong))), packed.AsUInt64().GetElement(0));
            }

            return;
        }

        for (; index < source.Length; index++)
        {
            Vector4 vector = Unsafe.Add(ref sourceBase, (uint)index);

            if (normalized)
            {
                if (scaled)
                {
                    vector *= 2F;
                    vector -= Vector4.One;
                }

                vector *= ShortMaximum;
                vector = Numerics.Clamp(vector, new Vector4(-ShortMaximum), new Vector4(ShortMaximum));
            }
            else
            {
                if (scaled)
                {
                    vector *= SignedIntegerRange;
                    vector += new Vector4(SignedIntegerMinimum);
                }

                vector = Numerics.Clamp(vector, new Vector4(short.MinValue), new Vector4(short.MaxValue));
            }

            int componentIndex = index * Vector128<float>.Count;
            destination[componentIndex] = (short)MathF.Round(vector.X);
            destination[componentIndex + 1] = (short)MathF.Round(vector.Y);
            destination[componentIndex + 2] = (short)MathF.Round(vector.Z);
            destination[componentIndex + 3] = (short)MathF.Round(vector.W);
        }
    }

    /// <summary>
    /// Converts signed integer components to native or scaled vectors while preserving the scalar conversion order.
    /// </summary>
    /// <param name="source">The signed integer components.</param>
    /// <param name="normalized">Whether native components use signed-normalized values.</param>
    /// <param name="scaled">Whether to map native components to the scaled range.</param>
    /// <returns>The converted vectors.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<float> ConvertToVector4(Vector512<float> source, bool normalized, bool scaled)
    {
        if (normalized)
        {
            // Both minimum two's-complement SNORM encodings represent -1.
            source = Vector512.Max(source, Vector512.Create(-ShortMaximum));

            if (scaled)
            {
                // Offset exact integer components before division to avoid cancellation near the signed-normalized lower bound.
                source += Vector512.Create(ShortMaximum);
                source /= Vector512.Create(SignedNormalizedMagnitude);
            }
            else
            {
                source /= Vector512.Create(ShortMaximum);
            }
        }
        else if (scaled)
        {
            source -= Vector512.Create(SignedIntegerMinimum);
            source /= Vector512.Create(SignedIntegerRange);
        }

        return source;
    }

    /// <summary>
    /// Converts signed integer components to native or scaled vectors while preserving the scalar conversion order.
    /// </summary>
    /// <param name="source">The signed integer components.</param>
    /// <param name="normalized">Whether native components use signed-normalized values.</param>
    /// <param name="scaled">Whether to map native components to the scaled range.</param>
    /// <returns>The converted vectors.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<float> ConvertToVector4(Vector256<float> source, bool normalized, bool scaled)
    {
        if (normalized)
        {
            // Both minimum two's-complement SNORM encodings represent -1.
            source = Vector256.Max(source, Vector256.Create(-ShortMaximum));

            if (scaled)
            {
                // Offset exact integer components before division to avoid cancellation near the signed-normalized lower bound.
                source += Vector256.Create(ShortMaximum);
                source /= Vector256.Create(SignedNormalizedMagnitude);
            }
            else
            {
                source /= Vector256.Create(ShortMaximum);
            }
        }
        else if (scaled)
        {
            source -= Vector256.Create(SignedIntegerMinimum);
            source /= Vector256.Create(SignedIntegerRange);
        }

        return source;
    }

    /// <summary>
    /// Converts signed integer components to native or scaled vectors while preserving the scalar conversion order.
    /// </summary>
    /// <param name="source">The signed integer components.</param>
    /// <param name="normalized">Whether native components use signed-normalized values.</param>
    /// <param name="scaled">Whether to map native components to the scaled range.</param>
    /// <returns>The converted vectors.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<float> ConvertToVector4(Vector128<float> source, bool normalized, bool scaled)
    {
        if (normalized)
        {
            // Both minimum two's-complement SNORM encodings represent -1.
            source = Vector128.Max(source, Vector128.Create(-ShortMaximum));

            if (scaled)
            {
                // Offset exact integer components before division to avoid cancellation near the signed-normalized lower bound.
                source += Vector128.Create(ShortMaximum);
                source /= Vector128.Create(SignedNormalizedMagnitude);
            }
            else
            {
                source /= Vector128.Create(ShortMaximum);
            }
        }
        else if (scaled)
        {
            source -= Vector128.Create(SignedIntegerMinimum);
            source /= Vector128.Create(SignedIntegerRange);
        }

        return source;
    }

    /// <summary>
    /// Converts vectors to signed integer components using the format's packing contract.
    /// </summary>
    /// <param name="source">The source vectors.</param>
    /// <param name="normalized">Whether native components use signed-normalized values.</param>
    /// <param name="scaled">Whether the source vectors use the scaled range.</param>
    /// <returns>The converted integer components.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<int> ConvertToInt32(Vector512<float> source, bool normalized, bool scaled)
    {
        if (normalized)
        {
            if (scaled)
            {
                source *= Vector512.Create(2F);
                source -= Vector512<float>.One;
            }

            source *= Vector512.Create(ShortMaximum);
            source = Vector512.Min(Vector512.Max(source, Vector512.Create(-ShortMaximum)), Vector512.Create(ShortMaximum));
        }
        else
        {
            if (scaled)
            {
                source *= Vector512.Create(SignedIntegerRange);
                source += Vector512.Create(SignedIntegerMinimum);
            }

            source = Vector512.Min(Vector512.Max(source, Vector512.Create((float)short.MinValue)), Vector512.Create((float)short.MaxValue));
        }

        return Vector512_.ConvertToInt32RoundToEven(source);
    }

    /// <summary>
    /// Converts vectors to signed integer components using the format's packing contract.
    /// </summary>
    /// <param name="source">The source vectors.</param>
    /// <param name="normalized">Whether native components use signed-normalized values.</param>
    /// <param name="scaled">Whether the source vectors use the scaled range.</param>
    /// <returns>The converted integer components.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<int> ConvertToInt32(Vector256<float> source, bool normalized, bool scaled)
    {
        if (normalized)
        {
            if (scaled)
            {
                source *= Vector256.Create(2F);
                source -= Vector256<float>.One;
            }

            source *= Vector256.Create(ShortMaximum);
            source = Vector256.Min(Vector256.Max(source, Vector256.Create(-ShortMaximum)), Vector256.Create(ShortMaximum));
        }
        else
        {
            if (scaled)
            {
                source *= Vector256.Create(SignedIntegerRange);
                source += Vector256.Create(SignedIntegerMinimum);
            }

            source = Vector256.Min(Vector256.Max(source, Vector256.Create((float)short.MinValue)), Vector256.Create((float)short.MaxValue));
        }

        return Vector256_.ConvertToInt32RoundToEven(source);
    }

    /// <summary>
    /// Converts vectors to signed integer components using the format's packing contract.
    /// </summary>
    /// <param name="source">The source vectors.</param>
    /// <param name="normalized">Whether native components use signed-normalized values.</param>
    /// <param name="scaled">Whether the source vectors use the scaled range.</param>
    /// <returns>The converted integer components.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> ConvertToInt32(Vector128<float> source, bool normalized, bool scaled)
    {
        if (normalized)
        {
            if (scaled)
            {
                source *= Vector128.Create(2F);
                source -= Vector128<float>.One;
            }

            source *= Vector128.Create(ShortMaximum);
            source = Vector128.Min(Vector128.Max(source, Vector128.Create(-ShortMaximum)), Vector128.Create(ShortMaximum));
        }
        else
        {
            if (scaled)
            {
                source *= Vector128.Create(SignedIntegerRange);
                source += Vector128.Create(SignedIntegerMinimum);
            }

            source = Vector128.Min(Vector128.Max(source, Vector128.Create((float)short.MinValue)), Vector128.Create((float)short.MaxValue));
        }

        return Vector128_.ConvertToInt32RoundToEven(source);
    }
}
