// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.PixelFormats.Utils;

/// <content>
/// Contains <see cref="AssociatedRgbaCompatible"/>.
/// </content>
internal static partial class Vector4Converters
{
    /// <summary>
    /// Provides efficient batched conversion for four-byte pixel types that store premultiplied alpha.
    /// </summary>
    public static class AssociatedRgbaCompatible
    {
        /// <summary>
        /// Converts associated RGBA byte components to an unassociated scaled vector.
        /// </summary>
        /// <param name="red">The associated red component.</param>
        /// <param name="green">The associated green component.</param>
        /// <param name="blue">The associated blue component.</param>
        /// <param name="alpha">The alpha component.</param>
        /// <returns>The unassociated scaled vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector4 ToUnassociatedVector4(byte red, byte green, byte blue, byte alpha)
        {
            // Divide the original byte magnitudes before normalization so a floating-point intermediate cannot move an exact byte conversion across its rounding midpoint.
            Vector4 vector = new(red, green, blue, alpha);
            float colorDivisor = alpha == 0 ? byte.MaxValue : alpha;
            return vector / new Vector4(colorDivisor, colorDivisor, colorDivisor, byte.MaxValue);
        }

        /// <summary>
        /// Converts packed RGBA byte magnitudes to unassociated scaled vectors.
        /// </summary>
        /// <param name="source">The packed RGBA bytes.</param>
        /// <param name="destination">The destination vectors.</param>
        private static void ToUnassociatedVector4(ReadOnlySpan<byte> source, Span<Vector4> destination)
        {
            ref byte sourceBase = ref MemoryMarshal.GetReference(source);
            ref Vector4 destinationBase = ref MemoryMarshal.GetReference(destination);

            // Each packed component occupies one byte, so the destination Vector4 lane count also defines the source byte stride.
            int componentsPerPixel = Vector128<float>.Count;
            int i = 0;

            // Portable widening advances one integer width at a time, so assemble the wider vectors from ordered 128-bit halves.
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
                    Unsafe.As<Vector4, Vector512<float>>(ref Unsafe.Add(ref destinationBase, (uint)i)) = ToUnassociatedVector4(Vector512.ConvertToSingle(integers));
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
                    Unsafe.As<Vector4, Vector256<float>>(ref Unsafe.Add(ref destinationBase, (uint)i)) = ToUnassociatedVector4(Vector256.ConvertToSingle(integers));
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                int pixelsPerVector = Vector128<byte>.Count / componentsPerPixel;

                for (; i <= destination.Length - pixelsPerVector; i += pixelsPerVector)
                {
                    Vector128<byte> packed = Vector128.LoadUnsafe(ref sourceBase, (nuint)(i * componentsPerPixel));
                    (Vector128<ushort> shorts0, Vector128<ushort> shorts1) = Vector128.Widen(packed);
                    (Vector128<uint> integers0, Vector128<uint> integers1) = Vector128.Widen(shorts0);
                    (Vector128<uint> integers2, Vector128<uint> integers3) = Vector128.Widen(shorts1);

                    Unsafe.Add(ref destinationBase, (uint)i) = ToUnassociatedVector4(Vector128.ConvertToSingle(integers0.AsInt32())).AsVector4();
                    Unsafe.Add(ref destinationBase, (uint)i + 1u) = ToUnassociatedVector4(Vector128.ConvertToSingle(integers1.AsInt32())).AsVector4();
                    Unsafe.Add(ref destinationBase, (uint)i + 2u) = ToUnassociatedVector4(Vector128.ConvertToSingle(integers2.AsInt32())).AsVector4();
                    Unsafe.Add(ref destinationBase, (uint)i + 3u) = ToUnassociatedVector4(Vector128.ConvertToSingle(integers3.AsInt32())).AsVector4();
                }
            }

