// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Shares native and normalized bulk conversions for signed 16-bit pixel storage.
/// </summary>
/// <remarks>
/// Two- and four-component layouts share widening and saturating packing at every SIMD width.
/// SNORM reserves both minimum encodings for -1. Keep its offset-before-division order identical to the scalar pixel API.
/// Association uses normalized color coordinates; native signed values are mapped before or after that arithmetic.
/// </remarks>
/// <typeparam name="TPixel">The signed-short pixel format.</typeparam>
internal abstract class ShortPixelOperations<TPixel> : PixelOperations<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private const byte RestorePackedPixelOrder = 0b_11_01_10_00;
    private const float ShortMaximum = short.MaxValue;
    private const float SignedNormalizedMagnitude = ShortMaximum * 2F;
    private const float SignedIntegerMinimum = short.MinValue;
    private const float SignedIntegerRange = ushort.MaxValue;

    private readonly Vector4 nativeOffset;
    private readonly Vector4 nativeMagnitude;
    private readonly bool normalized;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShortPixelOperations{TPixel}"/> class.
    /// </summary>
    /// <param name="normalized">Whether native components use signed-normalized units.</param>
    public ShortPixelOperations(bool normalized)
    {
        this.normalized = normalized;

        // Native signed integers and SNORM use different affine maps. Missing B and A stay at their vector defaults.
        float offset = normalized ? 1F : -(float)short.MinValue;
        float magnitude = normalized ? 2F : ushort.MaxValue;
        int componentCount = TPixel.GetPixelTypeInfo().ComponentInfo!.Value.ComponentCount;
        this.nativeOffset = componentCount == 4 ? new Vector4(offset) : new Vector4(offset, offset, 0F, 0F);
        this.nativeMagnitude = componentCount == 4 ? new Vector4(magnitude) : new Vector4(magnitude, magnitude, 1F, 1F);
    }

    /// <inheritdoc />
    public override void ToUnassociatedVector4(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination)
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        Unpack(MemoryMarshal.Cast<TPixel, short>(source), destination[..source.Length], this.normalized, false);
    }

    /// <inheritdoc />
    public override void ToAssociatedVector4(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination)
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        destination = destination[..source.Length];

        // Signed formats map their stored RGB and alpha ranges to [0,1] for association.
        // SNORM stores these components in [-1,1]. Signed integer storage uses [-32768,32767], including for alpha.
        // Multiply RGB by that opacity, then undo the range map for each stored component, including alpha.
        bool hasAlpha = TPixel.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Unassociated;
        Unpack(MemoryMarshal.Cast<TPixel, short>(source), destination, this.normalized, hasAlpha);

        // RG pixels have implicit alpha one. Keep their native components unchanged instead of normalizing and restoring them.
        if (hasAlpha)
        {
            Numerics.Premultiply(destination);
            PixelFormats.PixelOperations.MultiplyThenAdd(destination, this.nativeMagnitude, -this.nativeOffset);
        }
    }

    /// <inheritdoc />
    public override void ToUnassociatedScaledVector4(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination)
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        Unpack(MemoryMarshal.Cast<TPixel, short>(source), destination[..source.Length], this.normalized, true);
    }

    /// <inheritdoc />
    public override void ToAssociatedScaledVector4(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination)
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        destination = destination[..source.Length];
        Unpack(MemoryMarshal.Cast<TPixel, short>(source), destination, this.normalized, true);

        if (TPixel.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Unassociated)
        {
            Numerics.Premultiply(destination);
        }
    }

    /// <inheritdoc />
    public override void FromUnassociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<TPixel> destination)
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        destination = destination[..source.Length];
        Pack(source, MemoryMarshal.Cast<TPixel, short>(destination), this.normalized, false);
    }

    /// <inheritdoc />
    public override void FromAssociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<TPixel> destination)
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        // Map the stored signed components before unassociation. Two-component formats leave their implicit B and alpha unchanged.
        PixelFormats.PixelOperations.AddThenDivide(source, this.nativeOffset, this.nativeMagnitude);
        this.FromAssociatedScaledVector4Destructive(configuration, source, destination);
    }

    /// <inheritdoc />
    public override void FromUnassociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<TPixel> destination)
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        destination = destination[..source.Length];
        Pack(source, MemoryMarshal.Cast<TPixel, short>(destination), this.normalized, true);
    }

    /// <inheritdoc />
    public override void FromAssociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<TPixel> destination)
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        destination = destination[..source.Length];
        PixelFormats.PixelOperations.UnPremultiplyForPacking(source);
        Pack(source, MemoryMarshal.Cast<TPixel, short>(destination), this.normalized, true);
    }

    /// <summary>
    /// Expands signed 16-bit components to native or scaled vectors.
    /// </summary>
    /// <param name="source">The stored RG or RGBA components.</param>
    /// <param name="destination">The destination vectors.</param>
    /// <param name="normalized">Whether native components use signed-normalized values rather than integer values.</param>
    /// <param name="scaled">Whether to map native components to the scaled range.</param>
    private static void Unpack(ReadOnlySpan<short> source, Span<Vector4> destination, bool normalized, bool scaled)
    {
        ref short sourceBase = ref MemoryMarshal.GetReference(source);
        ref ushort sourceBits = ref Unsafe.As<short, ushort>(ref sourceBase);
        ref Vector4 destinationBase = ref MemoryMarshal.GetReference(destination);
        int componentCount = TPixel.GetPixelTypeInfo().ComponentInfo!.Value.ComponentCount;
        int index = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            int pixelsPerVector = Vector512<float>.Count / Vector128<float>.Count;

            for (; index <= destination.Length - pixelsPerVector; index += pixelsPerVector)
            {
                // Insert absent B/A slots before signed widening. Restore their defaults after the numeric map:
                // native zero does not map to scaled zero in a signed format.
                PixelFormats.PixelOperations.Load16BitComponents(ref sourceBits, (nuint)(index * componentCount), componentCount, out Vector256<ushort> bits);

                // Reinterpret the stored bits before widening so negative components retain their sign.
                // Sixteen shorts become sixteen ints, then sixteen floats: four consecutive RGBA groups.
                Vector256<short> packed = bits.AsInt16();
                Vector512<int> integers = Vector512.Create(Vector256.WidenLower(packed), Vector256.WidenUpper(packed));
                Vector512<float> converted = ConvertToVector4(Vector512.ConvertToSingle(integers), normalized, scaled);
                Unsafe.As<Vector4, Vector512<float>>(ref Unsafe.Add(ref destinationBase, (uint)index)) =
                    PixelFormats.PixelOperations.RestoreImplicitComponents(converted, componentCount);
            }
        }

        // The index counts pixels. Continue with complete groups of two, then one; no load needs row padding.
        if (Vector256.IsHardwareAccelerated)
        {
            int pixelsPerVector = Vector256<float>.Count / Vector128<float>.Count;

            for (; index <= destination.Length - pixelsPerVector; index += pixelsPerVector)
            {
                PixelFormats.PixelOperations.Load16BitComponents(ref sourceBits, (nuint)(index * componentCount), componentCount, out Vector128<ushort> bits);
                Vector128<short> packed = bits.AsInt16();
                Vector256<int> integers = Vector256.Create(Vector128.WidenLower(packed), Vector128.WidenUpper(packed));
                Vector256<float> converted = ConvertToVector4(Vector256.ConvertToSingle(integers), normalized, scaled);
                Unsafe.As<Vector4, Vector256<float>>(ref Unsafe.Add(ref destinationBase, (uint)index)) =
                    PixelFormats.PixelOperations.RestoreImplicitComponents(converted, componentCount);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            ref byte sourceBytes = ref Unsafe.As<short, byte>(ref sourceBase);

            for (; index < destination.Length; index++)
            {
                // Read exactly one stored pixel, including the final pixel: RG needs four bytes, RGBA needs eight.
                ref byte start = ref Unsafe.Add(ref sourceBytes, (uint)(index * Unsafe.SizeOf<TPixel>()));
                ulong packed = componentCount == 4 ? Unsafe.ReadUnaligned<ulong>(ref start) : Unsafe.ReadUnaligned<uint>(ref start);
                Vector128<int> integers = Vector128.WidenLower(Vector128.CreateScalarUnsafe(packed).AsInt16());
                Vector128<float> converted = ConvertToVector4(Vector128.ConvertToSingle(integers), normalized, scaled);
                Unsafe.Add(ref destinationBase, (uint)index) = PixelFormats.PixelOperations.RestoreImplicitComponents(converted, componentCount).AsVector4();
            }

            return;
        }

        // The fallback preserves the format implementations' operation order on platforms without vector acceleration.
        for (; index < destination.Length; index++)
        {
            int componentIndex = index * componentCount;
            Vector4 vector = new(
                source[componentIndex],
                source[componentIndex + 1],
                componentCount == 4 ? source[componentIndex + 2] : 0F,
                componentCount == 4 ? source[componentIndex + 3] : 1F);

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

            Unsafe.Add(ref destinationBase, (uint)index) = componentCount == 4 ? vector : new Vector4(vector.X, vector.Y, 0F, 1F);
        }
    }

    /// <summary>
    /// Packs native or scaled vectors into signed 16-bit components.
    /// </summary>
    /// <param name="source">The source vectors.</param>
    /// <param name="destination">The destination RG or RGBA components.</param>
    /// <param name="normalized">Whether native components use signed-normalized values rather than integer values.</param>
    /// <param name="scaled">Whether the source vectors use the scaled range.</param>
    private static void Pack(Span<Vector4> source, Span<short> destination, bool normalized, bool scaled)
    {
        ref Vector4 sourceBase = ref MemoryMarshal.GetReference(source);
        ref short destinationBase = ref MemoryMarshal.GetReference(destination);
        ref ushort destinationBits = ref Unsafe.As<short, ushort>(ref destinationBase);
        int componentCount = TPixel.GetPixelTypeInfo().ComponentInfo!.Value.ComponentCount;
        int index = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            int pixelsPerVector = Vector512<float>.Count / Vector128<float>.Count;

            for (; index <= source.Length - pixelsPerVector; index += pixelsPerVector)
            {
                Vector512<float> vectors = Unsafe.As<Vector4, Vector512<float>>(ref Unsafe.Add(ref sourceBase, (uint)index));

                // Convert and round all four components before compacting RG storage. Missing components need no output words.
                Vector512<int> integers = ConvertToInt32(vectors, normalized, scaled);

                // AVX2 packing operates independently in each 128-bit lane, producing pixels 0, 2, 1, 3. Restore the
                // source order with a native lane permutation; the portable PackSignedSaturate fallback is already ordered.
                Vector256<short> packed = Vector256_.PackSignedSaturate(integers.GetLower(), integers.GetUpper());

                if (Avx2.IsSupported)
                {
                    packed = Avx2.Permute4x64(packed.AsInt64(), RestorePackedPixelOrder).AsInt16();
                }

                PixelFormats.PixelOperations.Store16BitComponents(packed.AsUInt16(), ref destinationBits, (nuint)(index * componentCount), componentCount);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            int pixelsPerVector = Vector256<float>.Count / Vector128<float>.Count;

            for (; index <= source.Length - pixelsPerVector; index += pixelsPerVector)
            {
                Vector256<float> vectors = Unsafe.As<Vector4, Vector256<float>>(ref Unsafe.Add(ref sourceBase, (uint)index));
                Vector256<int> integers = ConvertToInt32(vectors, normalized, scaled);
                Vector128<short> packed = Vector128.NarrowWithSaturation(integers.GetLower(), integers.GetUpper());
                PixelFormats.PixelOperations.Store16BitComponents(packed.AsUInt16(), ref destinationBits, (nuint)(index * componentCount), componentCount);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            ref byte destinationBytes = ref Unsafe.As<short, byte>(ref destinationBase);

            for (; index < source.Length; index++)
            {
                Vector128<float> vector = Unsafe.As<Vector4, Vector128<float>>(ref Unsafe.Add(ref sourceBase, (uint)index));
                Vector128<int> integers = ConvertToInt32(vector, normalized, scaled);
                Vector128<short> packed = Vector128.NarrowWithSaturation(integers, integers);

                // This iteration owns one pixel. Store only its R/G words for RG storage.
                ref byte start = ref Unsafe.Add(ref destinationBytes, (uint)(index * Unsafe.SizeOf<TPixel>()));
                if (componentCount == 4)
                {
                    Unsafe.WriteUnaligned(ref start, packed.AsUInt64().ToScalar());
                }
                else
                {
                    Unsafe.WriteUnaligned(ref start, packed.AsUInt32().ToScalar());
                }
            }

            return;
        }

        // Scalar rounding uses nearest-even, as ConvertToInt32 does. Apply finite bounds before the integer cast.
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
                vector = ColorNumerics.ClampToFinite(vector, new Vector4(-ShortMaximum), new Vector4(ShortMaximum));
            }
            else
            {
                if (scaled)
                {
                    vector *= SignedIntegerRange;
                    vector += new Vector4(SignedIntegerMinimum);
                }

                vector = ColorNumerics.ClampToFinite(vector, new Vector4(short.MinValue), new Vector4(short.MaxValue));
            }

            int componentIndex = index * componentCount;
            destination[componentIndex] = (short)MathF.Round(vector.X);
            destination[componentIndex + 1] = (short)MathF.Round(vector.Y);

            if (componentCount == 4)
            {
                destination[componentIndex + 2] = (short)MathF.Round(vector.Z);
                destination[componentIndex + 3] = (short)MathF.Round(vector.W);
            }
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
            source = ColorNumerics.ClampToFinite(source, Vector512.Create(-ShortMaximum), Vector512.Create(ShortMaximum));
        }
        else
        {
            if (scaled)
            {
                source *= Vector512.Create(SignedIntegerRange);
                source += Vector512.Create(SignedIntegerMinimum);
            }

            source = ColorNumerics.ClampToFinite(source, Vector512.Create((float)short.MinValue), Vector512.Create((float)short.MaxValue));
        }

        // All lanes are finite and within the destination's short range here. Round to nearest-even before narrowing.
        // SNORM uses [-32767,32767]; SINT also retains -32768. Their different lower bounds are intentional.
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
            source = ColorNumerics.ClampToFinite(source, Vector256.Create(-ShortMaximum), Vector256.Create(ShortMaximum));
        }
        else
        {
            if (scaled)
            {
                source *= Vector256.Create(SignedIntegerRange);
                source += Vector256.Create(SignedIntegerMinimum);
            }

            source = ColorNumerics.ClampToFinite(source, Vector256.Create((float)short.MinValue), Vector256.Create((float)short.MaxValue));
        }

        // All lanes are finite and within the destination's short range here. Round to nearest-even before narrowing.
        // SNORM uses [-32767,32767]; SINT also retains -32768. Their different lower bounds are intentional.
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
            source = ColorNumerics.ClampToFinite(source, Vector128.Create(-ShortMaximum), Vector128.Create(ShortMaximum));
        }
        else
        {
            if (scaled)
            {
                source *= Vector128.Create(SignedIntegerRange);
                source += Vector128.Create(SignedIntegerMinimum);
            }

            source = ColorNumerics.ClampToFinite(source, Vector128.Create((float)short.MinValue), Vector128.Create((float)short.MaxValue));
        }

        // All lanes are finite and within the destination's short range here. Round to nearest-even before narrowing.
        // SNORM uses [-32767,32767]; SINT also retains -32768. Their different lower bounds are intentional.
        return Vector128_.ConvertToInt32RoundToEven(source);
    }
}
