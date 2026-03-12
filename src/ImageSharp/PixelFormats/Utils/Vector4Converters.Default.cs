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
            ApplyBackwardConversionModifiers(source, modifiers);

            if (modifiers.IsDefined(PixelConversionModifiers.Scale))
            {
                UnsafeFromScaledVector4Core(source, destination);
            }
            else
            {
                UnsafeFromVector4Core(source, destination);
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static void UnsafeToVector4<TPixel>(
            ReadOnlySpan<TPixel> source,
            Span<Vector4> destination,
            PixelConversionModifiers modifiers)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            if (modifiers.IsDefined(PixelConversionModifiers.Scale))
            {
                UnsafeToScaledVector4Core(source, destination);
            }
            else
            {
                UnsafeToVector4Core(source, destination);
            }

            ApplyForwardConversionModifiers(destination, modifiers);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static void UnsafeFromVector4Core<TPixel>(
            ReadOnlySpan<Vector4> source,
            Span<TPixel> destination)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            ref Vector4 sourceStart = ref MemoryMarshal.GetReference(source);
            ref Vector4 sourceEnd = ref Unsafe.Add(ref sourceStart, (uint)source.Length);
            ref TPixel destinationBase = ref MemoryMarshal.GetReference(destination);

            while (Unsafe.IsAddressLessThan(ref sourceStart, ref sourceEnd))
            {
                destinationBase = TPixel.FromVector4(sourceStart);

                sourceStart = ref Unsafe.Add(ref sourceStart, 1);
                destinationBase = ref Unsafe.Add(ref destinationBase, 1);
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static void UnsafeToVector4Core<TPixel>(
            ReadOnlySpan<TPixel> source,
            Span<Vector4> destination)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            ref TPixel sourceStart = ref MemoryMarshal.GetReference(source);
            ref TPixel sourceEnd = ref Unsafe.Add(ref sourceStart, (uint)source.Length);
            ref Vector4 destinationBase = ref MemoryMarshal.GetReference(destination);

            while (Unsafe.IsAddressLessThan(ref sourceStart, ref sourceEnd))
            {
                destinationBase = sourceStart.ToVector4();

                sourceStart = ref Unsafe.Add(ref sourceStart, 1);
                destinationBase = ref Unsafe.Add(ref destinationBase, 1);
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static void UnsafeFromScaledVector4Core<TPixel>(
            ReadOnlySpan<Vector4> source,
            Span<TPixel> destination)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            ref Vector4 sourceStart = ref MemoryMarshal.GetReference(source);
            ref Vector4 sourceEnd = ref Unsafe.Add(ref sourceStart, (uint)source.Length);
            ref TPixel destinationBase = ref MemoryMarshal.GetReference(destination);

            while (Unsafe.IsAddressLessThan(ref sourceStart, ref sourceEnd))
            {
                destinationBase = TPixel.FromScaledVector4(sourceStart);

                sourceStart = ref Unsafe.Add(ref sourceStart, 1);
                destinationBase = ref Unsafe.Add(ref destinationBase, 1);
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static void UnsafeToScaledVector4Core<TPixel>(
            ReadOnlySpan<TPixel> source,
            Span<Vector4> destination)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            ref TPixel sourceStart = ref MemoryMarshal.GetReference(source);
            ref TPixel sourceEnd = ref Unsafe.Add(ref sourceStart, (uint)source.Length);
            ref Vector4 destinationBase = ref MemoryMarshal.GetReference(destination);

            while (Unsafe.IsAddressLessThan(ref sourceStart, ref sourceEnd))
            {
                destinationBase = sourceStart.ToScaledVector4();

                sourceStart = ref Unsafe.Add(ref sourceStart, 1);
                destinationBase = ref Unsafe.Add(ref destinationBase, 1);
            }
        }
    }
}