            for (; i < destination.Length; i++)
            {
                int offset = i * componentsPerPixel;

                Unsafe.Add(ref destinationBase, (uint)i) = ToUnassociatedVector4(
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
        private static Vector512<float> ToUnassociatedVector4(Vector512<float> source)
        {
            Vector512<float> zero = Vector512<float>.Zero;
            Vector512<float> byteMax = Vector512.Create((float)byte.MaxValue);
            Vector512<float> alpha = Vector512_.ShuffleNative(source, 0b_11_11_11_11);

            // Zero-alpha storage has no alpha divisor, while W always uses the byte normalization divisor.
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
        private static Vector256<float> ToUnassociatedVector4(Vector256<float> source)
        {
            Vector256<float> zero = Vector256<float>.Zero;
            Vector256<float> byteMax = Vector256.Create((float)byte.MaxValue);
            Vector256<float> alpha = Vector256_.ShuffleNative(source, 0b_11_11_11_11);

            // Zero-alpha storage has no alpha divisor, while W always uses the byte normalization divisor.
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
        private static Vector128<float> ToUnassociatedVector4(Vector128<float> source)
        {
            Vector128<float> zero = Vector128<float>.Zero;
            Vector128<float> byteMax = Vector128.Create((float)byte.MaxValue);
            Vector128<float> alpha = Vector128_.ShuffleNative(source, 0b_11_11_11_11);

            // Zero-alpha storage has no alpha divisor, while W always uses the byte normalization divisor.
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
            source = Vector512.Min(Vector512.Max(source, zero), one);
            Vector512<float> alpha = Vector512_.ShuffleNative(source, 0b_11_11_11_11);
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
            source = Vector256.Min(Vector256.Max(source, zero), one);
            Vector256<float> alpha = Vector256_.ShuffleNative(source, 0b_11_11_11_11);
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
            source = Vector128.Min(Vector128.Max(source, zero), one);
            Vector128<float> alpha = Vector128_.ShuffleNative(source, 0b_11_11_11_11);
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
        private static Vector4 AssociateToByte(Vector4 source)
        {
            source = Numerics.Clamp(source, Vector4.Zero, Vector4.One);
            float storedAlpha = (byte)Numerics.Clamp((source.W * byte.MaxValue) + 0.5F, 0, byte.MaxValue);

            source *= storedAlpha;
            source.W = storedAlpha;
            return source;
        }

        /// <summary>
        /// Converts an unassociated scaled vector to an <see cref="Rgba32P"/> pixel.
        /// </summary>
        /// <param name="source">The unassociated scaled vector.</param>
        /// <returns>The associated pixel.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Rgba32P FromUnassociatedVector4ToRgba32P(Vector4 source)
        {
            source = AssociateToByte(source);
            return new Rgba32P(ConvertToByte(source.X), ConvertToByte(source.Y), ConvertToByte(source.Z), ConvertToByte(source.W));
        }

        /// <summary>
        /// Converts an unassociated scaled vector to a <see cref="Bgra32P"/> pixel.
        /// </summary>
        /// <param name="source">The unassociated scaled vector.</param>
        /// <returns>The associated pixel.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Bgra32P FromUnassociatedVector4ToBgra32P(Vector4 source)
        {
            source = AssociateToByte(source);
            return new Bgra32P(ConvertToByte(source.X), ConvertToByte(source.Y), ConvertToByte(source.Z), ConvertToByte(source.W));
        }

        /// <summary>
        /// Converts an unassociated scaled vector to an <see cref="Argb32P"/> pixel.
        /// </summary>
        /// <param name="source">The unassociated scaled vector.</param>
        /// <returns>The associated pixel.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Argb32P FromUnassociatedVector4ToArgb32P(Vector4 source)
        {
            source = AssociateToByte(source);
            return new Argb32P(ConvertToByte(source.X), ConvertToByte(source.Y), ConvertToByte(source.Z), ConvertToByte(source.W));
        }

        /// <summary>
        /// Converts an unassociated scaled vector to an <see cref="Abgr32P"/> pixel.
        /// </summary>
        /// <param name="source">The unassociated scaled vector.</param>
        /// <returns>The associated pixel.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Abgr32P FromUnassociatedVector4ToAbgr32P(Vector4 source)
        {
            source = AssociateToByte(source);
            return new Abgr32P(ConvertToByte(source.X), ConvertToByte(source.Y), ConvertToByte(source.Z), ConvertToByte(source.W));
        }

        /// <summary>
        /// Converts an associated scaled vector to associated byte magnitudes using the alpha byte the destination stores.
        /// </summary>
        /// <param name="source">The associated scaled vector.</param>
        /// <returns>The associated byte magnitudes.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector4 ReassociateToByte(Vector4 source)
        {
            float alpha = source.W;

            if (alpha <= 0)
            {
                return Vector4.Zero;
            }

            float byteAlpha = alpha * byte.MaxValue;
            float storedAlpha = (byte)Numerics.Clamp(byteAlpha + 0.5F, 0, byte.MaxValue);

            if (byteAlpha == storedAlpha)
            {
                // Quantization leaves alpha unchanged, so RGB is already associated correctly. Avoiding division preserves exact byte midpoints produced by scaling stored components.
                source *= byte.MaxValue;
            }
            else
            {
                // Recover straight RGB before multiplying by the stored alpha byte. Keeping the association in byte magnitude preserves exact half-byte rounding boundaries.
                Numerics.UnPremultiply(ref source);
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
            Vector512<float> alpha = Vector512.Max(Vector512_.ShuffleNative(source, 0b_11_11_11_11), zero);
            Vector512<float> byteAlpha = alpha * byteMax;
            Vector512<float> storedAlpha = Vector512.Floor(Vector512.Min(Vector512.Max(byteAlpha + Vector512.Create(.5F), zero), byteMax));
            Vector512<float> result = (source / alpha) * storedAlpha;

            // Exact byte alpha values need no reassociation. Multiplying by 255 directly preserves RGB values that already lie on byte midpoints.
            result = Vector512.ConditionalSelect(Vector512.Equals(byteAlpha, storedAlpha), source * byteMax, result);
            Vector512<float> alphaMask = Vector512.Create(0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1).AsSingle();
            result = Vector512.ConditionalSelect(alphaMask, storedAlpha, result);
            result = Vector512.Min(Vector512.Max(result, zero), storedAlpha);
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
            Vector256<float> alpha = Vector256.Max(Vector256_.ShuffleNative(source, 0b_11_11_11_11), zero);
            Vector256<float> byteAlpha = alpha * byteMax;
            Vector256<float> storedAlpha = Vector256.Floor(Vector256.Min(Vector256.Max(byteAlpha + Vector256.Create(.5F), zero), byteMax));
            Vector256<float> result = (source / alpha) * storedAlpha;

            // Exact byte alpha values need no reassociation. Multiplying by 255 directly preserves RGB values that already lie on byte midpoints.
            result = Vector256.ConditionalSelect(Vector256.Equals(byteAlpha, storedAlpha), source * byteMax, result);
            Vector256<float> alphaMask = Vector256.Create(0, 0, 0, -1, 0, 0, 0, -1).AsSingle();
            result = Vector256.ConditionalSelect(alphaMask, storedAlpha, result);
            result = Vector256.Min(Vector256.Max(result, zero), storedAlpha);
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
            Vector128<float> alpha = Vector128.Max(Vector128_.ShuffleNative(source, 0b_11_11_11_11), zero);
            Vector128<float> byteAlpha = alpha * byteMax;
            Vector128<float> storedAlpha = Vector128.Floor(Vector128.Min(Vector128.Max(byteAlpha + Vector128.Create(.5F), zero), byteMax));
            Vector128<float> result = (source / alpha) * storedAlpha;

            // Exact byte alpha values need no reassociation. Multiplying by 255 directly preserves RGB values that already lie on byte midpoints.
            result = Vector128.ConditionalSelect(Vector128.Equals(byteAlpha, storedAlpha), source * byteMax, result);
            Vector128<float> alphaMask = Vector128.Create(0, 0, 0, -1).AsSingle();
            result = Vector128.ConditionalSelect(alphaMask, storedAlpha, result);
            result = Vector128.Min(Vector128.Max(result, zero), storedAlpha);
            return Vector128.ConditionalSelect(Vector128.Equals(alpha, zero), zero, result);
        }

        /// <summary>
        /// Converts an associated scaled vector to an <see cref="Rgba32P"/> pixel.
        /// </summary>
        /// <param name="source">The associated scaled vector.</param>
        /// <returns>The associated pixel.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Rgba32P FromAssociatedVector4ToRgba32P(Vector4 source)
        {
            source = ReassociateToByte(source);
            return new Rgba32P(ConvertToByte(source.X), ConvertToByte(source.Y), ConvertToByte(source.Z), ConvertToByte(source.W));
        }

        /// <summary>
        /// Converts an associated scaled vector to a <see cref="Bgra32P"/> pixel.
        /// </summary>
        /// <param name="source">The associated scaled vector.</param>
        /// <returns>The associated pixel.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Bgra32P FromAssociatedVector4ToBgra32P(Vector4 source)
        {
            source = ReassociateToByte(source);
            return new Bgra32P(ConvertToByte(source.X), ConvertToByte(source.Y), ConvertToByte(source.Z), ConvertToByte(source.W));
        }

        /// <summary>
        /// Converts an associated scaled vector to an <see cref="Argb32P"/> pixel.
        /// </summary>
        /// <param name="source">The associated scaled vector.</param>
        /// <returns>The associated pixel.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Argb32P FromAssociatedVector4ToArgb32P(Vector4 source)
        {
            source = ReassociateToByte(source);
            return new Argb32P(ConvertToByte(source.X), ConvertToByte(source.Y), ConvertToByte(source.Z), ConvertToByte(source.W));
        }

        /// <summary>
        /// Converts an associated scaled vector to an <see cref="Abgr32P"/> pixel.
        /// </summary>
        /// <param name="source">The associated scaled vector.</param>
        /// <returns>The associated pixel.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Abgr32P FromAssociatedVector4ToAbgr32P(Vector4 source)
        {
            source = ReassociateToByte(source);
            return new Abgr32P(ConvertToByte(source.X), ConvertToByte(source.Y), ConvertToByte(source.Z), ConvertToByte(source.W));
        }

        /// <summary>
        /// Converts associated RGBA pixels to associated scaled vectors.
        /// </summary>
        /// <param name="source">The source pixels.</param>
        /// <param name="destination">The destination vectors.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void ToAssociatedVector4(ReadOnlySpan<Rgba32P> source, Span<Vector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];
            SimdUtils.ByteToNormalizedFloat(MemoryMarshal.Cast<Rgba32P, byte>(source), MemoryMarshal.Cast<Vector4, float>(destination));
        }

        /// <summary>
        /// Converts premultiplied RGBA pixels to unassociated scaled vectors.
        /// </summary>
        /// <param name="source">The source pixels.</param>
        /// <param name="destination">The destination vectors.</param>
        internal static void ToUnassociatedVector4(ReadOnlySpan<Rgba32P> source, Span<Vector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            ToUnassociatedVector4(MemoryMarshal.Cast<Rgba32P, byte>(source), destination[..source.Length]);
        }

        /// <summary>
        /// Converts unassociated scaled vectors to premultiplied RGBA pixels.
        /// </summary>
        /// <param name="source">The source vectors, modified in place during conversion.</param>
        /// <param name="destination">The destination pixels.</param>
        internal static void FromUnassociatedVector4(Span<Vector4> source, Span<Rgba32P> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];
            AssociateToByte(source);
            SimdUtils.FloatToByteSaturate(MemoryMarshal.Cast<Vector4, float>(source), MemoryMarshal.Cast<Rgba32P, byte>(destination));
        }

        /// <summary>
        /// Converts associated scaled vectors to premultiplied RGBA pixels.
        /// </summary>
        /// <param name="source">The source vectors, modified in place during conversion.</param>
        /// <param name="destination">The destination pixels.</param>
        internal static void FromAssociatedVector4(Span<Vector4> source, Span<Rgba32P> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];
            ReassociateToByte(source);
            SimdUtils.FloatToByteSaturate(MemoryMarshal.Cast<Vector4, float>(source), MemoryMarshal.Cast<Rgba32P, byte>(destination));
        }

        /// <summary>
        /// Converts associated BGRA pixels to associated scaled vectors.
        /// </summary>
        /// <param name="source">The source pixels.</param>
        /// <param name="destination">The destination vectors.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void ToAssociatedVector4(ReadOnlySpan<Bgra32P> source, Span<Vector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];

            if (source.IsEmpty)
            {
                return;
            }

            // ByteToNormalizedFloat preserves component order, so the packed BGRA bytes must first be shuffled to RGBA.
            // Reuse the unwritten end of the larger Vector4 destination as staging to avoid a temporary allocation.
            // The final vector overlaps that staging, so its source pixel is converted after the staged bytes are consumed.
            int lastIndex = source.Length - 1;
            Span<Rgba32> temporary = MemoryMarshal.Cast<Vector4, Rgba32>(destination).Slice((3 * source.Length) + 1, lastIndex);
            PixelConverter.FromBgra32.ToRgba32(MemoryMarshal.Cast<Bgra32P, byte>(source[..lastIndex]), MemoryMarshal.Cast<Rgba32, byte>(temporary));
            SimdUtils.ByteToNormalizedFloat(MemoryMarshal.Cast<Rgba32, byte>(temporary), MemoryMarshal.Cast<Vector4, float>(destination[..lastIndex]));
            destination[lastIndex] = source[lastIndex].ToVector4();
        }

        /// <summary>
        /// Converts premultiplied BGRA pixels to unassociated scaled vectors.
        /// </summary>
        /// <param name="source">The source pixels.</param>
        /// <param name="destination">The destination vectors.</param>
        internal static void ToUnassociatedVector4(ReadOnlySpan<Bgra32P> source, Span<Vector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];

            if (source.IsEmpty)
            {
                return;
            }

            // Stage all but the final pixel as RGBA bytes in the unwritten end of the expanding Vector4 destination.
            // The final expanded vector overlaps that staging region, so convert it directly after the staged bytes are consumed.
            int lastIndex = source.Length - 1;
            Span<Rgba32> temporary = MemoryMarshal.Cast<Vector4, Rgba32>(destination).Slice((3 * source.Length) + 1, lastIndex);
            PixelConverter.FromBgra32.ToRgba32(MemoryMarshal.Cast<Bgra32P, byte>(source[..lastIndex]), MemoryMarshal.Cast<Rgba32, byte>(temporary));
            ToUnassociatedVector4(MemoryMarshal.Cast<Rgba32, byte>(temporary), destination[..lastIndex]);
            destination[lastIndex] = ToUnassociatedVector4(source[lastIndex].R, source[lastIndex].G, source[lastIndex].B, source[lastIndex].A);
        }

        /// <summary>
        /// Converts unassociated scaled vectors to premultiplied BGRA pixels.
        /// </summary>
        /// <param name="source">The source vectors, modified in place during conversion.</param>
        /// <param name="destination">The destination pixels.</param>
        internal static void FromUnassociatedVector4(Span<Vector4> source, Span<Bgra32P> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];
            AssociateToByte(source);

            // FloatToByteSaturate emits RGBA bytes, which can be shuffled in place to avoid a temporary pixel buffer.
            Span<byte> destinationBytes = MemoryMarshal.Cast<Bgra32P, byte>(destination);
            SimdUtils.FloatToByteSaturate(MemoryMarshal.Cast<Vector4, float>(source), destinationBytes);
            PixelConverter.FromRgba32.ToBgra32(destinationBytes, destinationBytes);
        }

