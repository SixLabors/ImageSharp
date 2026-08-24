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
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void FromVector4<TPixel>(
            Span<Vector4> source,
            Span<TPixel> destination,
            PixelConversionModifiers modifiers)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            UnsafeFromVector4(source, destination, modifiers);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static void ToVector4<TPixel>(
            ReadOnlySpan<TPixel> source,
            Span<Vector4> destination,
            PixelConversionModifiers modifiers)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            UnsafeToVector4(source, destination, modifiers);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static void UnsafeFromVector4<TPixel>(
            Span<Vector4> source,
            Span<TPixel> destination,
            PixelConversionModifiers modifiers)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            bool scaled = modifiers.IsDefined(PixelConversionModifiers.Scale);

            if (modifiers.IsDefined(PixelConversionModifiers.SRgbCompand))
            {
                // Transfer functions consume straight color, so preserve the established modifier order before selecting a scalar storage conversion.
                ApplyBackwardConversionModifiers(source, modifiers);

                if (scaled)
                {
                    UnsafeFromUnassociatedScaledVector4Core(source, destination);
                }
                else
                {
                    UnsafeFromUnassociatedVector4Core(source, destination);
                }

                return;
            }

            if (scaled)
            {
                if (modifiers.IsDefined(PixelConversionModifiers.Premultiply))
                {
                    UnsafeFromAssociatedScaledVector4Core(source, destination);
                }
                else
                {
                    UnsafeFromUnassociatedScaledVector4Core(source, destination);
                }
            }
            else if (modifiers.IsDefined(PixelConversionModifiers.Premultiply))
            {
                UnsafeFromAssociatedVector4Core(source, destination);
            }
            else
            {
                UnsafeFromUnassociatedVector4Core(source, destination);
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static void UnsafeToVector4<TPixel>(
            ReadOnlySpan<TPixel> source,
            Span<Vector4> destination,
            PixelConversionModifiers modifiers)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            // Conversion length is defined by the source. Limiting the destination here also keeps modifiers from
            // changing spare capacity supplied by callers of either the checked or unchecked conversion path.
            destination = destination[..source.Length];

            bool scaled = modifiers.IsDefined(PixelConversionModifiers.Scale);

            if (modifiers.IsDefined(PixelConversionModifiers.SRgbCompand))
            {
                if (scaled)
                {
                    UnsafeToUnassociatedScaledVector4Core(source, destination);
                }
                else
                {
                    UnsafeToUnassociatedVector4Core(source, destination);
                }

                // Transfer functions consume straight color; association is applied only after expansion.
                ApplyForwardConversionModifiers(destination, modifiers);
                return;
            }

            if (scaled)
            {
                if (modifiers.IsDefined(PixelConversionModifiers.Premultiply))
                {
                    UnsafeToAssociatedScaledVector4Core(source, destination);
                }
                else
                {
                    UnsafeToUnassociatedScaledVector4Core(source, destination);
                }
            }
            else if (modifiers.IsDefined(PixelConversionModifiers.Premultiply))
            {
                UnsafeToAssociatedVector4Core(source, destination);
            }
            else
            {
                UnsafeToUnassociatedVector4Core(source, destination);
            }
        }

        /// <summary>
        /// Converts unassociated native-range vectors to pixels without checking span lengths.
        /// </summary>
        /// <typeparam name="TPixel">The destination pixel format.</typeparam>
        /// <param name="source">The source vectors.</param>
        /// <param name="destination">The destination pixels. Its length must be at least <paramref name="source"/> length.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        private static void UnsafeFromUnassociatedVector4Core<TPixel>(
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
        private static void UnsafeFromAssociatedVector4Core<TPixel>(
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
        private static void UnsafeToUnassociatedVector4Core<TPixel>(
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
        private static void UnsafeToAssociatedVector4Core<TPixel>(
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
        private static void UnsafeFromUnassociatedScaledVector4Core<TPixel>(
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
        private static void UnsafeFromAssociatedScaledVector4Core<TPixel>(
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
        private static void UnsafeToUnassociatedScaledVector4Core<TPixel>(
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
        private static void UnsafeToAssociatedScaledVector4Core<TPixel>(
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
