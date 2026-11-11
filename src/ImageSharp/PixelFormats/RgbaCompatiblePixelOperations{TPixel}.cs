// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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

        // Associated bytes are divided by their alpha byte in one division, so exact byte results stay exact.
        this.Unpack<PixelFormats.PixelOperations.NoAlphaOperator>(configuration, source, destination, associated);
    }

    /// <inheritdoc />
    public override void ToAssociatedVector4(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination)
    {
        if (TPixel.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Associated)
        {
            this.Unpack<PixelFormats.PixelOperations.NoAlphaOperator>(configuration, source, destination, false);
        }
        else if (source.Length < Vector4ConversionThreshold)
        {
            base.ToAssociatedVector4(configuration, source, destination);
        }
        else
        {
            // The unpack normalizes and associates each pixel in one pass.
            this.Unpack<PixelFormats.PixelOperations.AssociateOperator>(configuration, source, destination, false);
        }
    }

    /// <inheritdoc />
    public override void ToUnassociatedScaledVector4(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination)
        => this.ToUnassociatedVector4(configuration, source, destination);

    /// <inheritdoc />
    public override void ToAssociatedScaledVector4(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination)
        => this.ToAssociatedVector4(configuration, source, destination);

    /// <inheritdoc />
    internal override void ToAssociatedScaledVector4<TAlpha>(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination)
    {
        // Associated bytes are already associated scaled vectors after normalization. Straight bytes need association first.
        if (TPixel.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Associated)
        {
            this.Unpack<TAlpha>(configuration, source, destination, false);
        }
        else
        {
            base.ToAssociatedScaledVector4<TAlpha>(configuration, source, destination);
        }
    }

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
            this.Pack<AssociateToByteOperator>(configuration, source, destination[..source.Length]);
        }
        else
        {
            this.Pack<PixelFormats.PixelOperations.NoAlphaOperator>(configuration, source, destination[..source.Length]);
        }
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
            this.Pack<ReassociateToByteOperator>(configuration, source, destination[..source.Length]);
        }
        else
        {
            // The pack unassociates and converts each pixel in one pass.
            this.Pack<PixelFormats.PixelOperations.UnassociateOperator>(configuration, source, destination[..source.Length]);
        }
    }

    /// <inheritdoc />
    public override void FromUnassociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<TPixel> destination)
        => this.FromUnassociatedVector4Destructive(configuration, source, destination);

    /// <inheritdoc />
    public override void FromAssociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<TPixel> destination)
        => this.FromAssociatedVector4Destructive(configuration, source, destination);

    /// <summary>
    /// Expands canonical RGBA bytes staged in the output storage to scaled vectors, and applies <typeparamref name="TAlpha"/> in
    /// the same pass.
    /// </summary>
    /// <typeparam name="TAlpha">The alpha step for each scaled pixel. It is not used when <paramref name="unassociate"/> is set.</typeparam>
    /// <param name="configuration">The conversion configuration.</param>
    /// <param name="source">The packed source pixels.</param>
    /// <param name="destination">The output vectors.</param>
    /// <param name="unassociate">Whether to divide associated bytes by their alpha byte.</param>
    private void Unpack<TAlpha>(Configuration configuration, ReadOnlySpan<TPixel> source, Span<Vector4> destination, bool unassociate)
        where TAlpha : struct, PixelFormats.PixelOperations.IAlphaOperator
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
            SimdUtils.ByteToNormalizedFloat<TAlpha>(staging, MemoryMarshal.Cast<Vector4, float>(destination[..last]));
        }

        // The last pixel overlaps the staged bytes, so the scalar conversion of the pixel type gives it.
        destination[last] = unassociate
            ? source[last].ToUnassociatedScaledVector4()
            : TAlpha.Apply(source[last].ToScaledVector4().AsVector128()).AsVector4();
    }

    /// <summary>
    /// Applies <typeparamref name="TAlpha"/> to scaled vectors, packs canonical RGBA bytes in the same pass, and uses the existing
    /// typed conversion for channel order.
    /// </summary>
    /// <param name="configuration">The conversion configuration.</param>
    /// <param name="source">The scaled components.</param>
    /// <param name="destination">The output pixels, restricted to the input count.</param>
    /// <typeparam name="TAlpha">
    /// The alpha step for each pixel. For an associated destination, its result is in byte magnitudes.
    /// </typeparam>
    private void Pack<TAlpha>(Configuration configuration, Span<Vector4> source, Span<TPixel> destination)
        where TAlpha : struct, PixelFormats.PixelOperations.IAlphaOperator
    {
        if (TPixel.GetPixelTypeInfo().ComponentInfo!.Value.ComponentCount == 4)
        {
            Span<byte> bytes = MemoryMarshal.AsBytes(destination);

            // All four-byte typed shuffles support identical source and destination storage. No temporary pixels are needed.
            if (TPixel.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Associated)
            {
                SimdUtils.FloatToByteSaturate<TAlpha>(MemoryMarshal.Cast<Vector4, float>(source), bytes);
                this.FromRgba32P(configuration, MemoryMarshal.Cast<byte, Rgba32P>(bytes), destination);
            }
            else
            {
                SimdUtils.NormalizedFloatToByteSaturate<TAlpha>(MemoryMarshal.Cast<Vector4, float>(source), bytes);
                this.FromRgba32(configuration, MemoryMarshal.Cast<byte, Rgba32>(bytes), destination);
            }
        }
        else
        {
            // Three-byte output has no space for an RGBA intermediate. But the destructive source row has 16 bytes for each pixel.
            // The method puts the 4-byte RGBA values at the start of that row.
            // Each SIMD block loads its 4 source vectors before it stores 1 byte vector.
            // Each later store ends before the next source vector that the method did not read.
            // The scalar remainder reads float i before it writes byte i. Thus, the RGBA bytes never overwrite floats that the method did not read.
            Span<Rgba32> packed = MemoryMarshal.Cast<Vector4, Rgba32>(source)[..source.Length];
            SimdUtils.NormalizedFloatToByteSaturate<TAlpha>(MemoryMarshal.Cast<Vector4, float>(source), MemoryMarshal.AsBytes(packed));
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
        => Numerics.UnPremultiply(Vector128.Create(red, green, blue, (float)alpha), byte.MaxValue).AsVector4();

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

        // Packed bytes have [R, G, B, A] order. The shared widening helpers keep that order.
        // Four pixels fit one 512-bit float vector; two fit one 256-bit float vector.
        if (Vector512.IsHardwareAccelerated)
        {
            int pixelsPerVector = Vector512<float>.Count / componentsPerPixel;

            for (; i <= destination.Length - pixelsPerVector; i += pixelsPerVector)
            {
                Vector512<int> integers = Vector512_.Widen(Vector128.LoadUnsafe(ref sourceBase, (nuint)(i * componentsPerPixel)));
                Unsafe.As<Vector4, Vector512<float>>(ref Unsafe.Add(ref destinationBase, (uint)i)) =
                    Numerics.UnPremultiply(Vector512.ConvertToSingle(integers), byte.MaxValue);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            int pixelsPerVector = Vector256<float>.Count / componentsPerPixel;

            for (; i <= destination.Length - pixelsPerVector; i += pixelsPerVector)
            {
                // An 8-byte load reads exactly two pixels, so the last pair of a row reads no padding.
                Vector256<int> integers = Vector256_.Widen(Vector64.LoadUnsafe(ref sourceBase, (nuint)(i * componentsPerPixel)));
                Unsafe.As<Vector4, Vector256<float>>(ref Unsafe.Add(ref destinationBase, (uint)i)) =
                    Numerics.UnPremultiply(Vector256.ConvertToSingle(integers), byte.MaxValue);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            // One 16-byte load supplies four pixels. Each pair of widening steps produces one complete RGBA float vector.
            int pixelsPerVector = Vector128<byte>.Count / componentsPerPixel;

            for (; i <= destination.Length - pixelsPerVector; i += pixelsPerVector)
            {
                Vector128_.Widen(
                    Vector128.LoadUnsafe(ref sourceBase, (nuint)(i * componentsPerPixel)),
                    out Vector128<int> integers0,
                    out Vector128<int> integers1,
                    out Vector128<int> integers2,
                    out Vector128<int> integers3);

                Unsafe.Add(ref destinationBase, (uint)i) =
                    Numerics.UnPremultiply(Vector128.ConvertToSingle(integers0), byte.MaxValue).AsVector4();
                Unsafe.Add(ref destinationBase, (uint)i + 1u) =
                    Numerics.UnPremultiply(Vector128.ConvertToSingle(integers1), byte.MaxValue).AsVector4();
                Unsafe.Add(ref destinationBase, (uint)i + 2u) =
                    Numerics.UnPremultiply(Vector128.ConvertToSingle(integers2), byte.MaxValue).AsVector4();
                Unsafe.Add(ref destinationBase, (uint)i + 3u) =
                    Numerics.UnPremultiply(Vector128.ConvertToSingle(integers3), byte.MaxValue).AsVector4();
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
    /// Converts an unassociated scaled vector to associated byte magnitudes.
    /// </summary>
    /// <param name="source">The unassociated scaled vector.</param>
    /// <returns>The associated byte magnitudes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 AssociateToByte(Vector4 source)
        => AssociateToByteOperator.Apply(source.AsVector128()).AsVector4();

    /// <summary>
    /// Converts an associated scaled vector to associated byte magnitudes using the alpha byte the destination stores.
    /// </summary>
    /// <param name="source">The associated scaled vector.</param>
    /// <returns>The associated byte magnitudes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 ReassociateToByte(Vector4 source)
        => ReassociateToByteOperator.Apply(source.AsVector128()).AsVector4();

    /// <summary>
    /// Converts a floating-point byte magnitude to a byte.
    /// </summary>
    /// <param name="value">The byte magnitude.</param>
    /// <returns>The rounded and clamped byte.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte ConvertToByte(float value) => (byte)ColorNumerics.ClampToFinite(value + .5F, 0, byte.MaxValue);

    /// <summary>
    /// Converts unassociated scaled RGBA pixels to associated byte magnitudes, with the alpha byte that the destination stores.
    /// </summary>
    /// <remarks>
    /// The components are bounded to [0, 1] first. NaN becomes zero. The alpha byte is floor(alpha * 255 + 0.5).
    /// RGB is multiplied by that alpha byte, so the result is associated and in byte units in one step.
    /// </remarks>
    private readonly struct AssociateToByteOperator : PixelFormats.PixelOperations.IAlphaOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<float> Apply(Vector128<float> source)
        {
            source = ColorNumerics.ClampToFinite(source, Vector128<float>.Zero, Vector128<float>.One);
            Vector128<float> alpha = Vector128_.ShuffleNative(source, 0b_11_11_11_11);
            Vector128<float> storedAlpha = Vector128.Floor((alpha * Vector128.Create((float)byte.MaxValue)) + Vector128.Create(.5F));

            // RGB is associated with the quantized alpha byte. The alpha lane stores that byte.
            Vector128<float> alphaMask = Vector128.Create(0, 0, 0, -1).AsSingle();
            return Vector128.ConditionalSelect(alphaMask, storedAlpha, source * storedAlpha);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<float> Apply(Vector256<float> source)
        {
            source = ColorNumerics.ClampToFinite(source, Vector256<float>.Zero, Vector256<float>.One);
            Vector256<float> alpha = Vector256_.ShuffleNative(source, 0b_11_11_11_11);
            Vector256<float> storedAlpha = Vector256.Floor((alpha * Vector256.Create((float)byte.MaxValue)) + Vector256.Create(.5F));

            // Each four-lane group uses its own alpha byte, as in the 128-bit form.
            Vector256<float> alphaMask = Vector256.Create(0, 0, 0, -1, 0, 0, 0, -1).AsSingle();
            return Vector256.ConditionalSelect(alphaMask, storedAlpha, source * storedAlpha);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<float> Apply(Vector512<float> source)
        {
            source = ColorNumerics.ClampToFinite(source, Vector512<float>.Zero, Vector512<float>.One);
            Vector512<float> alpha = Vector512_.ShuffleNative(source, 0b_11_11_11_11);
            Vector512<float> storedAlpha = Vector512.Floor((alpha * Vector512.Create((float)byte.MaxValue)) + Vector512.Create(.5F));

            // Each four-lane group uses its own alpha byte, as in the 128-bit form.
            Vector512<float> alphaMask = Vector512.Create(0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1).AsSingle();
            return Vector512.ConditionalSelect(alphaMask, storedAlpha, source * storedAlpha);
        }
    }

    /// <summary>
    /// Converts associated scaled RGBA pixels to associated byte magnitudes, with the alpha byte that the destination stores.
    /// </summary>
    /// <remarks>
    /// Alpha is made finite and nonnegative. Zero alpha gives zero for all components.
    /// When alpha * 255 is already a whole byte, RGB is multiplied by 255. This keeps RGB values that are on byte midpoints.
    /// Otherwise, RGB is divided by alpha and multiplied by the stored alpha byte.
    /// Then RGB is bounded to [0, stored alpha], so the stored pixel is a valid associated color.
    /// </remarks>
    private readonly struct ReassociateToByteOperator : PixelFormats.PixelOperations.IAlphaOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<float> Apply(Vector128<float> source)
        {
            Vector128<float> zero = Vector128<float>.Zero;
            Vector128<float> byteMax = Vector128.Create((float)byte.MaxValue);

            // Keep positive finite alpha for the RGB division. Infinity becomes float.MaxValue.
            Vector128<float> alpha = Vector128_.ShuffleNative(source, 0b_11_11_11_11);
            alpha = ColorNumerics.ClampToFinite(alpha, zero, Vector128.Create(float.MaxValue));
            Vector128<float> byteAlpha = alpha * byteMax;
            Vector128<float> storedAlpha = Vector128.Floor(ColorNumerics.ClampToFinite(byteAlpha + Vector128.Create(.5F), zero, byteMax));
            Vector128<float> result = (source / alpha) * storedAlpha;

            Vector128<float> alphaMask = Vector128.Create(0, 0, 0, -1).AsSingle();
            result = Vector128.ConditionalSelect(Vector128.Equals(byteAlpha, storedAlpha), source * byteMax, result);
            result = Vector128.ConditionalSelect(alphaMask, storedAlpha, result);
            result = ColorNumerics.ClampToFinite(result, zero, storedAlpha);
            return Vector128.ConditionalSelect(Vector128.Equals(alpha, zero), zero, result);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<float> Apply(Vector256<float> source)
        {
            Vector256<float> zero = Vector256<float>.Zero;
            Vector256<float> byteMax = Vector256.Create((float)byte.MaxValue);

            // Each four-lane group uses its own alpha, with the same rules as the 128-bit form.
            Vector256<float> alpha = Vector256_.ShuffleNative(source, 0b_11_11_11_11);
            alpha = ColorNumerics.ClampToFinite(alpha, zero, Vector256.Create(float.MaxValue));
            Vector256<float> byteAlpha = alpha * byteMax;
            Vector256<float> storedAlpha = Vector256.Floor(ColorNumerics.ClampToFinite(byteAlpha + Vector256.Create(.5F), zero, byteMax));
            Vector256<float> result = (source / alpha) * storedAlpha;

            Vector256<float> alphaMask = Vector256.Create(0, 0, 0, -1, 0, 0, 0, -1).AsSingle();
            result = Vector256.ConditionalSelect(Vector256.Equals(byteAlpha, storedAlpha), source * byteMax, result);
            result = Vector256.ConditionalSelect(alphaMask, storedAlpha, result);
            result = ColorNumerics.ClampToFinite(result, zero, storedAlpha);
            return Vector256.ConditionalSelect(Vector256.Equals(alpha, zero), zero, result);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<float> Apply(Vector512<float> source)
        {
            Vector512<float> zero = Vector512<float>.Zero;
            Vector512<float> byteMax = Vector512.Create((float)byte.MaxValue);

            // Each four-lane group uses its own alpha, with the same rules as the 128-bit form.
            Vector512<float> alpha = Vector512_.ShuffleNative(source, 0b_11_11_11_11);
            alpha = ColorNumerics.ClampToFinite(alpha, zero, Vector512.Create(float.MaxValue));
            Vector512<float> byteAlpha = alpha * byteMax;
            Vector512<float> storedAlpha = Vector512.Floor(ColorNumerics.ClampToFinite(byteAlpha + Vector512.Create(.5F), zero, byteMax));
            Vector512<float> result = (source / alpha) * storedAlpha;

            Vector512<float> alphaMask = Vector512.Create(0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1).AsSingle();
            result = Vector512.ConditionalSelect(Vector512.Equals(byteAlpha, storedAlpha), source * byteMax, result);
            result = Vector512.ConditionalSelect(alphaMask, storedAlpha, result);
            result = ColorNumerics.ClampToFinite(result, zero, storedAlpha);
            return Vector512.ConditionalSelect(Vector512.Equals(alpha, zero), zero, result);
        }
    }
}
