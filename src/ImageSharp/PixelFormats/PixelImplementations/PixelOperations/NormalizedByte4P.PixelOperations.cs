// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.PixelFormats;

/// <content>
/// Provides optimized overrides for bulk operations.
/// </content>
public partial struct NormalizedByte4P
{
    /// <summary>
    /// Provides optimized bulk operations for <see cref="NormalizedByte4P"/>.
    /// </summary>
    internal class PixelOperations : AssociatedAlphaPixelOperations<NormalizedByte4P>
    {
        private const byte RestorePackedPixelOrder = 0b_11_01_10_00;

        /// <inheritdoc />
        protected override void ToUnassociatedVector4(Configuration configuration, ReadOnlySpan<NormalizedByte4P> source, Span<Vector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            ToUnassociatedVector4(source, destination[..source.Length], false);
        }

        /// <inheritdoc />
        protected override void ToAssociatedVector4(Configuration configuration, ReadOnlySpan<NormalizedByte4P> source, Span<Vector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            ToVector4(source, destination[..source.Length], false);
        }

        /// <inheritdoc />
        protected override void FromUnassociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<NormalizedByte4P> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];
            Associate(source, false);
            Pack(source, destination, true);
        }

        /// <inheritdoc />
        protected override void FromAssociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<NormalizedByte4P> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];
            Reassociate(source, false);
            Pack(source, destination, true);
        }

        /// <inheritdoc />
        protected override void ToUnassociatedScaledVector4(Configuration configuration, ReadOnlySpan<NormalizedByte4P> source, Span<Vector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            ToUnassociatedVector4(source, destination[..source.Length], true);
        }

        /// <inheritdoc />
        protected override void ToAssociatedScaledVector4(Configuration configuration, ReadOnlySpan<NormalizedByte4P> source, Span<Vector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            ToVector4(source, destination[..source.Length], true);
        }

        /// <inheritdoc />
        protected override void FromUnassociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<NormalizedByte4P> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];
            Associate(source, true);
            Pack(source, destination, true);
        }

        /// <inheritdoc />
        protected override void FromAssociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<NormalizedByte4P> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];
            Reassociate(source, true);
            Pack(source, destination, true);
        }

        /// <summary>
        /// Converts packed signed-normalized pixels to native or scaled vectors.
        /// </summary>
        /// <param name="source">The source pixels.</param>
        /// <param name="destination">The destination vectors.</param>
        /// <param name="scaled">Whether to produce scaled vectors.</param>
        internal static void ToVector4(ReadOnlySpan<NormalizedByte4P> source, Span<Vector4> destination, bool scaled)
        {
            ref byte sourceBase = ref Unsafe.As<NormalizedByte4P, byte>(ref MemoryMarshal.GetReference(source));
            ref Vector4 destinationBase = ref MemoryMarshal.GetReference(destination);

            // Each packed component occupies one byte, so the destination Vector4 lane count also defines the source byte stride.
            int componentsPerPixel = Vector128<float>.Count;
            int i = 0;

            // Portable widening advances one integer width at a time, so assemble the wider vectors from ordered 128-bit halves.
            if (Vector512.IsHardwareAccelerated)
            {
                int pixelsPerVector = Vector512<float>.Count / Vector128<float>.Count;

                for (; i <= source.Length - pixelsPerVector; i += pixelsPerVector)
                {
                    Vector128<sbyte> packed = Vector128.LoadUnsafe(ref sourceBase, (nuint)(i * componentsPerPixel)).AsSByte();
                    Vector128<short> lowerShorts = Vector128.WidenLower(packed);
                    Vector128<short> upperShorts = Vector128.WidenUpper(packed);
                    Vector256<int> lowerIntegers = Vector256.Create(Vector128.WidenLower(lowerShorts), Vector128.WidenUpper(lowerShorts));
                    Vector256<int> upperIntegers = Vector256.Create(Vector128.WidenLower(upperShorts), Vector128.WidenUpper(upperShorts));
                    Vector512<int> integers = Vector512.Create(lowerIntegers, upperIntegers);
                    Unsafe.As<Vector4, Vector512<float>>(ref Unsafe.Add(ref destinationBase, (uint)i)) = ToVector4(Vector512.ConvertToSingle(integers), scaled);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                int pixelsPerVector = Vector256<float>.Count / Vector128<float>.Count;

                for (; i <= source.Length - pixelsPerVector; i += pixelsPerVector)
                {
                    ulong packed = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref sourceBase, (uint)(i * componentsPerPixel)));
                    Vector128<short> shorts = Vector128.WidenLower(Vector128.CreateScalarUnsafe(packed).AsSByte());
                    Vector256<int> integers = Vector256.Create(Vector128.WidenLower(shorts), Vector128.WidenUpper(shorts));
                    Unsafe.As<Vector4, Vector256<float>>(ref Unsafe.Add(ref destinationBase, (uint)i)) = ToVector4(Vector256.ConvertToSingle(integers), scaled);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; i < source.Length; i++)
                {
                    uint packed = Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref sourceBase, (uint)(i * componentsPerPixel)));
                    Vector128<short> shorts = Vector128.WidenLower(Vector128.CreateScalarUnsafe(packed).AsSByte());
                    Vector128<int> integers = Vector128.WidenLower(shorts);
                    Unsafe.Add(ref destinationBase, (uint)i) = ToVector4(Vector128.ConvertToSingle(integers), scaled).AsVector4();
                }

                return;
            }

            ref NormalizedByte4P pixelBase = ref Unsafe.As<byte, NormalizedByte4P>(ref sourceBase);

            for (; i < source.Length; i++)
            {
                NormalizedByte4P pixel = Unsafe.Add(ref pixelBase, (uint)i);
                Unsafe.Add(ref destinationBase, (uint)i) = scaled ? pixel.ToScaledVector4() : pixel.ToVector4();
            }
        }

        /// <summary>
        /// Converts signed-normalized storage to native or scaled vectors.
        /// </summary>
        /// <param name="source">The signed storage components.</param>
        /// <param name="scaled">Whether to produce scaled vectors.</param>
        /// <returns>The converted vectors.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<float> ToVector4(Vector512<float> source, bool scaled)
        {
            // Both minimum two's-complement SNORM encodings represent -1.
            source = Vector512.Max(source, Vector512.Create(-MaxPos));

            if (scaled)
            {
                // Offset exact integer components before division to avoid cancellation near the signed-normalized lower bound.
                return (source + Vector512.Create(MaxPos)) / Vector512.Create(ScaledMagnitude);
            }

            return source / Vector512.Create(MaxPos);
        }

        /// <summary>
        /// Converts signed-normalized storage to native or scaled vectors.
        /// </summary>
        /// <param name="source">The signed storage components.</param>
        /// <param name="scaled">Whether to produce scaled vectors.</param>
        /// <returns>The converted vectors.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<float> ToVector4(Vector256<float> source, bool scaled)
        {
            // Both minimum two's-complement SNORM encodings represent -1.
            source = Vector256.Max(source, Vector256.Create(-MaxPos));

            if (scaled)
            {
                // Offset exact integer components before division to avoid cancellation near the signed-normalized lower bound.
                return (source + Vector256.Create(MaxPos)) / Vector256.Create(ScaledMagnitude);
            }

            return source / Vector256.Create(MaxPos);
        }

        /// <summary>
        /// Converts signed-normalized storage to native or scaled vectors.
        /// </summary>
        /// <param name="source">The signed storage components.</param>
        /// <param name="scaled">Whether to produce scaled vectors.</param>
        /// <returns>The converted vectors.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<float> ToVector4(Vector128<float> source, bool scaled)
        {
            // Both minimum two's-complement SNORM encodings represent -1.
            source = Vector128.Max(source, Vector128.Create(-MaxPos));

            if (scaled)
            {
                // Offset exact integer components before division to avoid cancellation near the signed-normalized lower bound.
                return (source + Vector128.Create(MaxPos)) / Vector128.Create(ScaledMagnitude);
            }

            return source / Vector128.Create(MaxPos);
        }

        /// <summary>
        /// Converts packed associated pixels directly to unassociated native or scaled vectors.
        /// </summary>
        /// <param name="source">The source pixels.</param>
        /// <param name="destination">The destination vectors.</param>
        /// <param name="scaled">Whether to produce scaled vectors.</param>
        private static void ToUnassociatedVector4(ReadOnlySpan<NormalizedByte4P> source, Span<Vector4> destination, bool scaled)
        {
            ref byte sourceBase = ref Unsafe.As<NormalizedByte4P, byte>(ref MemoryMarshal.GetReference(source));
            ref Vector4 destinationBase = ref MemoryMarshal.GetReference(destination);

            // Each packed component occupies one byte, so the destination Vector4 lane count also defines the source byte stride.
            int componentsPerPixel = Vector128<float>.Count;
            int i = 0;

            // Portable widening advances one integer width at a time, so assemble the wider vectors from ordered 128-bit halves.
            if (Vector512.IsHardwareAccelerated)
            {
                int pixelsPerVector = Vector512<float>.Count / Vector128<float>.Count;

                for (; i <= source.Length - pixelsPerVector; i += pixelsPerVector)
                {
                    Vector128<sbyte> packed = Vector128.LoadUnsafe(ref sourceBase, (nuint)(i * componentsPerPixel)).AsSByte();
                    Vector128<short> lowerShorts = Vector128.WidenLower(packed);
                    Vector128<short> upperShorts = Vector128.WidenUpper(packed);
                    Vector256<int> lowerIntegers = Vector256.Create(Vector128.WidenLower(lowerShorts), Vector128.WidenUpper(lowerShorts));
                    Vector256<int> upperIntegers = Vector256.Create(Vector128.WidenLower(upperShorts), Vector128.WidenUpper(upperShorts));
                    Vector512<int> integers = Vector512.Create(lowerIntegers, upperIntegers);
                    Unsafe.As<Vector4, Vector512<float>>(ref Unsafe.Add(ref destinationBase, (uint)i)) = ToUnassociatedVector4(Vector512.ConvertToSingle(integers), scaled);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                int pixelsPerVector = Vector256<float>.Count / Vector128<float>.Count;

                for (; i <= source.Length - pixelsPerVector; i += pixelsPerVector)
                {
                    ulong packed = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref sourceBase, (uint)(i * componentsPerPixel)));
                    Vector128<short> shorts = Vector128.WidenLower(Vector128.CreateScalarUnsafe(packed).AsSByte());
                    Vector256<int> integers = Vector256.Create(Vector128.WidenLower(shorts), Vector128.WidenUpper(shorts));
                    Unsafe.As<Vector4, Vector256<float>>(ref Unsafe.Add(ref destinationBase, (uint)i)) = ToUnassociatedVector4(Vector256.ConvertToSingle(integers), scaled);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; i < source.Length; i++)
                {
                    uint packed = Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref sourceBase, (uint)(i * componentsPerPixel)));
                    Vector128<short> shorts = Vector128.WidenLower(Vector128.CreateScalarUnsafe(packed).AsSByte());
                    Vector128<int> integers = Vector128.WidenLower(shorts);
                    Unsafe.Add(ref destinationBase, (uint)i) = ToUnassociatedVector4(Vector128.ConvertToSingle(integers), scaled).AsVector4();
                }

                return;
            }

            ref NormalizedByte4P pixelBase = ref Unsafe.As<byte, NormalizedByte4P>(ref sourceBase);

            for (; i < source.Length; i++)
            {
                NormalizedByte4P pixel = Unsafe.Add(ref pixelBase, (uint)i);
                Unsafe.Add(ref destinationBase, (uint)i) = scaled ? pixel.ToUnassociatedScaledVector4() : pixel.ToUnassociatedVector4();
            }
        }

        /// <summary>
        /// Converts four associated signed-byte vectors to unassociated native or scaled vectors.
        /// </summary>
        /// <param name="source">The signed storage components.</param>
        /// <param name="scaled">Whether to produce scaled vectors.</param>
        /// <returns>The unassociated vectors.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<float> ToUnassociatedVector4(Vector512<float> source, bool scaled)
        {
            // Clamp the duplicate SNORM minimum encoding before converting it to the nonnegative associated domain.
            source = Vector512.Max(source, Vector512.Create(-MaxPos));
            source += Vector512.Create(MaxPos);
            Vector512<float> alpha = Vector512_.ShuffleNative(source, 0b_11_11_11_11);
            Vector512<float> alphaMask = Vector512.Create(0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1).AsSingle();
            Vector512<float> normalizeMask = Vector512.Equals(alpha, Vector512<float>.Zero) | alphaMask;
            Vector512<float> divisor = Vector512.ConditionalSelect(normalizeMask, Vector512.Create(ScaledMagnitude), alpha);
            Vector512<float> result = source / divisor;

            // Unassociation is performed in scaled space; map the result back only after alpha has served as a true opacity value.
            return scaled ? result : (result * Vector512.Create(2F)) - Vector512<float>.One;
        }

        /// <summary>
        /// Converts two associated signed-byte vectors to unassociated native or scaled vectors.
        /// </summary>
        /// <param name="source">The signed storage components.</param>
        /// <param name="scaled">Whether to produce scaled vectors.</param>
        /// <returns>The unassociated vectors.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<float> ToUnassociatedVector4(Vector256<float> source, bool scaled)
        {
            // Clamp the duplicate SNORM minimum encoding before converting it to the nonnegative associated domain.
            source = Vector256.Max(source, Vector256.Create(-MaxPos));
            source += Vector256.Create(MaxPos);
            Vector256<float> alpha = Vector256_.ShuffleNative(source, 0b_11_11_11_11);
            Vector256<float> alphaMask = Vector256.Create(0, 0, 0, -1, 0, 0, 0, -1).AsSingle();
            Vector256<float> normalizeMask = Vector256.Equals(alpha, Vector256<float>.Zero) | alphaMask;
            Vector256<float> divisor = Vector256.ConditionalSelect(normalizeMask, Vector256.Create(ScaledMagnitude), alpha);
            Vector256<float> result = source / divisor;

            // Unassociation is performed in scaled space; map the result back only after alpha has served as a true opacity value.
            return scaled ? result : (result * Vector256.Create(2F)) - Vector256<float>.One;
        }

        /// <summary>
        /// Converts an associated signed-byte vector to an unassociated native or scaled vector.
        /// </summary>
        /// <param name="source">The signed storage components.</param>
        /// <param name="scaled">Whether to produce a scaled vector.</param>
        /// <returns>The unassociated vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<float> ToUnassociatedVector4(Vector128<float> source, bool scaled)
        {
            // Clamp the duplicate SNORM minimum encoding before converting it to the nonnegative associated domain.
            source = Vector128.Max(source, Vector128.Create(-MaxPos));
            source += Vector128.Create(MaxPos);
            Vector128<float> alpha = Vector128_.ShuffleNative(source, 0b_11_11_11_11);
            Vector128<float> alphaMask = Vector128.Create(0, 0, 0, -1).AsSingle();
            Vector128<float> normalizeMask = Vector128.Equals(alpha, Vector128<float>.Zero) | alphaMask;
            Vector128<float> divisor = Vector128.ConditionalSelect(normalizeMask, Vector128.Create(ScaledMagnitude), alpha);
            Vector128<float> result = source / divisor;

            // Unassociation is performed in scaled space; map the result back only after alpha has served as a true opacity value.
            return scaled ? result : (result * Vector128.Create(2F)) - Vector128<float>.One;
        }

        /// <summary>
        /// Converts unassociated native or scaled vectors to the destination's associated scaled representation.
        /// </summary>
        /// <param name="source">The vectors to convert in place.</param>
        /// <param name="scaled">Whether the source contains scaled vectors.</param>
        private static void Associate(Span<Vector4> source, bool scaled)
        {
            // Quantize alpha through signed-normalized destination storage before associating RGB. The kernels replace W separately
            // so alpha is stored unchanged instead of being multiplied by itself.
            if (Vector512.IsHardwareAccelerated)
            {
                int pixelsPerVector = Vector512<float>.Count / Vector128<float>.Count;
                ref Vector512<float> sourceBase = ref Unsafe.As<Vector4, Vector512<float>>(ref MemoryMarshal.GetReference(source));
                ref Vector512<float> sourceEnd = ref Unsafe.Add(ref sourceBase, (uint)source.Length / (uint)pixelsPerVector);

                while (Unsafe.IsAddressLessThan(ref sourceBase, ref sourceEnd))
                {
                    sourceBase = Associate(sourceBase, scaled);
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
                    sourceBase = Associate(sourceBase, scaled);
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
                    sourceBase = Associate(sourceBase, scaled);
                    sourceBase = ref Unsafe.Add(ref sourceBase, 1);
                }

                return;
            }

            ref Vector4 tailBase = ref MemoryMarshal.GetReference(source);

            for (nuint i = 0; i < (uint)source.Length; i++)
            {
                Vector4 vector = Unsafe.Add(ref tailBase, i);

                if (!scaled)
                {
                    // Signed-native W is an affine encoding rather than opacity, so association must happen after mapping to scaled space.
                    vector += Vector4.One;
                    vector /= 2F;
                }

                // Qualify the containing pixel type because this nested class also declares SIMD helpers named Associate.
                Unsafe.Add(ref tailBase, i) = NormalizedByte4P.Associate(vector);
            }
        }

        /// <summary>
        /// Converts four unassociated native or scaled vectors to the destination's associated scaled representation.
        /// </summary>
        /// <param name="source">The unassociated vectors.</param>
        /// <param name="scaled">Whether the source contains scaled vectors.</param>
        /// <returns>The associated vectors.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<float> Associate(Vector512<float> source, bool scaled)
        {
            Vector512<float> zero = Vector512<float>.Zero;
            Vector512<float> one = Vector512<float>.One;

            if (!scaled)
            {
                // Signed-native W is an affine encoding rather than opacity, so association must happen after mapping to scaled space.
                source = (source + one) / Vector512.Create(2F);
            }

            source = Vector512.Min(Vector512.Max(source, zero), one);
            Vector512<float> alpha = Vector512_.ShuffleNative(source, 0b_11_11_11_11);
            Vector512<float> nativeAlpha = Vector512.Min(Vector512.Max((alpha * Vector512.Create(2F)) - one, -one), one);
            Vector512<float> storedAlpha = Vector512_.RoundToNearestInteger(nativeAlpha * Vector512.Create(MaxPos));
            storedAlpha += Vector512.Create(MaxPos);
            storedAlpha /= Vector512.Create(ScaledMagnitude);
            Vector512<float> result = source * storedAlpha;
            Vector512<float> alphaMask = Vector512.Create(0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1).AsSingle();
            return Vector512.ConditionalSelect(alphaMask, storedAlpha, result);
        }

        /// <summary>
        /// Converts two unassociated native or scaled vectors to the destination's associated scaled representation.
        /// </summary>
        /// <param name="source">The unassociated vectors.</param>
        /// <param name="scaled">Whether the source contains scaled vectors.</param>
        /// <returns>The associated vectors.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<float> Associate(Vector256<float> source, bool scaled)
        {
            Vector256<float> zero = Vector256<float>.Zero;
            Vector256<float> one = Vector256<float>.One;

            if (!scaled)
            {
                // Signed-native W is an affine encoding rather than opacity, so association must happen after mapping to scaled space.
                source = (source + one) / Vector256.Create(2F);
            }

            source = Vector256.Min(Vector256.Max(source, zero), one);
            Vector256<float> alpha = Vector256_.ShuffleNative(source, 0b_11_11_11_11);
            Vector256<float> nativeAlpha = Vector256.Min(Vector256.Max((alpha * Vector256.Create(2F)) - one, -one), one);
            Vector256<float> storedAlpha = Vector256_.RoundToNearestInteger(nativeAlpha * Vector256.Create(MaxPos));
            storedAlpha += Vector256.Create(MaxPos);
            storedAlpha /= Vector256.Create(ScaledMagnitude);
            Vector256<float> result = source * storedAlpha;
            Vector256<float> alphaMask = Vector256.Create(0, 0, 0, -1, 0, 0, 0, -1).AsSingle();
            return Vector256.ConditionalSelect(alphaMask, storedAlpha, result);
        }

        /// <summary>
        /// Converts an unassociated native or scaled vector to the destination's associated scaled representation.
        /// </summary>
        /// <param name="source">The unassociated vector.</param>
        /// <param name="scaled">Whether the source contains a scaled vector.</param>
        /// <returns>The associated vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<float> Associate(Vector128<float> source, bool scaled)
        {
            Vector128<float> zero = Vector128<float>.Zero;
            Vector128<float> one = Vector128<float>.One;

            if (!scaled)
            {
                // Signed-native W is an affine encoding rather than opacity, so association must happen after mapping to scaled space.
                source = (source + one) / Vector128.Create(2F);
            }

            source = Vector128.Min(Vector128.Max(source, zero), one);
            Vector128<float> alpha = Vector128_.ShuffleNative(source, 0b_11_11_11_11);
            Vector128<float> nativeAlpha = Vector128.Min(Vector128.Max((alpha * Vector128.Create(2F)) - one, -one), one);
            Vector128<float> storedAlpha = Vector128_.RoundToNearestInteger(nativeAlpha * Vector128.Create(MaxPos));
            storedAlpha += Vector128.Create(MaxPos);
            storedAlpha /= Vector128.Create(ScaledMagnitude);
            Vector128<float> result = source * storedAlpha;
            Vector128<float> alphaMask = Vector128.Create(0, 0, 0, -1).AsSingle();
            return Vector128.ConditionalSelect(alphaMask, storedAlpha, result);
        }

        /// <summary>
        /// Reassociates native or scaled vectors with the scaled alpha values the destination stores.
        /// </summary>
        /// <param name="source">The vectors to convert in place.</param>
        /// <param name="scaled">Whether the source contains scaled vectors.</param>
        private static void Reassociate(Span<Vector4> source, bool scaled)
        {
            // Preserve straight color across destination-alpha quantization by scaling RGB by storedAlpha / inputAlpha. The kernels
            // replace W, clamp RGB to stored alpha, and clear zero-alpha vectors so the stored result remains valid associated color.
            if (Vector512.IsHardwareAccelerated)
            {
                int pixelsPerVector = Vector512<float>.Count / Vector128<float>.Count;
                ref Vector512<float> sourceBase = ref Unsafe.As<Vector4, Vector512<float>>(ref MemoryMarshal.GetReference(source));
                ref Vector512<float> sourceEnd = ref Unsafe.Add(ref sourceBase, (uint)source.Length / (uint)pixelsPerVector);

                while (Unsafe.IsAddressLessThan(ref sourceBase, ref sourceEnd))
                {
                    sourceBase = Reassociate(sourceBase, scaled);
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
                    sourceBase = Reassociate(sourceBase, scaled);
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
                    sourceBase = Reassociate(sourceBase, scaled);
                    sourceBase = ref Unsafe.Add(ref sourceBase, 1);
                }

                return;
            }

            ref Vector4 tailBase = ref MemoryMarshal.GetReference(source);

            for (nuint i = 0; i < (uint)source.Length; i++)
            {
                Vector4 vector = Unsafe.Add(ref tailBase, i);

                if (!scaled)
                {
                    // Convert native components together so RGB and alpha enter reassociation in the same scaled coordinate system.
                    vector += Vector4.One;
                    vector /= 2F;
                }

                // Qualify the containing pixel type because this nested class also declares SIMD helpers named Reassociate.
                Unsafe.Add(ref tailBase, i) = NormalizedByte4P.Reassociate(vector);
            }
        }

        /// <summary>
        /// Reassociates four native or scaled vectors with the scaled alpha values the destination stores.
        /// </summary>
        /// <param name="source">The associated vectors.</param>
        /// <param name="scaled">Whether the source contains scaled vectors.</param>
        /// <returns>The reassociated vectors.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<float> Reassociate(Vector512<float> source, bool scaled)
        {
            Vector512<float> zero = Vector512<float>.Zero;
            Vector512<float> one = Vector512<float>.One;

            if (!scaled)
            {
                // Convert native components together so RGB and alpha enter reassociation in the same scaled coordinate system.
                source = (source + one) / Vector512.Create(2F);
            }

            Vector512<float> alpha = Vector512_.ShuffleNative(source, 0b_11_11_11_11);
            Vector512<float> nativeAlpha = Vector512.Min(Vector512.Max((alpha * Vector512.Create(2F)) - one, -one), one);
            Vector512<float> storedAlpha = Vector512_.RoundToNearestInteger(nativeAlpha * Vector512.Create(MaxPos));
            storedAlpha += Vector512.Create(MaxPos);
            storedAlpha /= Vector512.Create(ScaledMagnitude);
            Vector512<float> result = source * (storedAlpha / alpha);
            Vector512<float> alphaMask = Vector512.Create(0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1).AsSingle();
            result = Vector512.ConditionalSelect(alphaMask, storedAlpha, result);
            result = Vector512.Min(Vector512.Max(result, zero), storedAlpha);
            return Vector512.ConditionalSelect(Vector512.LessThanOrEqual(alpha, zero), zero, result);
        }

        /// <summary>
        /// Reassociates two native or scaled vectors with the scaled alpha values the destination stores.
        /// </summary>
        /// <param name="source">The associated vectors.</param>
        /// <param name="scaled">Whether the source contains scaled vectors.</param>
        /// <returns>The reassociated vectors.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<float> Reassociate(Vector256<float> source, bool scaled)
        {
            Vector256<float> zero = Vector256<float>.Zero;
            Vector256<float> one = Vector256<float>.One;

            if (!scaled)
            {
                // Convert native components together so RGB and alpha enter reassociation in the same scaled coordinate system.
                source = (source + one) / Vector256.Create(2F);
            }

            Vector256<float> alpha = Vector256_.ShuffleNative(source, 0b_11_11_11_11);
            Vector256<float> nativeAlpha = Vector256.Min(Vector256.Max((alpha * Vector256.Create(2F)) - one, -one), one);
            Vector256<float> storedAlpha = Vector256_.RoundToNearestInteger(nativeAlpha * Vector256.Create(MaxPos));
            storedAlpha += Vector256.Create(MaxPos);
            storedAlpha /= Vector256.Create(ScaledMagnitude);
            Vector256<float> result = source * (storedAlpha / alpha);
            Vector256<float> alphaMask = Vector256.Create(0, 0, 0, -1, 0, 0, 0, -1).AsSingle();
            result = Vector256.ConditionalSelect(alphaMask, storedAlpha, result);
            result = Vector256.Min(Vector256.Max(result, zero), storedAlpha);
            return Vector256.ConditionalSelect(Vector256.LessThanOrEqual(alpha, zero), zero, result);
        }

        /// <summary>
        /// Reassociates a native or scaled vector with the scaled alpha value the destination stores.
        /// </summary>
        /// <param name="source">The associated vector.</param>
        /// <param name="scaled">Whether the source contains a scaled vector.</param>
        /// <returns>The reassociated vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<float> Reassociate(Vector128<float> source, bool scaled)
        {
            Vector128<float> zero = Vector128<float>.Zero;
            Vector128<float> one = Vector128<float>.One;

            if (!scaled)
            {
                // Convert native components together so RGB and alpha enter reassociation in the same scaled coordinate system.
                source = (source + one) / Vector128.Create(2F);
            }

            Vector128<float> alpha = Vector128_.ShuffleNative(source, 0b_11_11_11_11);
            Vector128<float> nativeAlpha = Vector128.Min(Vector128.Max((alpha * Vector128.Create(2F)) - one, -one), one);
            Vector128<float> storedAlpha = Vector128_.RoundToNearestInteger(nativeAlpha * Vector128.Create(MaxPos));
            storedAlpha += Vector128.Create(MaxPos);
            storedAlpha /= Vector128.Create(ScaledMagnitude);
            Vector128<float> result = source * (storedAlpha / alpha);
            Vector128<float> alphaMask = Vector128.Create(0, 0, 0, -1).AsSingle();
            result = Vector128.ConditionalSelect(alphaMask, storedAlpha, result);
            result = Vector128.Min(Vector128.Max(result, zero), storedAlpha);
            return Vector128.ConditionalSelect(Vector128.LessThanOrEqual(alpha, zero), zero, result);
        }

        /// <summary>
        /// Packs native or scaled vectors into signed-normalized storage.
        /// </summary>
        /// <param name="source">The source vectors.</param>
        /// <param name="destination">The destination pixels.</param>
        /// <param name="scaled">Whether the source contains scaled vectors.</param>
        internal static void Pack(Span<Vector4> source, Span<NormalizedByte4P> destination, bool scaled)
        {
            ref Vector4 sourceBase = ref MemoryMarshal.GetReference(source);
            ref NormalizedByte4P destinationBase = ref MemoryMarshal.GetReference(destination);
            int i = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                int pixelsPerVector = Vector512<float>.Count / Vector128<float>.Count;

                for (; i <= source.Length - pixelsPerVector; i += pixelsPerVector)
                {
                    Vector512<float> vector = Unsafe.As<Vector4, Vector512<float>>(ref Unsafe.Add(ref sourceBase, (uint)i));
                    Vector512<int> integers = ConvertToPackedInt32(vector, scaled);
                    Vector256<short> shorts = Vector256_.PackSignedSaturate(integers.GetLower(), integers.GetUpper());
                    Vector128<sbyte> packed = Vector128_.PackSignedSaturate(shorts.GetLower(), shorts.GetUpper());

                    if (Avx2.IsSupported)
                    {
                        // AVX2 narrows each 128-bit lane independently, placing the middle two pixels out of order.
                        packed = Vector128_.ShuffleNative(packed.AsSingle(), RestorePackedPixelOrder).AsSByte();
                    }

                    Unsafe.As<NormalizedByte4P, Vector128<sbyte>>(ref Unsafe.Add(ref destinationBase, (uint)i)) = packed;
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                int pixelsPerVector = Vector256<float>.Count / Vector128<float>.Count;

                for (; i <= source.Length - pixelsPerVector; i += pixelsPerVector)
                {
                    Vector256<float> vector = Unsafe.As<Vector4, Vector256<float>>(ref Unsafe.Add(ref sourceBase, (uint)i));
                    Vector256<int> integers = ConvertToPackedInt32(vector, scaled);
                    Vector128<short> shorts = Vector128_.PackSignedSaturate(integers.GetLower(), integers.GetUpper());
                    Vector128<sbyte> packed = Vector128_.PackSignedSaturate(shorts, shorts);
                    Unsafe.As<NormalizedByte4P, ulong>(ref Unsafe.Add(ref destinationBase, (uint)i)) = packed.AsUInt64().GetElement(0);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; i < source.Length; i++)
                {
                    Vector128<float> vector = Unsafe.As<Vector4, Vector128<float>>(ref Unsafe.Add(ref sourceBase, (uint)i));
                    Vector128<int> integers = ConvertToPackedInt32(vector, scaled);
                    Vector128<short> shorts = Vector128_.PackSignedSaturate(integers, integers);
                    Vector128<sbyte> packed = Vector128_.PackSignedSaturate(shorts, shorts);
                    Unsafe.Add(ref destinationBase, (uint)i).PackedValue = packed.AsUInt32().GetElement(0);
                }

                return;
            }

            for (; i < source.Length; i++)
            {
                Vector4 vector = Unsafe.Add(ref sourceBase, (uint)i);
                Unsafe.Add(ref destinationBase, (uint)i) = scaled ? FromScaledVector4(vector) : FromVector4(vector);
            }
        }

        /// <summary>
        /// Converts four vectors to signed integers using the destination's packing contract.
        /// </summary>
        /// <param name="source">The source vectors.</param>
        /// <param name="scaled">Whether the source contains scaled vectors.</param>
        /// <returns>The packed integer components.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<int> ConvertToPackedInt32(Vector512<float> source, bool scaled)
        {
            Vector512<float> one = Vector512<float>.One;

            if (scaled)
            {
                source *= Vector512.Create(2F);
                source -= one;
            }

            source = Vector512.Min(Vector512.Max(source, -one), one) * Vector512.Create(MaxPos);
            return Vector512_.ConvertToInt32RoundToEven(source);
        }

        /// <summary>
        /// Converts two vectors to signed integers using the destination's packing contract.
        /// </summary>
        /// <param name="source">The source vectors.</param>
        /// <param name="scaled">Whether the source contains scaled vectors.</param>
        /// <returns>The packed integer components.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<int> ConvertToPackedInt32(Vector256<float> source, bool scaled)
        {
            Vector256<float> one = Vector256<float>.One;

            if (scaled)
            {
                source *= Vector256.Create(2F);
                source -= one;
            }

            source = Vector256.Min(Vector256.Max(source, -one), one) * Vector256.Create(MaxPos);
            return Vector256_.ConvertToInt32RoundToEven(source);
        }

        /// <summary>
        /// Converts a vector to signed integers using the destination's packing contract.
        /// </summary>
        /// <param name="source">The source vector.</param>
        /// <param name="scaled">Whether the source contains a scaled vector.</param>
        /// <returns>The packed integer components.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<int> ConvertToPackedInt32(Vector128<float> source, bool scaled)
        {
            Vector128<float> one = Vector128<float>.One;

            if (scaled)
            {
                source *= Vector128.Create(2F);
                source -= one;
            }

            source = Vector128.Min(Vector128.Max(source, -one), one) * Vector128.Create(MaxPos);
            return Vector128_.ConvertToInt32RoundToEven(source);
        }
    }
}