        /// <summary>
        /// Converts associated scaled vectors to premultiplied BGRA pixels.
        /// </summary>
        /// <param name="source">The source vectors, modified in place during conversion.</param>
        /// <param name="destination">The destination pixels.</param>
        internal static void FromAssociatedVector4(Span<Vector4> source, Span<Bgra32P> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];
            ReassociateToByte(source);

            Span<byte> destinationBytes = MemoryMarshal.Cast<Bgra32P, byte>(destination);
            SimdUtils.FloatToByteSaturate(MemoryMarshal.Cast<Vector4, float>(source), destinationBytes);
            PixelConverter.FromRgba32.ToBgra32(destinationBytes, destinationBytes);
        }

        /// <summary>
        /// Converts associated ARGB pixels to associated scaled vectors.
        /// </summary>
        /// <param name="source">The source pixels.</param>
        /// <param name="destination">The destination vectors.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void ToAssociatedVector4(ReadOnlySpan<Argb32P> source, Span<Vector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];

            if (source.IsEmpty)
            {
                return;
            }

            // ByteToNormalizedFloat preserves component order, so the packed ARGB bytes must first be shuffled to RGBA.
            // Reuse the unwritten end of the larger Vector4 destination as staging to avoid a temporary allocation.
            // The final vector overlaps that staging, so its source pixel is converted after the staged bytes are consumed.
            int lastIndex = source.Length - 1;
            Span<Rgba32> temporary = MemoryMarshal.Cast<Vector4, Rgba32>(destination).Slice((3 * source.Length) + 1, lastIndex);
            PixelConverter.FromArgb32.ToRgba32(MemoryMarshal.Cast<Argb32P, byte>(source[..lastIndex]), MemoryMarshal.Cast<Rgba32, byte>(temporary));
            SimdUtils.ByteToNormalizedFloat(MemoryMarshal.Cast<Rgba32, byte>(temporary), MemoryMarshal.Cast<Vector4, float>(destination[..lastIndex]));
            destination[lastIndex] = source[lastIndex].ToVector4();
        }

        /// <summary>
        /// Converts premultiplied ARGB pixels to unassociated scaled vectors.
        /// </summary>
        /// <param name="source">The source pixels.</param>
        /// <param name="destination">The destination vectors.</param>
        internal static void ToUnassociatedVector4(ReadOnlySpan<Argb32P> source, Span<Vector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];

            if (source.IsEmpty)
            {
                return;
            }

            // Stage all but the final pixel as RGBA bytes in the unwritten end of the expanding Vector4 destination.
            // The final expanded vector overlaps that staging region, so convert it directly after the staged bytes are consumed.
            int lastIndex = source.Length - 1;
            Span<Rgba32> temporary = MemoryMarshal.Cast<Vector4, Rgba32>(destination).Slice((3 * source.Length) + 1, lastIndex);
            PixelConverter.FromArgb32.ToRgba32(MemoryMarshal.Cast<Argb32P, byte>(source[..lastIndex]), MemoryMarshal.Cast<Rgba32, byte>(temporary));
            ToUnassociatedVector4(MemoryMarshal.Cast<Rgba32, byte>(temporary), destination[..lastIndex]);
            destination[lastIndex] = ToUnassociatedVector4(source[lastIndex].R, source[lastIndex].G, source[lastIndex].B, source[lastIndex].A);
        }

        /// <summary>
        /// Converts unassociated scaled vectors to premultiplied ARGB pixels.
        /// </summary>
        /// <param name="source">The source vectors, modified in place during conversion.</param>
        /// <param name="destination">The destination pixels.</param>
        internal static void FromUnassociatedVector4(Span<Vector4> source, Span<Argb32P> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];
            AssociateToByte(source);

            // FloatToByteSaturate emits RGBA bytes, which can be shuffled in place to avoid a temporary pixel buffer.
            Span<byte> destinationBytes = MemoryMarshal.Cast<Argb32P, byte>(destination);
            SimdUtils.FloatToByteSaturate(MemoryMarshal.Cast<Vector4, float>(source), destinationBytes);
            PixelConverter.FromRgba32.ToArgb32(destinationBytes, destinationBytes);
        }

        /// <summary>
        /// Converts associated scaled vectors to premultiplied ARGB pixels.
        /// </summary>
        /// <param name="source">The source vectors, modified in place during conversion.</param>
        /// <param name="destination">The destination pixels.</param>
        internal static void FromAssociatedVector4(Span<Vector4> source, Span<Argb32P> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];
            ReassociateToByte(source);

            Span<byte> destinationBytes = MemoryMarshal.Cast<Argb32P, byte>(destination);
            SimdUtils.FloatToByteSaturate(MemoryMarshal.Cast<Vector4, float>(source), destinationBytes);
            PixelConverter.FromRgba32.ToArgb32(destinationBytes, destinationBytes);
        }

        /// <summary>
        /// Converts associated ABGR pixels to associated scaled vectors.
        /// </summary>
        /// <param name="source">The source pixels.</param>
        /// <param name="destination">The destination vectors.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void ToAssociatedVector4(ReadOnlySpan<Abgr32P> source, Span<Vector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];

            if (source.IsEmpty)
            {
                return;
            }

            // ByteToNormalizedFloat preserves component order, so the packed ABGR bytes must first be shuffled to RGBA.
            // Reuse the unwritten end of the larger Vector4 destination as staging to avoid a temporary allocation.
            // The final vector overlaps that staging, so its source pixel is converted after the staged bytes are consumed.
            int lastIndex = source.Length - 1;
            Span<Rgba32> temporary = MemoryMarshal.Cast<Vector4, Rgba32>(destination).Slice((3 * source.Length) + 1, lastIndex);
            PixelConverter.FromAbgr32.ToRgba32(MemoryMarshal.Cast<Abgr32P, byte>(source[..lastIndex]), MemoryMarshal.Cast<Rgba32, byte>(temporary));
            SimdUtils.ByteToNormalizedFloat(MemoryMarshal.Cast<Rgba32, byte>(temporary), MemoryMarshal.Cast<Vector4, float>(destination[..lastIndex]));
            destination[lastIndex] = source[lastIndex].ToVector4();
        }

        /// <summary>
        /// Converts premultiplied ABGR pixels to unassociated scaled vectors.
        /// </summary>
        /// <param name="source">The source pixels.</param>
        /// <param name="destination">The destination vectors.</param>
        internal static void ToUnassociatedVector4(ReadOnlySpan<Abgr32P> source, Span<Vector4> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];

            if (source.IsEmpty)
            {
                return;
            }

            // Stage all but the final pixel as RGBA bytes in the unwritten end of the expanding Vector4 destination.
            // The final expanded vector overlaps that staging region, so convert it directly after the staged bytes are consumed.
            int lastIndex = source.Length - 1;
            Span<Rgba32> temporary = MemoryMarshal.Cast<Vector4, Rgba32>(destination).Slice((3 * source.Length) + 1, lastIndex);
            PixelConverter.FromAbgr32.ToRgba32(MemoryMarshal.Cast<Abgr32P, byte>(source[..lastIndex]), MemoryMarshal.Cast<Rgba32, byte>(temporary));
            ToUnassociatedVector4(MemoryMarshal.Cast<Rgba32, byte>(temporary), destination[..lastIndex]);
            destination[lastIndex] = ToUnassociatedVector4(source[lastIndex].R, source[lastIndex].G, source[lastIndex].B, source[lastIndex].A);
        }

        /// <summary>
        /// Converts unassociated scaled vectors to premultiplied ABGR pixels.
        /// </summary>
        /// <param name="source">The source vectors, modified in place during conversion.</param>
        /// <param name="destination">The destination pixels.</param>
        internal static void FromUnassociatedVector4(Span<Vector4> source, Span<Abgr32P> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];
            AssociateToByte(source);

            // FloatToByteSaturate emits RGBA bytes, which can be shuffled in place to avoid a temporary pixel buffer.
            Span<byte> destinationBytes = MemoryMarshal.Cast<Abgr32P, byte>(destination);
            SimdUtils.FloatToByteSaturate(MemoryMarshal.Cast<Vector4, float>(source), destinationBytes);
            PixelConverter.FromRgba32.ToAbgr32(destinationBytes, destinationBytes);
        }

        /// <summary>
        /// Converts associated scaled vectors to premultiplied ABGR pixels.
        /// </summary>
        /// <param name="source">The source vectors, modified in place during conversion.</param>
        /// <param name="destination">The destination pixels.</param>
        internal static void FromAssociatedVector4(Span<Vector4> source, Span<Abgr32P> destination)
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            destination = destination[..source.Length];
            ReassociateToByte(source);

            Span<byte> destinationBytes = MemoryMarshal.Cast<Abgr32P, byte>(destination);
            SimdUtils.FloatToByteSaturate(MemoryMarshal.Cast<Vector4, float>(source), destinationBytes);
            PixelConverter.FromRgba32.ToAbgr32(destinationBytes, destinationBytes);
        }

        /// <summary>
        /// Converts a floating-point byte magnitude to a byte.
        /// </summary>
        /// <param name="value">The byte magnitude.</param>
        /// <returns>The rounded and clamped byte.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte ConvertToByte(float value) => (byte)Numerics.Clamp(value + .5F, 0, byte.MaxValue);
    }
}
