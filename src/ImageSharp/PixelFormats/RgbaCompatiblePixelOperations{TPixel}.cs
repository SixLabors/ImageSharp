// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.PixelFormats.Utils;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Shares bulk conversion for RGB and RGBA byte layouts.
/// </summary>
/// <remarks>
/// Typed byte conversions supply channel order. Associated layouts retain their stored bytes when changing order.
/// Four-byte output packs directly into the destination and shuffles in place. Three-byte output retains its existing temporary row.
/// Expansion stages packed bytes in the unwritten end of the vector destination, so it needs no separate allocation.
/// </remarks>
/// <typeparam name="TPixel">The compatible RGB or RGBA byte pixel format.</typeparam>
internal abstract class RgbaCompatiblePixelOperations<TPixel> : PixelOperations<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    /// <summary>
    /// Gets the minimum straight-byte pixel count for destination staging.
    /// </summary>
    private static int Vector4ConversionThreshold => CalculateVector4ConversionThreshold();

    /// <inheritdoc />
    public override void ToUnassociatedVector4(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination)
    {
        bool associated = TPixel.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Associated;

        if (!associated && source.Length < Vector4ConversionThreshold)
        {
            base.ToUnassociatedVector4(configuration, source, destination);

            return;
        }

        this.Unpack(configuration, source, destination, associated);
    }

    /// <inheritdoc />
    public override void ToAssociatedVector4(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination)
    {
        if (TPixel.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Associated)
        {
            this.Unpack(configuration, source, destination, false);
        }
        else if (source.Length < Vector4ConversionThreshold)
        {
            base.ToAssociatedVector4(configuration, source, destination);
        }
        else
        {
            this.ToUnassociatedVector4(configuration, source, destination);
            Numerics.Premultiply(destination[..source.Length]);
        }
    }

    /// <inheritdoc />
    public override void ToUnassociatedScaledVector4(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination)
        => this.ToUnassociatedVector4(configuration, source, destination);

    /// <inheritdoc />
    public override void ToAssociatedScaledVector4(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination)
        => this.ToAssociatedVector4(configuration, source, destination);

    /// <inheritdoc />
    public override void FromUnassociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<TPixel> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        if (TPixel.GetPixelTypeInfo().ComponentInfo!.Value.ComponentCount == 3 && source.Length < Vector4ConversionThreshold)
        {
            base.FromUnassociatedVector4Destructive(configuration, source, destination);

            return;
        }

        if (TPixel.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Associated)
        {
            // Use the rounded destination alpha before packing associated RGB, matching the single-pixel byte conversion.
            AssociateToByte(source);
        }

        this.Pack(configuration, source, destination[..source.Length]);
    }

    /// <inheritdoc />
    public override void FromAssociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<TPixel> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        if (TPixel.GetPixelTypeInfo().ComponentInfo!.Value.ComponentCount == 3 && source.Length < Vector4ConversionThreshold)
        {
            base.FromAssociatedVector4Destructive(configuration, source, destination);

            return;
        }

        if (TPixel.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Associated)
        {
            ReassociateToByte(source);
        }
        else
        {
            PixelFormats.PixelOperations.UnPremultiplyForPacking(source);
        }

        this.Pack(configuration, source, destination[..source.Length]);
    }

    /// <inheritdoc />
    public override void FromUnassociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<TPixel> destination)
        => this.FromUnassociatedVector4Destructive(configuration, source, destination);

    /// <inheritdoc />
    public override void FromAssociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<TPixel> destination)
        => this.FromAssociatedVector4Destructive(configuration, source, destination);

    /// <summary>
    /// Expands canonical RGBA bytes staged in the output storage.
    /// </summary>
    /// <param name="configuration">The conversion configuration.</param>
    /// <param name="source">The packed source pixels.</param>
    /// <param name="destination">The output vectors.</param>
    /// <param name="unassociate">Whether to divide associated bytes by their alpha.</param>
    private void Unpack(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination, bool unassociate)
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        destination = destination[..source.Length];

        if (source.IsEmpty)
        {
            return;
        }

        // Output has four times as many bytes as packed RGBA. The final N-1 packed slots start at 4*N-(N-1) = 3*N+1.
        // Forward expansion consumes the staged bytes before overwriting them. Convert the overlapping final pixel last.
        int last = source.Length - 1;
        Span<byte> staging = MemoryMarshal.AsBytes(destination).Slice(((3 * source.Length) + 1) * 4, last * 4);
        bool associated = TPixel.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Associated;

        if (associated)
        {
            this.ToRgba32P(configuration, source[..last], MemoryMarshal.Cast<byte, Rgba32P>(staging));
        }
        else
        {
            this.ToRgba32(configuration, source[..last], MemoryMarshal.Cast<byte, Rgba32>(staging));
        }

        if (unassociate)
        {
            UnassociateBytes(staging, destination[..last]);
        }
        else
        {
            SimdUtils.ByteToNormalizedFloat(staging, MemoryMarshal.Cast<Vector4, float>(destination[..last]));
        }

        destination[last] = associated && !unassociate ? source[last].ToAssociatedVector4() : source[last].ToUnassociatedVector4();
    }

    /// <summary>
    /// Packs canonical RGBA bytes and uses the existing typed conversion for channel order.
    /// </summary>
    /// <param name="configuration">The conversion configuration.</param>
    /// <param name="source">The components in normalized units, or associated byte magnitudes.</param>
    /// <param name="destination">The output pixels, restricted to the input count.</param>
    private void Pack(Configuration configuration, Span<Vector4> source, Span<TPixel> destination)
    {
        if (TPixel.GetPixelTypeInfo().ComponentInfo!.Value.ComponentCount == 4)
        {
            Span<byte> bytes = MemoryMarshal.AsBytes(destination);

            // All four-byte typed shuffles support identical source and destination storage. No temporary pixels are needed.
            if (TPixel.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Associated)
            {
                SimdUtils.FloatToByteSaturate(MemoryMarshal.Cast<Vector4, float>(source), bytes);
                this.FromRgba32P(configuration, MemoryMarshal.Cast<byte, Rgba32P>(bytes), destination);
            }
            else
            {
                SimdUtils.NormalizedFloatToByteSaturate(MemoryMarshal.Cast<Vector4, float>(source), bytes);
                this.FromRgba32(configuration, MemoryMarshal.Cast<byte, Rgba32>(bytes), destination);
            }
        }
        else
        {
            Guard.NotNull(configuration, nameof(configuration));

            // RGB24 output has no room for an RGBA intermediate. Retain one allocator-owned packed row for this conversion.
            using IMemoryOwner<Rgba32> owner = configuration.MemoryAllocator.Allocate<Rgba32>(source.Length);
            Span<Rgba32> packed = owner.Memory.Span;
            SimdUtils.NormalizedFloatToByteSaturate(MemoryMarshal.Cast<Vector4, float>(source), MemoryMarshal.AsBytes(packed));
            this.FromRgba32(configuration, packed, destination);
        }
    }

    /// <summary>
    /// Selects the minimum input count at which RGBA staging uses accelerated conversion.
    /// </summary>
    /// <returns>The existing width-dependent threshold, or the scalar-only threshold.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CalculateVector4ConversionThreshold()
    {
        if (!Vector128.IsHardwareAccelerated)
        {
            return int.MaxValue;
        }

        if (Vector512.IsHardwareAccelerated)
        {
            return 512;
        }

        if (Vector256.IsHardwareAccelerated)
        {
            return 256;
        }

        return 128;
    }

    /// <summary>
    /// Converts associated RGBA byte components to an unassociated scaled vector.
    /// </summary>
    /// <param name="red">The associated red component.</param>
    /// <param name="green">The associated green component.</param>
    /// <param name="blue">The associated blue component.</param>
    /// <param name="alpha">The alpha component.</param>
    /// <returns>The unassociated scaled vector.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 UnassociateBytes(byte red, byte green, byte blue, byte alpha)
    {
        // RGB / alphaByte restores straight RGB directly in normalized units. Alpha itself uses alphaByte / 255.
        // Divide the byte magnitudes directly. Normalizing first can move an exact byte result across its rounding midpoint.
        Vector4 vector = new(red, green, blue, alpha);
        float colorDivisor = alpha == 0 ? byte.MaxValue : alpha;
        return vector / new Vector4(colorDivisor, colorDivisor, colorDivisor, byte.MaxValue);
    }

    /// <summary>
    /// Converts packed RGBA byte magnitudes to unassociated scaled vectors.
    /// </summary>
    /// <param name="source">The packed RGBA bytes.</param>
    /// <param name="destination">The destination vectors.</param>
    public static void UnassociateBytes(ReadOnlySpan<byte> source, Span<Vector4> destination)
    {
        ref byte sourceBase = ref MemoryMarshal.GetReference(source);
        ref Vector4 destinationBase = ref MemoryMarshal.GetReference(destination);

        // Each packed component occupies one byte, so the destination Vector4 lane count also defines the source byte stride.
        int componentsPerPixel = Vector128<float>.Count;
        int i = 0;

        // Packed bytes have [R, G, B, A] order. Widen byte -> ushort -> uint without changing that order.
        // Four pixels fit one 512-bit float vector; two fit one 256-bit float vector.
        if (Vector512.IsHardwareAccelerated)
        {
            int pixelsPerVector = Vector512<float>.Count / componentsPerPixel;

            for (; i <= destination.Length - pixelsPerVector; i += pixelsPerVector)
            {
                Vector128<byte> packed = Vector128.LoadUnsafe(ref sourceBase, (nuint)(i * componentsPerPixel));
                Vector128<ushort> lowerShorts = Vector128.WidenLower(packed);
                Vector128<ushort> upperShorts = Vector128.WidenUpper(packed);
                Vector256<uint> lowerIntegers = Vector256.Create(Vector128.WidenLower(lowerShorts), Vector128.WidenUpper(lowerShorts));
                Vector256<uint> upperIntegers = Vector256.Create(Vector128.WidenLower(upperShorts), Vector128.WidenUpper(upperShorts));
                Vector512<int> integers = Vector512.Create(lowerIntegers, upperIntegers).AsInt32();
                Unsafe.As<Vector4, Vector512<float>>(ref Unsafe.Add(ref destinationBase, (uint)i)) =
                    UnassociateBytes(Vector512.ConvertToSingle(integers));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            int pixelsPerVector = Vector256<float>.Count / componentsPerPixel;

            for (; i <= destination.Length - pixelsPerVector; i += pixelsPerVector)
            {
                ulong packed = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref sourceBase, (uint)(i * componentsPerPixel)));
                Vector128<ushort> shorts = Vector128.WidenLower(Vector128.CreateScalarUnsafe(packed).AsByte());
                Vector256<int> integers = Vector256.Create(Vector128.WidenLower(shorts), Vector128.WidenUpper(shorts)).AsInt32();
                Unsafe.As<Vector4, Vector256<float>>(ref Unsafe.Add(ref destinationBase, (uint)i)) =
                    UnassociateBytes(Vector256.ConvertToSingle(integers));
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            // One 16-byte load supplies four pixels. Each pair of widening steps produces one complete RGBA float vector.
            int pixelsPerVector = Vector128<byte>.Count / componentsPerPixel;

            for (; i <= destination.Length - pixelsPerVector; i += pixelsPerVector)
            {
                Vector128<byte> packed = Vector128.LoadUnsafe(ref sourceBase, (nuint)(i * componentsPerPixel));
                (Vector128<ushort> shorts0, Vector128<ushort> shorts1) = Vector128.Widen(packed);
                (Vector128<uint> integers0, Vector128<uint> integers1) = Vector128.Widen(shorts0);
                (Vector128<uint> integers2, Vector128<uint> integers3) = Vector128.Widen(shorts1);

                Unsafe.Add(ref destinationBase, (uint)i) = UnassociateBytes(Vector128.ConvertToSingle(integers0.AsInt32())).AsVector4();
                Unsafe.Add(ref destinationBase, (uint)i + 1u) = UnassociateBytes(Vector128.ConvertToSingle(integers1.AsInt32())).AsVector4();
                Unsafe.Add(ref destinationBase, (uint)i + 2u) = UnassociateBytes(Vector128.ConvertToSingle(integers2.AsInt32())).AsVector4();
                Unsafe.Add(ref destinationBase, (uint)i + 3u) = UnassociateBytes(Vector128.ConvertToSingle(integers3.AsInt32())).AsVector4();
            }
        }

        // Read only the four bytes of each remaining pixel. This also handles the full input when SIMD is unavailable.
        for (; i < destination.Length; i++)
        {
            int offset = i * componentsPerPixel;

            Unsafe.Add(ref destinationBase, (uint)i) = UnassociateBytes(
                Unsafe.Add(ref sourceBase, (uint)offset),
                Unsafe.Add(ref sourceBase, (uint)offset + 1u),
                Unsafe.Add(ref sourceBase, (uint)offset + 2u),
                Unsafe.Add(ref sourceBase, (uint)offset + 3u));
        }
    }

    /// <summary>
    /// Converts four associated RGBA byte-magnitude vectors to unassociated scaled vectors.
    /// </summary>
    /// <param name="source">The associated byte-magnitude vectors.</param>
    /// <returns>The unassociated scaled vectors.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<float> UnassociateBytes(Vector512<float> source)
    {
        Vector512<float> zero = Vector512<float>.Zero;
        Vector512<float> byteMax = Vector512.Create((float)byte.MaxValue);
        Vector512<float> alpha = Vector512_.ShuffleNative(source, 0b_11_11_11_11);

        // RGB uses its pixel's alpha byte as divisor. At zero alpha, use 255 to retain hidden RGB in normalized units.
        // The all-ones lanes 3, 7, 11 and 15 select 255 for alpha itself, independent of the RGB divisor.
        Vector512<float> alphaMask = Vector512.Create(0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1).AsSingle();
        Vector512<float> normalizeMask = Vector512.Equals(alpha, zero) | alphaMask;
        Vector512<float> divisor = Vector512.ConditionalSelect(normalizeMask, byteMax, alpha);
        return source / divisor;
    }

    /// <summary>
    /// Converts two associated RGBA byte-magnitude vectors to unassociated scaled vectors.
    /// </summary>
    /// <param name="source">The associated byte-magnitude vectors.</param>
    /// <returns>The unassociated scaled vectors.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<float> UnassociateBytes(Vector256<float> source)
    {
        Vector256<float> zero = Vector256<float>.Zero;
        Vector256<float> byteMax = Vector256.Create((float)byte.MaxValue);
        Vector256<float> alpha = Vector256_.ShuffleNative(source, 0b_11_11_11_11);

        // RGB uses its pixel's alpha byte as divisor. At zero alpha, use 255 to retain hidden RGB in normalized units.
        // The all-ones lanes 3 and 7 select 255 for alpha itself, independent of the RGB divisor.
        Vector256<float> alphaMask = Vector256.Create(0, 0, 0, -1, 0, 0, 0, -1).AsSingle();
        Vector256<float> normalizeMask = Vector256.Equals(alpha, zero) | alphaMask;
        Vector256<float> divisor = Vector256.ConditionalSelect(normalizeMask, byteMax, alpha);
        return source / divisor;
    }

    /// <summary>
    /// Converts an associated RGBA byte-magnitude vector to an unassociated scaled vector.
    /// </summary>
    /// <param name="source">The associated byte-magnitude vector.</param>
    /// <returns>The unassociated scaled vector.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<float> UnassociateBytes(Vector128<float> source)
    {
        Vector128<float> zero = Vector128<float>.Zero;
        Vector128<float> byteMax = Vector128.Create((float)byte.MaxValue);
        Vector128<float> alpha = Vector128_.ShuffleNative(source, 0b_11_11_11_11);

        // RGB uses the alpha byte as divisor. At zero alpha, use 255 to retain hidden RGB in normalized units.
        // The all-ones fourth lane selects 255 for alpha itself, independent of the RGB divisor.
        Vector128<float> alphaMask = Vector128.Create(0, 0, 0, -1).AsSingle();
        Vector128<float> normalizeMask = Vector128.Equals(alpha, zero) | alphaMask;
        Vector128<float> divisor = Vector128.ConditionalSelect(normalizeMask, byteMax, alpha);
        return source / divisor;
    }

    /// <summary>
    /// Converts unassociated scaled vectors to associated byte magnitudes.
    /// </summary>
    /// <param name="source">The vectors to convert in place.</param>
    private static void AssociateToByte(Span<Vector4> source)
    {
        // Input is normalized RGBA; output is in byte units. The following packer rounds RGB and changes channel order.
        // Each register is loaded before replacement, so destructive conversion needs no second row.
        if (Vector512.IsHardwareAccelerated)
        {
            int pixelsPerVector = Vector512<float>.Count / Vector128<float>.Count;
            ref Vector512<float> sourceBase = ref Unsafe.As<Vector4, Vector512<float>>(ref MemoryMarshal.GetReference(source));
            ref Vector512<float> sourceEnd = ref Unsafe.Add(ref sourceBase, (uint)source.Length / (uint)pixelsPerVector);

            while (Unsafe.IsAddressLessThan(ref sourceBase, ref sourceEnd))
            {
                sourceBase = AssociateToByte(sourceBase);
                sourceBase = ref Unsafe.Add(ref sourceBase, 1);
            }

            // SIMD widths are powers of two, so masking finds the start of the unprocessed remainder without division.
            source = source[(source.Length & ~(pixelsPerVector - 1))..];
        }

        if (Vector256.IsHardwareAccelerated)
        {
            int pixelsPerVector = Vector256<float>.Count / Vector128<float>.Count;
            ref Vector256<float> sourceBase = ref Unsafe.As<Vector4, Vector256<float>>(ref MemoryMarshal.GetReference(source));
            ref Vector256<float> sourceEnd = ref Unsafe.Add(ref sourceBase, (uint)source.Length / (uint)pixelsPerVector);

            while (Unsafe.IsAddressLessThan(ref sourceBase, ref sourceEnd))
            {
                sourceBase = AssociateToByte(sourceBase);
                sourceBase = ref Unsafe.Add(ref sourceBase, 1);
            }

            // SIMD widths are powers of two, so masking finds the start of the unprocessed remainder without division.
            source = source[(source.Length & ~(pixelsPerVector - 1))..];
        }

        if (Vector128.IsHardwareAccelerated)
        {
            ref Vector128<float> sourceBase = ref Unsafe.As<Vector4, Vector128<float>>(ref MemoryMarshal.GetReference(source));
            ref Vector128<float> sourceEnd = ref Unsafe.Add(ref sourceBase, (uint)source.Length);

            while (Unsafe.IsAddressLessThan(ref sourceBase, ref sourceEnd))
            {
                sourceBase = AssociateToByte(sourceBase);
                sourceBase = ref Unsafe.Add(ref sourceBase, 1);
            }

            return;
        }

        ref Vector4 tailBase = ref MemoryMarshal.GetReference(source);

        for (nuint i = 0; i < (uint)source.Length; i++)
        {
            Unsafe.Add(ref tailBase, i) = AssociateToByte(Unsafe.Add(ref tailBase, i));
        }
    }

    /// <summary>
    /// Converts four unassociated scaled vectors to associated byte magnitudes.
    /// </summary>
    /// <param name="source">The unassociated scaled vectors.</param>
    /// <returns>The associated byte magnitudes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<float> AssociateToByte(Vector512<float> source)
    {
        Vector512<float> zero = Vector512<float>.Zero;
        Vector512<float> one = Vector512<float>.One;
        Vector512<float> byteMax = Vector512.Create((float)byte.MaxValue);
        source = ColorNumerics.ClampToFinite(source, zero, one);
        Vector512<float> alpha = Vector512_.ShuffleNative(source, 0b_11_11_11_11);

        // floor(alpha * 255 + 0.5) matches the scalar byte-alpha rounding rule for the already bounded input.
        // Multiplying normalized RGB by that byte associates and converts to byte units in one step.
        Vector512<float> storedAlpha = Vector512.Floor((alpha * byteMax) + Vector512.Create(.5F));
        Vector512<float> result = source * storedAlpha;

        // RGB is associated using the quantized alpha byte, while W stores that byte directly.
        Vector512<float> alphaMask = Vector512.Create(0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1).AsSingle();
        return Vector512.ConditionalSelect(alphaMask, storedAlpha, result);
    }

    /// <summary>
    /// Converts two unassociated scaled vectors to associated byte magnitudes.
    /// </summary>
    /// <param name="source">The unassociated scaled vectors.</param>
    /// <returns>The associated byte magnitudes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<float> AssociateToByte(Vector256<float> source)
    {
        Vector256<float> zero = Vector256<float>.Zero;
        Vector256<float> one = Vector256<float>.One;
        Vector256<float> byteMax = Vector256.Create((float)byte.MaxValue);
        source = ColorNumerics.ClampToFinite(source, zero, one);
        Vector256<float> alpha = Vector256_.ShuffleNative(source, 0b_11_11_11_11);

        // floor(alpha * 255 + 0.5) matches the scalar byte-alpha rounding rule for the already bounded input.
        // Multiplying normalized RGB by that byte associates and converts to byte units in one step.
        Vector256<float> storedAlpha = Vector256.Floor((alpha * byteMax) + Vector256.Create(.5F));
        Vector256<float> result = source * storedAlpha;

        // RGB is associated using the quantized alpha byte, while W stores that byte directly.
        Vector256<float> alphaMask = Vector256.Create(0, 0, 0, -1, 0, 0, 0, -1).AsSingle();
        return Vector256.ConditionalSelect(alphaMask, storedAlpha, result);
    }

    /// <summary>
    /// Converts an unassociated scaled vector to associated byte magnitudes.
    /// </summary>
    /// <param name="source">The unassociated scaled vector.</param>
    /// <returns>The associated byte magnitudes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<float> AssociateToByte(Vector128<float> source)
    {
        Vector128<float> zero = Vector128<float>.Zero;
        Vector128<float> one = Vector128<float>.One;
        Vector128<float> byteMax = Vector128.Create((float)byte.MaxValue);
        source = ColorNumerics.ClampToFinite(source, zero, one);
        Vector128<float> alpha = Vector128_.ShuffleNative(source, 0b_11_11_11_11);

        // floor(alpha * 255 + 0.5) matches the scalar byte-alpha rounding rule for the already bounded input.
        // Multiplying normalized RGB by that byte associates and converts to byte units in one step.
        Vector128<float> storedAlpha = Vector128.Floor((alpha * byteMax) + Vector128.Create(.5F));
        Vector128<float> result = source * storedAlpha;

        // RGB is associated using the quantized alpha byte, while W stores that byte directly.
        Vector128<float> alphaMask = Vector128.Create(0, 0, 0, -1).AsSingle();
        return Vector128.ConditionalSelect(alphaMask, storedAlpha, result);
    }

    /// <summary>
    /// Converts an unassociated scaled vector to associated byte magnitudes.
    /// </summary>
    /// <param name="source">The unassociated scaled vector.</param>
    /// <returns>The associated byte magnitudes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 AssociateToByte(Vector4 source)
    {
        source = ColorNumerics.ClampToFinite(source, Vector4.Zero, Vector4.One);
        float storedAlpha = (byte)ColorNumerics.ClampToFinite((source.W * byte.MaxValue) + 0.5F, 0, byte.MaxValue);

        source *= storedAlpha;
        source.W = storedAlpha;
        return source;
    }

    /// <summary>
    /// Converts an associated scaled vector to associated byte magnitudes using the alpha byte the destination stores.
    /// </summary>
    /// <param name="source">The associated scaled vector.</param>
    /// <returns>The associated byte magnitudes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 ReassociateToByte(Vector4 source)
    {
        // Keep positive finite alpha for RGB division. Infinity must become finite before it is used as a divisor.
        float alpha = ColorNumerics.ClampToFinite(source.W, 0F, float.MaxValue);

        if (alpha <= 0)
        {
            return Vector4.Zero;
        }

        float byteAlpha = alpha * byte.MaxValue;
        float storedAlpha = (byte)ColorNumerics.ClampToFinite(byteAlpha + 0.5F, 0, byte.MaxValue);

        if (byteAlpha == storedAlpha)
        {
            // Rounding leaves alpha unchanged. Multiply RGB by 255 directly to preserve exact byte midpoints.
            // Dividing and multiplying again could move a component across its rounding boundary.
            source *= byte.MaxValue;
        }
        else
        {
            // Recover straight RGB, then multiply by the stored alpha byte.
            // Working in byte units avoids another multiplication by 255 before rounding.
            Numerics.UnPremultiply(ref source, new Vector4(alpha));
            source *= storedAlpha;
        }

        source.W = storedAlpha;
        Numerics.ClampRgbToAlpha(ref source);
        return source;
    }

    /// <summary>
    /// Converts associated scaled vectors to associated byte magnitudes using the alpha bytes the destination stores.
    /// </summary>
    /// <param name="source">The vectors to convert in place.</param>
    private static void ReassociateToByte(Span<Vector4> source)
    {
        if (Vector512.IsHardwareAccelerated)
        {
            int pixelsPerVector = Vector512<float>.Count / Vector128<float>.Count;
            ref Vector512<float> vectorSourceBase = ref Unsafe.As<Vector4, Vector512<float>>(ref MemoryMarshal.GetReference(source));
            ref Vector512<float> sourceEnd = ref Unsafe.Add(ref vectorSourceBase, (uint)source.Length / (uint)pixelsPerVector);

            while (Unsafe.IsAddressLessThan(ref vectorSourceBase, ref sourceEnd))
            {
                vectorSourceBase = ReassociateToByte(vectorSourceBase);
                vectorSourceBase = ref Unsafe.Add(ref vectorSourceBase, 1);
            }

            // SIMD widths are powers of two, so masking finds the start of the unprocessed remainder without division.
            source = source[(source.Length & ~(pixelsPerVector - 1))..];
        }

        if (Vector256.IsHardwareAccelerated)
        {
            int pixelsPerVector = Vector256<float>.Count / Vector128<float>.Count;
            ref Vector256<float> vectorSourceBase = ref Unsafe.As<Vector4, Vector256<float>>(ref MemoryMarshal.GetReference(source));
            ref Vector256<float> sourceEnd = ref Unsafe.Add(ref vectorSourceBase, (uint)source.Length / (uint)pixelsPerVector);

            while (Unsafe.IsAddressLessThan(ref vectorSourceBase, ref sourceEnd))
            {
                vectorSourceBase = ReassociateToByte(vectorSourceBase);
                vectorSourceBase = ref Unsafe.Add(ref vectorSourceBase, 1);
            }

            // SIMD widths are powers of two, so masking finds the start of the unprocessed remainder without division.
            source = source[(source.Length & ~(pixelsPerVector - 1))..];
        }

        if (Vector128.IsHardwareAccelerated)
        {
            ref Vector128<float> vectorSourceBase = ref Unsafe.As<Vector4, Vector128<float>>(ref MemoryMarshal.GetReference(source));
            ref Vector128<float> sourceEnd = ref Unsafe.Add(ref vectorSourceBase, (uint)source.Length);

            while (Unsafe.IsAddressLessThan(ref vectorSourceBase, ref sourceEnd))
            {
                vectorSourceBase = ReassociateToByte(vectorSourceBase);
                vectorSourceBase = ref Unsafe.Add(ref vectorSourceBase, 1);
            }

            return;
        }

        ref Vector4 sourceBase = ref MemoryMarshal.GetReference(source);

        for (nuint i = 0; i < (uint)source.Length; i++)
        {
            Unsafe.Add(ref sourceBase, i) = ReassociateToByte(Unsafe.Add(ref sourceBase, i));
        }
    }

    /// <summary>
    /// Converts four associated scaled vectors to associated byte magnitudes using the alpha bytes the destination stores.
    /// </summary>
    /// <param name="source">The associated scaled vectors.</param>
    /// <returns>The associated byte magnitudes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<float> ReassociateToByte(Vector512<float> source)
    {
        Vector512<float> zero = Vector512<float>.Zero;
        Vector512<float> byteMax = Vector512.Create((float)byte.MaxValue);

        // Keep positive finite alpha for RGB division. Infinity uses float.MaxValue; stored byte alpha is bounded separately.
        Vector512<float> alpha = Vector512_.ShuffleNative(source, 0b_11_11_11_11);
        alpha = ColorNumerics.ClampToFinite(alpha, zero, Vector512.Create(float.MaxValue));
        Vector512<float> byteAlpha = alpha * byteMax;
        Vector512<float> storedAlpha = Vector512.Floor(ColorNumerics.ClampToFinite(byteAlpha + Vector512.Create(.5F), zero, byteMax));
        Vector512<float> result = (source / alpha) * storedAlpha;

        // Exact byte alpha values need no reassociation. Multiplying by 255 directly preserves RGB values that already lie on byte midpoints.
        result = Vector512.ConditionalSelect(Vector512.Equals(byteAlpha, storedAlpha), source * byteMax, result);
        Vector512<float> alphaMask = Vector512.Create(0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1).AsSingle();
        result = Vector512.ConditionalSelect(alphaMask, storedAlpha, result);
        result = ColorNumerics.ClampToFinite(result, zero, storedAlpha);
        return Vector512.ConditionalSelect(Vector512.Equals(alpha, zero), zero, result);
    }

    /// <summary>
    /// Converts two associated scaled vectors to associated byte magnitudes using the alpha bytes the destination stores.
    /// </summary>
    /// <param name="source">The associated scaled vectors.</param>
    /// <returns>The associated byte magnitudes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<float> ReassociateToByte(Vector256<float> source)
    {
        Vector256<float> zero = Vector256<float>.Zero;
        Vector256<float> byteMax = Vector256.Create((float)byte.MaxValue);

        // Keep positive finite alpha for RGB division. Infinity uses float.MaxValue; stored byte alpha is bounded separately.
        Vector256<float> alpha = Vector256_.ShuffleNative(source, 0b_11_11_11_11);
        alpha = ColorNumerics.ClampToFinite(alpha, zero, Vector256.Create(float.MaxValue));
        Vector256<float> byteAlpha = alpha * byteMax;
        Vector256<float> storedAlpha = Vector256.Floor(ColorNumerics.ClampToFinite(byteAlpha + Vector256.Create(.5F), zero, byteMax));
        Vector256<float> result = (source / alpha) * storedAlpha;

        // Exact byte alpha values need no reassociation. Multiplying by 255 directly preserves RGB values that already lie on byte midpoints.
        result = Vector256.ConditionalSelect(Vector256.Equals(byteAlpha, storedAlpha), source * byteMax, result);
        Vector256<float> alphaMask = Vector256.Create(0, 0, 0, -1, 0, 0, 0, -1).AsSingle();
        result = Vector256.ConditionalSelect(alphaMask, storedAlpha, result);
        result = ColorNumerics.ClampToFinite(result, zero, storedAlpha);
        return Vector256.ConditionalSelect(Vector256.Equals(alpha, zero), zero, result);
    }

    /// <summary>
    /// Converts an associated scaled vector to associated byte magnitudes using the alpha byte the destination stores.
    /// </summary>
    /// <param name="source">The associated scaled vector.</param>
    /// <returns>The associated byte magnitudes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<float> ReassociateToByte(Vector128<float> source)
    {
        Vector128<float> zero = Vector128<float>.Zero;
        Vector128<float> byteMax = Vector128.Create((float)byte.MaxValue);

        // Keep positive finite alpha for RGB division. Infinity uses float.MaxValue; stored byte alpha is bounded separately.
        Vector128<float> alpha = Vector128_.ShuffleNative(source, 0b_11_11_11_11);
        alpha = ColorNumerics.ClampToFinite(alpha, zero, Vector128.Create(float.MaxValue));
        Vector128<float> byteAlpha = alpha * byteMax;
        Vector128<float> storedAlpha = Vector128.Floor(ColorNumerics.ClampToFinite(byteAlpha + Vector128.Create(.5F), zero, byteMax));
        Vector128<float> result = (source / alpha) * storedAlpha;

        // Exact byte alpha values need no reassociation. Multiplying by 255 directly preserves RGB values that already lie on byte midpoints.
        result = Vector128.ConditionalSelect(Vector128.Equals(byteAlpha, storedAlpha), source * byteMax, result);
        Vector128<float> alphaMask = Vector128.Create(0, 0, 0, -1).AsSingle();
        result = Vector128.ConditionalSelect(alphaMask, storedAlpha, result);
        result = ColorNumerics.ClampToFinite(result, zero, storedAlpha);
        return Vector128.ConditionalSelect(Vector128.Equals(alpha, zero), zero, result);
    }

    /// <summary>
    /// Converts a floating-point byte magnitude to a byte.
    /// </summary>
    /// <param name="value">The byte magnitude.</param>
    /// <returns>The rounded and clamped byte.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte ConvertToByte(float value) => (byte)ColorNumerics.ClampToFinite(value + .5F, 0, byte.MaxValue);
}
