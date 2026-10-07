// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.PixelFormats.Utils;

/// <summary>
/// Helper class for (bulk) conversion of <see cref="Vector4"/> buffers to/from other buffer types.
/// </summary>
internal static partial class Vector4Converters
{
    /// <summary>
    /// Provides default implementations for batched to/from <see cref="Vector4"/> conversion.
    /// WARNING: The methods prefixed with "Unsafe" are operating without bounds checking and input validation!
    /// Input validation is the responsibility of the caller!
    /// </summary>
    public static class Default
    {
        /// <summary>
        /// Converts unassociated native-range vectors to pixels without checking span lengths.
        /// </summary>
        /// <typeparam name="TPixel">The destination pixel format.</typeparam>
        /// <param name="source">The source vectors.</param>
        /// <param name="destination">The destination pixels. Its length must be at least <paramref name="source"/> length.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void UnsafeFromUnassociatedVector4<TPixel>(
            ReadOnlySpan<Vector4> source,
            Span<TPixel> destination)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            ref Vector4 sourceStart = ref MemoryMarshal.GetReference(source);
            ref Vector4 sourceEnd = ref Unsafe.Add(ref sourceStart, (uint)source.Length);
            ref TPixel destinationBase = ref MemoryMarshal.GetReference(destination);

            while (Unsafe.IsAddressLessThan(ref sourceStart, ref sourceEnd))
            {
                destinationBase = TPixel.FromUnassociatedVector4(sourceStart);

                sourceStart = ref Unsafe.Add(ref sourceStart, 1);
                destinationBase = ref Unsafe.Add(ref destinationBase, 1);
            }
        }

        /// <summary>
        /// Converts associated native-range vectors to pixels without checking span lengths.
        /// </summary>
        /// <typeparam name="TPixel">The destination pixel format.</typeparam>
        /// <param name="source">The source vectors.</param>
        /// <param name="destination">The destination pixels. Its length must be at least <paramref name="source"/> length.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void UnsafeFromAssociatedVector4<TPixel>(
            ReadOnlySpan<Vector4> source,
            Span<TPixel> destination)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            ref Vector4 sourceStart = ref MemoryMarshal.GetReference(source);
            ref Vector4 sourceEnd = ref Unsafe.Add(ref sourceStart, (uint)source.Length);
            ref TPixel destinationBase = ref MemoryMarshal.GetReference(destination);

            while (Unsafe.IsAddressLessThan(ref sourceStart, ref sourceEnd))
            {
                destinationBase = TPixel.FromAssociatedVector4(sourceStart);

                sourceStart = ref Unsafe.Add(ref sourceStart, 1);
                destinationBase = ref Unsafe.Add(ref destinationBase, 1);
            }
        }

        /// <summary>
        /// Converts pixels to unassociated native-range vectors without checking span lengths.
        /// </summary>
        /// <typeparam name="TPixel">The source pixel format.</typeparam>
        /// <param name="source">The source pixels.</param>
        /// <param name="destination">The destination vectors. Its length must be at least <paramref name="source"/> length.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void UnsafeToUnassociatedVector4<TPixel>(
            ReadOnlySpan<TPixel> source,
            Span<Vector4> destination)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            ref TPixel sourceStart = ref MemoryMarshal.GetReference(source);
            ref TPixel sourceEnd = ref Unsafe.Add(ref sourceStart, (uint)source.Length);
            ref Vector4 destinationBase = ref MemoryMarshal.GetReference(destination);

            while (Unsafe.IsAddressLessThan(ref sourceStart, ref sourceEnd))
            {
                destinationBase = sourceStart.ToUnassociatedVector4();

                sourceStart = ref Unsafe.Add(ref sourceStart, 1);
                destinationBase = ref Unsafe.Add(ref destinationBase, 1);
            }
        }

        /// <summary>
        /// Converts pixels to associated native-range vectors without checking span lengths.
        /// </summary>
        /// <typeparam name="TPixel">The source pixel format.</typeparam>
        /// <param name="source">The source pixels.</param>
        /// <param name="destination">The destination vectors. Its length must be at least <paramref name="source"/> length.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void UnsafeToAssociatedVector4<TPixel>(
            ReadOnlySpan<TPixel> source,
            Span<Vector4> destination)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            ref TPixel sourceStart = ref MemoryMarshal.GetReference(source);
            ref TPixel sourceEnd = ref Unsafe.Add(ref sourceStart, (uint)source.Length);
            ref Vector4 destinationBase = ref MemoryMarshal.GetReference(destination);

            while (Unsafe.IsAddressLessThan(ref sourceStart, ref sourceEnd))
            {
                destinationBase = sourceStart.ToAssociatedVector4();

                sourceStart = ref Unsafe.Add(ref sourceStart, 1);
                destinationBase = ref Unsafe.Add(ref destinationBase, 1);
            }
        }

        /// <summary>
        /// Converts unassociated scaled vectors to pixels without checking span lengths.
        /// </summary>
        /// <typeparam name="TPixel">The destination pixel format.</typeparam>
        /// <param name="source">The source vectors.</param>
        /// <param name="destination">The destination pixels. Its length must be at least <paramref name="source"/> length.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void UnsafeFromUnassociatedScaledVector4<TPixel>(
            ReadOnlySpan<Vector4> source,
            Span<TPixel> destination)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            ref Vector4 sourceStart = ref MemoryMarshal.GetReference(source);
            ref Vector4 sourceEnd = ref Unsafe.Add(ref sourceStart, (uint)source.Length);
            ref TPixel destinationBase = ref MemoryMarshal.GetReference(destination);

            while (Unsafe.IsAddressLessThan(ref sourceStart, ref sourceEnd))
            {
                destinationBase = TPixel.FromUnassociatedScaledVector4(sourceStart);

                sourceStart = ref Unsafe.Add(ref sourceStart, 1);
                destinationBase = ref Unsafe.Add(ref destinationBase, 1);
            }
        }

        /// <summary>
        /// Converts associated scaled vectors to pixels without checking span lengths.
        /// </summary>
        /// <typeparam name="TPixel">The destination pixel format.</typeparam>
        /// <param name="source">The source vectors.</param>
        /// <param name="destination">The destination pixels. Its length must be at least <paramref name="source"/> length.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void UnsafeFromAssociatedScaledVector4<TPixel>(
            ReadOnlySpan<Vector4> source,
            Span<TPixel> destination)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            ref Vector4 sourceStart = ref MemoryMarshal.GetReference(source);
            ref Vector4 sourceEnd = ref Unsafe.Add(ref sourceStart, (uint)source.Length);
            ref TPixel destinationBase = ref MemoryMarshal.GetReference(destination);

            while (Unsafe.IsAddressLessThan(ref sourceStart, ref sourceEnd))
            {
                destinationBase = TPixel.FromAssociatedScaledVector4(sourceStart);

                sourceStart = ref Unsafe.Add(ref sourceStart, 1);
                destinationBase = ref Unsafe.Add(ref destinationBase, 1);
            }
        }

        /// <summary>
        /// Converts pixels to unassociated scaled vectors without checking span lengths.
        /// </summary>
        /// <typeparam name="TPixel">The source pixel format.</typeparam>
        /// <param name="source">The source pixels.</param>
        /// <param name="destination">The destination vectors. Its length must be at least <paramref name="source"/> length.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void UnsafeToUnassociatedScaledVector4<TPixel>(
            ReadOnlySpan<TPixel> source,
            Span<Vector4> destination)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            ref TPixel sourceStart = ref MemoryMarshal.GetReference(source);
            ref TPixel sourceEnd = ref Unsafe.Add(ref sourceStart, (uint)source.Length);
            ref Vector4 destinationBase = ref MemoryMarshal.GetReference(destination);

            while (Unsafe.IsAddressLessThan(ref sourceStart, ref sourceEnd))
            {
                destinationBase = sourceStart.ToUnassociatedScaledVector4();

                sourceStart = ref Unsafe.Add(ref sourceStart, 1);
                destinationBase = ref Unsafe.Add(ref destinationBase, 1);
            }
        }

        /// <summary>
        /// Converts pixels to associated scaled vectors without checking span lengths.
        /// </summary>
        /// <typeparam name="TPixel">The source pixel format.</typeparam>
        /// <param name="source">The source pixels.</param>
        /// <param name="destination">The destination vectors. Its length must be at least <paramref name="source"/> length.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void UnsafeToAssociatedScaledVector4<TPixel>(
            ReadOnlySpan<TPixel> source,
            Span<Vector4> destination)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            ref TPixel sourceStart = ref MemoryMarshal.GetReference(source);
            ref TPixel sourceEnd = ref Unsafe.Add(ref sourceStart, (uint)source.Length);
            ref Vector4 destinationBase = ref MemoryMarshal.GetReference(destination);

            while (Unsafe.IsAddressLessThan(ref sourceStart, ref sourceEnd))
            {
                destinationBase = sourceStart.ToAssociatedScaledVector4();

                sourceStart = ref Unsafe.Add(ref sourceStart, 1);
                destinationBase = ref Unsafe.Add(ref destinationBase, 1);
            }
        }
    }
}
