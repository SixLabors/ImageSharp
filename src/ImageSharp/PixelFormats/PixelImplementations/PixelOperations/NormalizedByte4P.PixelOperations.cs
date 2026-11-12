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
    internal class PixelOperations : PixelOperations<NormalizedByte4P>
    {
        private const byte RestorePackedPixelOrder = 0b_11_01_10_00;

        /// <summary>
        /// Converts an unassociated scaled vector to the associated representation of a signed-normalized-byte destination.
        /// </summary>
        /// <param name="source">The unassociated scaled vector.</param>
        /// <returns>The associated scaled vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 Associate(Vector4 source)
            => StorageAssociateOperator.Apply(source.AsVector128()).AsVector4();

        /// <summary>
        /// Reassociates a scaled vector with the alpha value the destination stores.
        /// </summary>
        /// <param name="source">The associated scaled vector.</param>
        /// <returns>The reassociated scaled vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 Reassociate(Vector4 source)
            => StorageReassociateOperator.Apply(source.AsVector128()).AsVector4();

        /// <inheritdoc />
        public override void ToUnassociatedVector4(Configuration configuration, ReadOnlySpan<NormalizedByte4P> source, Span<Vector4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            ToUnassociatedVector4(source, destination[..source.Length], false);
        }

        /// <inheritdoc />
        public override void ToAssociatedVector4(Configuration configuration, ReadOnlySpan<NormalizedByte4P> source, Span<Vector4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            ToVector4<PixelFormats.PixelOperations.NoAlphaOperator>(source, destination[..source.Length], false);
        }

        /// <inheritdoc />
        public override void FromUnassociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<NormalizedByte4P> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            Pack<StorageAssociateOperator>(source, destination[..source.Length], false);
        }

        /// <inheritdoc />
        public override void FromAssociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<NormalizedByte4P> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            Pack<StorageReassociateOperator>(source, destination[..source.Length], false);
        }

        /// <inheritdoc />
        public override void ToUnassociatedScaledVector4(Configuration configuration, ReadOnlySpan<NormalizedByte4P> source, Span<Vector4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            ToUnassociatedVector4(source, destination[..source.Length], true);
        }

        /// <inheritdoc />
        public override void ToAssociatedScaledVector4(Configuration configuration, ReadOnlySpan<NormalizedByte4P> source, Span<Vector4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            ToVector4<PixelFormats.PixelOperations.NoAlphaOperator>(source, destination[..source.Length], true);
        }

        /// <inheritdoc />
        internal override void ToAssociatedScaledVector4<TAlpha>(
            Configuration configuration,
            ReadOnlySpan<NormalizedByte4P> source,
            Span<Vector4> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            ToVector4<TAlpha>(source, destination[..source.Length], true);
        }

        /// <inheritdoc />
        public override void FromUnassociatedScaledVector4Destructive(
            Configuration configuration,
            Span<Vector4> source,
            Span<NormalizedByte4P> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            Pack<StorageAssociateOperator>(source, destination[..source.Length], true);
        }

        /// <inheritdoc />
        public override void FromAssociatedScaledVector4Destructive(
            Configuration configuration,
            Span<Vector4> source,
            Span<NormalizedByte4P> destination)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            Pack<StorageReassociateOperator>(source, destination[..source.Length], true);
        }

        /// <summary>
        /// Unpacks signed-normalized storage to native or scaled vectors, and applies <typeparamref name="TAlpha"/> in the same pass.
        /// </summary>
        /// <remarks>
        /// The alpha step uses scaled vectors. With an alpha step, the components are unpacked to the scaled range, the step is
        /// applied, and native output is mapped back with native = (scaled * 2) - 1.
        /// </remarks>
        /// <typeparam name="TAlpha">The alpha step for each pixel.</typeparam>
        /// <param name="source">The source pixels.</param>
        /// <param name="destination">The destination vectors.</param>
        /// <param name="scaled">Whether to produce scaled vectors.</param>
        public static void ToVector4<TAlpha>(ReadOnlySpan<NormalizedByte4P> source, Span<Vector4> destination, bool scaled)
            where TAlpha : struct, PixelFormats.PixelOperations.IAlphaOperator
        {
            ref byte sourceBase = ref Unsafe.As<NormalizedByte4P, byte>(ref MemoryMarshal.GetReference(source));
            ref Vector4 destinationBase = ref MemoryMarshal.GetReference(destination);
            bool alphaStep = typeof(TAlpha) != typeof(PixelFormats.PixelOperations.NoAlphaOperator);
            bool decodeScaled = scaled || alphaStep;
            bool mapToNative = alphaStep && !scaled;

            int i = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                int pixelsPerVector = Vector512<float>.Count / Vector128<float>.Count;

                for (; i <= source.Length - pixelsPerVector; i += pixelsPerVector)
                {
                    LoadCodes(ref sourceBase, i, out Vector512<float> codes);
                    Vector512<float> vector = TAlpha.Apply(ToVector4(codes, decodeScaled));

                    if (mapToNative)
                    {
                        vector = Vector512.MultiplyAddEstimate(vector, Vector512.Create(2F), -Vector512<float>.One);
                    }

                    Unsafe.As<Vector4, Vector512<float>>(ref Unsafe.Add(ref destinationBase, (uint)i)) = vector;
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                int pixelsPerVector = Vector256<float>.Count / Vector128<float>.Count;

                for (; i <= source.Length - pixelsPerVector; i += pixelsPerVector)
                {
                    LoadCodes(ref sourceBase, i, out Vector256<float> codes);
                    Vector256<float> vector = TAlpha.Apply(ToVector4(codes, decodeScaled));

                    if (mapToNative)
                    {
                        vector = Vector256.MultiplyAddEstimate(vector, Vector256.Create(2F), -Vector256<float>.One);
                    }

                    Unsafe.As<Vector4, Vector256<float>>(ref Unsafe.Add(ref destinationBase, (uint)i)) = vector;
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; i < source.Length; i++)
                {
                    LoadCodes(ref sourceBase, i, out Vector128<float> codes);
                    Vector128<float> vector = TAlpha.Apply(ToVector4(codes, decodeScaled));

                    if (mapToNative)
                    {
                        vector = Vector128.MultiplyAddEstimate(vector, Vector128.Create(2F), -Vector128<float>.One);
                    }

                    Unsafe.Add(ref destinationBase, (uint)i) = vector.AsVector4();
                }

                return;
            }

            ref NormalizedByte4P pixelBase = ref Unsafe.As<byte, NormalizedByte4P>(ref sourceBase);

            for (; i < source.Length; i++)
            {
                NormalizedByte4P pixel = Unsafe.Add(ref pixelBase, (uint)i);
                Vector4 vector = decodeScaled ? pixel.ToScaledVector4() : pixel.ToVector4();
                vector = TAlpha.Apply(vector.AsVector128()).AsVector4();

                if (mapToNative)
                {
                    vector = Vector4.MultiplyAddEstimate(vector, new Vector4(2F), -Vector4.One);
                }

                Unsafe.Add(ref destinationBase, (uint)i) = vector;
            }
        }

        /// <summary>
        /// Loads four packed pixels and converts their signed codes to floats.
        /// </summary>
        /// <param name="source">The first byte of the packed pixels.</param>
        /// <param name="pixel">The index of the first pixel to load.</param>
        /// <param name="codes">The sixteen signed codes, in RGBA order for each pixel.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void LoadCodes(ref byte source, int pixel, out Vector512<float> codes)
        {
            // Each pixel is four bytes. One 16-byte load supplies four pixels, and sign extension keeps negative codes.
            Vector128<sbyte> packed = Vector128.LoadUnsafe(ref source, (nuint)(pixel * 4)).AsSByte();
            codes = Vector512.ConvertToSingle(Vector512_.Widen(packed));
        }

        /// <summary>
        /// Loads two packed pixels and converts their signed codes to floats.
        /// </summary>
        /// <param name="source">The first byte of the packed pixels.</param>
        /// <param name="pixel">The index of the first pixel to load.</param>
        /// <param name="codes">The eight signed codes, in RGBA order for each pixel.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void LoadCodes(ref byte source, int pixel, out Vector256<float> codes)
        {
            // An 8-byte load reads exactly two pixels, so the last pair of a row reads no padding.
            Vector64<sbyte> packed = Vector64.LoadUnsafe(ref source, (nuint)(pixel * 4)).AsSByte();
            codes = Vector256.ConvertToSingle(Vector256_.Widen(packed));
        }

        /// <summary>
        /// Loads one packed pixel and converts its signed codes to floats.
        /// </summary>
        /// <param name="source">The first byte of the packed pixels.</param>
        /// <param name="pixel">The index of the pixel to load.</param>
        /// <param name="codes">The four signed codes, in RGBA order.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void LoadCodes(ref byte source, int pixel, out Vector128<float> codes)
        {
            // A 4-byte load reads exactly one pixel, so the last pixel of a row reads no padding.
            uint packed = Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref source, (uint)(pixel * 4)));
            Vector128<short> shorts = Vector128.WidenLower(Vector128.CreateScalarUnsafe(packed).AsSByte());
            codes = Vector128.ConvertToSingle(Vector128.WidenLower(shorts));
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

            int i = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                int pixelsPerVector = Vector512<float>.Count / Vector128<float>.Count;

                for (; i <= source.Length - pixelsPerVector; i += pixelsPerVector)
                {
                    LoadCodes(ref sourceBase, i, out Vector512<float> codes);
                    Unsafe.As<Vector4, Vector512<float>>(ref Unsafe.Add(ref destinationBase, (uint)i)) =
                        ToUnassociatedVector4(codes, scaled);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                int pixelsPerVector = Vector256<float>.Count / Vector128<float>.Count;

                for (; i <= source.Length - pixelsPerVector; i += pixelsPerVector)
                {
                    LoadCodes(ref sourceBase, i, out Vector256<float> codes);
                    Unsafe.As<Vector4, Vector256<float>>(ref Unsafe.Add(ref destinationBase, (uint)i)) =
                        ToUnassociatedVector4(codes, scaled);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; i < source.Length; i++)
                {
                    LoadCodes(ref sourceBase, i, out Vector128<float> codes);
                    Unsafe.Add(ref destinationBase, (uint)i) = ToUnassociatedVector4(codes, scaled).AsVector4();
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
            Vector512<float> result = Numerics.UnPremultiply(source, ScaledMagnitude);

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
            Vector256<float> result = Numerics.UnPremultiply(source, ScaledMagnitude);

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
            Vector128<float> result = Numerics.UnPremultiply(source, ScaledMagnitude);

            // Unassociation is performed in scaled space; map the result back only after alpha has served as a true opacity value.
            return scaled ? result : (result * Vector128.Create(2F)) - Vector128<float>.One;
        }

        /// <summary>
        /// Applies <typeparamref name="TAlpha"/> to native or scaled vectors, and packs them into signed-normalized storage in
        /// the same pass.
        /// </summary>
        /// <remarks>
        /// The alpha step uses scaled vectors. With an alpha step, native input is mapped first with scaled = (native + 1) / 2.
        /// The destination must contain space for every source vector.
        /// </remarks>
        /// <typeparam name="TAlpha">The alpha step for each pixel.</typeparam>
        /// <param name="source">The source vectors.</param>
        /// <param name="destination">The destination pixels.</param>
        /// <param name="scaled">Whether the source contains scaled vectors.</param>
        public static void Pack<TAlpha>(Span<Vector4> source, Span<NormalizedByte4P> destination, bool scaled)
            where TAlpha : struct, PixelFormats.PixelOperations.IAlphaOperator
        {
            ref Vector4 sourceBase = ref MemoryMarshal.GetReference(source);
            ref NormalizedByte4P destinationBase = ref MemoryMarshal.GetReference(destination);
            bool alphaStep = typeof(TAlpha) != typeof(PixelFormats.PixelOperations.NoAlphaOperator);
            bool mapToScaled = alphaStep && !scaled;
            bool encodeScaled = scaled || alphaStep;
            int i = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                int pixelsPerVector = Vector512<float>.Count / Vector128<float>.Count;

                for (; i <= source.Length - pixelsPerVector; i += pixelsPerVector)
                {
                    Vector512<float> vector = Unsafe.As<Vector4, Vector512<float>>(ref Unsafe.Add(ref sourceBase, (uint)i));

                    if (mapToScaled)
                    {
                        vector = (vector + Vector512<float>.One) / Vector512.Create(2F);
                    }

                    Vector512<int> integers = ConvertToPackedInt32(TAlpha.Apply(vector), encodeScaled);
                    Vector256<short> shorts = Vector256_.PackSignedSaturate(integers.GetLower(), integers.GetUpper());
                    Vector128<sbyte> packed = Vector128.NarrowWithSaturation(shorts.GetLower(), shorts.GetUpper());

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

                    if (mapToScaled)
                    {
                        vector = (vector + Vector256<float>.One) / Vector256.Create(2F);
                    }

                    Vector256<int> integers = ConvertToPackedInt32(TAlpha.Apply(vector), encodeScaled);
                    Vector128<short> shorts = Vector128.NarrowWithSaturation(integers.GetLower(), integers.GetUpper());
                    Vector128<sbyte> packed = Vector128.NarrowWithSaturation(shorts, shorts);
                    Unsafe.As<NormalizedByte4P, ulong>(ref Unsafe.Add(ref destinationBase, (uint)i)) = packed.AsUInt64().GetElement(0);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; i < source.Length; i++)
                {
                    Vector128<float> vector = Unsafe.As<Vector4, Vector128<float>>(ref Unsafe.Add(ref sourceBase, (uint)i));

                    if (mapToScaled)
                    {
                        vector = (vector + Vector128<float>.One) / Vector128.Create(2F);
                    }

                    Vector128<int> integers = ConvertToPackedInt32(TAlpha.Apply(vector), encodeScaled);
                    Vector128<short> shorts = Vector128.NarrowWithSaturation(integers, integers);
                    Vector128<sbyte> packed = Vector128.NarrowWithSaturation(shorts, shorts);
                    Unsafe.Add(ref destinationBase, (uint)i).PackedValue = packed.AsUInt32().GetElement(0);
                }

                return;
            }

            for (; i < source.Length; i++)
            {
                Vector4 vector = Unsafe.Add(ref sourceBase, (uint)i);

                if (mapToScaled)
                {
                    vector = (vector + Vector4.One) / 2F;
                }

                // Both signed-byte formats use this packer. The alpha step selects the alpha representation that is stored.
                vector = TAlpha.Apply(vector.AsVector128()).AsVector4();
                Unsafe.Add(ref destinationBase, (uint)i) = encodeScaled
                    ? PackAssociatedScaledVector4(vector)
                    : new NormalizedByte4P { PackedValue = NormalizedByte4P.Pack(vector) };
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

            source = ColorNumerics.ClampToFinite(source, -one, one) * Vector512.Create(MaxPos);

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

            source = ColorNumerics.ClampToFinite(source, -one, one) * Vector256.Create(MaxPos);

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

            source = ColorNumerics.ClampToFinite(source, -one, one) * Vector128.Create(MaxPos);

            return Vector128_.ConvertToInt32RoundToEven(source);
        }

        /// <summary>
        /// Converts unassociated scaled RGBA pixels to the associated scaled representation, with the alpha that the
        /// destination stores.
        /// </summary>
        /// <remarks>
        /// The components are bounded to [0, 1] first. NaN becomes zero. Alpha is mapped to signed units [-1, 1], rounded to
        /// the stored integer range [-127, 127] with ties to even, and mapped back to opacity. RGB is multiplied by that
        /// stored alpha.
        /// </remarks>
        private readonly struct StorageAssociateOperator : PixelFormats.PixelOperations.IAlphaOperator
        {
            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static Vector128<float> Apply(Vector128<float> source)
            {
                Vector128<float> one = Vector128<float>.One;

                source = ColorNumerics.ClampToFinite(source, Vector128<float>.Zero, one);
                Vector128<float> alpha = Vector128_.ShuffleNative(source, 0b_11_11_11_11);
                Vector128<float> nativeAlpha = ColorNumerics.ClampToFinite((alpha * Vector128.Create(2F)) - one, -one, one);
                Vector128<float> storedAlpha = Vector128.Round(nativeAlpha * Vector128.Create(MaxPos));
                storedAlpha = (storedAlpha + Vector128.Create(MaxPos)) / Vector128.Create(ScaledMagnitude);

                // RGB is associated with the stored alpha. The alpha lane takes that alpha.
                return Vector128.ConditionalSelect(Vector128.Create(0, 0, 0, -1).AsSingle(), storedAlpha, source * storedAlpha);
            }

            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static Vector256<float> Apply(Vector256<float> source)
            {
                Vector256<float> one = Vector256<float>.One;

                // Each four-lane group uses its own alpha, with the same rules as the 128-bit form.
                source = ColorNumerics.ClampToFinite(source, Vector256<float>.Zero, one);
                Vector256<float> alpha = Vector256_.ShuffleNative(source, 0b_11_11_11_11);
                Vector256<float> nativeAlpha = ColorNumerics.ClampToFinite((alpha * Vector256.Create(2F)) - one, -one, one);
                Vector256<float> storedAlpha = Vector256.Round(nativeAlpha * Vector256.Create(MaxPos));
                storedAlpha = (storedAlpha + Vector256.Create(MaxPos)) / Vector256.Create(ScaledMagnitude);

                Vector256<float> alphaMask = Vector256.Create(0, 0, 0, -1, 0, 0, 0, -1).AsSingle();
                return Vector256.ConditionalSelect(alphaMask, storedAlpha, source * storedAlpha);
            }

            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static Vector512<float> Apply(Vector512<float> source)
            {
                Vector512<float> one = Vector512<float>.One;

                // Each four-lane group uses its own alpha, with the same rules as the 128-bit form.
                source = ColorNumerics.ClampToFinite(source, Vector512<float>.Zero, one);
                Vector512<float> alpha = Vector512_.ShuffleNative(source, 0b_11_11_11_11);
                Vector512<float> nativeAlpha = ColorNumerics.ClampToFinite((alpha * Vector512.Create(2F)) - one, -one, one);
                Vector512<float> storedAlpha = Vector512.Round(nativeAlpha * Vector512.Create(MaxPos));
                storedAlpha = (storedAlpha + Vector512.Create(MaxPos)) / Vector512.Create(ScaledMagnitude);

                Vector512<float> alphaMask = Vector512.Create(0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1).AsSingle();
                return Vector512.ConditionalSelect(alphaMask, storedAlpha, source * storedAlpha);
            }
        }

        /// <summary>
        /// Reassociates associated scaled RGBA pixels with the alpha that the destination stores.
        /// </summary>
        /// <remarks>
        /// Alpha is made finite and nonnegative. Positive finite alpha above one is kept until RGB is recovered.
        /// Zero alpha gives zero for all components. RGB is multiplied by stored alpha / alpha in one step, so RGB values
        /// on exact midpoints stay exact when alpha does not change. Then RGB is bounded to [0, stored alpha].
        /// </remarks>
        private readonly struct StorageReassociateOperator : PixelFormats.PixelOperations.IAlphaOperator
        {
            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static Vector128<float> Apply(Vector128<float> source)
            {
                Vector128<float> zero = Vector128<float>.Zero;
                Vector128<float> one = Vector128<float>.One;

                Vector128<float> alpha = Vector128_.ShuffleNative(source, 0b_11_11_11_11);
                alpha = ColorNumerics.ClampToFinite(alpha, zero, Vector128.Create(float.MaxValue));
                Vector128<float> nativeAlpha = ColorNumerics.ClampToFinite((alpha * Vector128.Create(2F)) - one, -one, one);
                Vector128<float> storedAlpha = Vector128.Round(nativeAlpha * Vector128.Create(MaxPos));
                storedAlpha = (storedAlpha + Vector128.Create(MaxPos)) / Vector128.Create(ScaledMagnitude);

                Vector128<float> result = source * (storedAlpha / alpha);
                result = Vector128.ConditionalSelect(Vector128.Create(0, 0, 0, -1).AsSingle(), storedAlpha, result);
                result = ColorNumerics.ClampToFinite(result, zero, storedAlpha);

                return Vector128.ConditionalSelect(Vector128.LessThanOrEqual(alpha, zero), zero, result);
            }

            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static Vector256<float> Apply(Vector256<float> source)
            {
                Vector256<float> zero = Vector256<float>.Zero;
                Vector256<float> one = Vector256<float>.One;

                // Each four-lane group uses its own alpha, with the same rules as the 128-bit form.
                Vector256<float> alpha = Vector256_.ShuffleNative(source, 0b_11_11_11_11);
                alpha = ColorNumerics.ClampToFinite(alpha, zero, Vector256.Create(float.MaxValue));
                Vector256<float> nativeAlpha = ColorNumerics.ClampToFinite((alpha * Vector256.Create(2F)) - one, -one, one);
                Vector256<float> storedAlpha = Vector256.Round(nativeAlpha * Vector256.Create(MaxPos));
                storedAlpha = (storedAlpha + Vector256.Create(MaxPos)) / Vector256.Create(ScaledMagnitude);

                Vector256<float> result = source * (storedAlpha / alpha);
                result = Vector256.ConditionalSelect(Vector256.Create(0, 0, 0, -1, 0, 0, 0, -1).AsSingle(), storedAlpha, result);
                result = ColorNumerics.ClampToFinite(result, zero, storedAlpha);

                return Vector256.ConditionalSelect(Vector256.LessThanOrEqual(alpha, zero), zero, result);
            }

            /// <inheritdoc/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static Vector512<float> Apply(Vector512<float> source)
            {
                Vector512<float> zero = Vector512<float>.Zero;
                Vector512<float> one = Vector512<float>.One;

                // Each four-lane group uses its own alpha, with the same rules as the 128-bit form.
                Vector512<float> alpha = Vector512_.ShuffleNative(source, 0b_11_11_11_11);
                alpha = ColorNumerics.ClampToFinite(alpha, zero, Vector512.Create(float.MaxValue));
                Vector512<float> nativeAlpha = ColorNumerics.ClampToFinite((alpha * Vector512.Create(2F)) - one, -one, one);
                Vector512<float> storedAlpha = Vector512.Round(nativeAlpha * Vector512.Create(MaxPos));
                storedAlpha = (storedAlpha + Vector512.Create(MaxPos)) / Vector512.Create(ScaledMagnitude);

                Vector512<float> alphaMask = Vector512.Create(0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1).AsSingle();
                Vector512<float> result = source * (storedAlpha / alpha);
                result = Vector512.ConditionalSelect(alphaMask, storedAlpha, result);
                result = ColorNumerics.ClampToFinite(result, zero, storedAlpha);

                return Vector512.ConditionalSelect(Vector512.LessThanOrEqual(alpha, zero), zero, result);
            }
        }
    }
}
