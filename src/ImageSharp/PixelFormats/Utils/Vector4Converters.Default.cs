// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.PixelFormats.Utils
{
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
                Span<Vector4> sourceVectors,
                Span<TPixel> destPixels,
                PixelConversionModifiers modifiers)
                where TPixel : unmanaged, IPixel<TPixel>
            {
                Guard.DestinationShouldNotBeTooShort(sourceVectors, destPixels, nameof(destPixels));

                UnsafeFromVector4(sourceVectors, destPixels, modifiers);
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            public static void ToVector4<TPixel>(
                ReadOnlySpan<TPixel> sourcePixels,
                Span<Vector4> destVectors,
                PixelConversionModifiers modifiers)
                where TPixel : unmanaged, IPixel<TPixel>
            {
                Guard.DestinationShouldNotBeTooShort(sourcePixels, destVectors, nameof(destVectors));

                UnsafeToVector4(sourcePixels, destVectors, modifiers);
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            public static void UnsafeFromVector4<TPixel>(
                Span<Vector4> sourceVectors,
                Span<TPixel> destPixels,
                PixelConversionModifiers modifiers)
                where TPixel : unmanaged, IPixel<TPixel>
            {
                ApplyBackwardConversionModifiers(sourceVectors, modifiers);

                if (modifiers.IsDefined(PixelConversionModifiers.Scale))
                {
                    UnsafeFromScaledVector4Core(sourceVectors, destPixels);
                }
                else
                {
                    UnsafeFromVector4Core(sourceVectors, destPixels);
                }
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            public static void UnsafeToVector4<TPixel>(
                ReadOnlySpan<TPixel> sourcePixels,
                Span<Vector4> destVectors,
                PixelConversionModifiers modifiers)
                where TPixel : unmanaged, IPixel<TPixel>
            {
                if (modifiers.IsDefined(PixelConversionModifiers.Scale))
                {
                    UnsafeToScaledVector4Core(sourcePixels, destVectors);
                }
                else
                {
                    UnsafeToVector4Core(sourcePixels, destVectors);
                }

                ApplyForwardConversionModifiers(destVectors, modifiers);
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            private static void UnsafeFromVector4Core<TPixel>(
                ReadOnlySpan<Vector4> sourceVectors,
                Span<TPixel> destPixels)
                where TPixel : unmanaged, IPixel<TPixel>
            {
                ref Vector4 sourceStart = ref MemoryMarshal.GetReference(sourceVectors);
                ref Vector4 sourceEnd = ref Unsafe.Add(ref sourceStart, sourceVectors.Length);
                ref TPixel destRef = ref MemoryMarshal.GetReference(destPixels);

                while (Unsafe.IsAddressLessThan(ref sourceStart, ref sourceEnd))
                {
                    destRef.FromVector4(sourceStart);

                    sourceStart = ref Unsafe.Add(ref sourceStart, 1);
                    destRef = ref Unsafe.Add(ref destRef, 1);
                }
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            private static void UnsafeToVector4Core<TPixel>(
                ReadOnlySpan<TPixel> sourcePixels,
                Span<Vector4> destVectors)
                where TPixel : unmanaged, IPixel<TPixel>
            {
                ref TPixel sourceStart = ref MemoryMarshal.GetReference(sourcePixels);
                ref TPixel sourceEnd = ref Unsafe.Add(ref sourceStart, sourcePixels.Length);
                ref Vector4 destRef = ref MemoryMarshal.GetReference(destVectors);

                while (Unsafe.IsAddressLessThan(ref sourceStart, ref sourceEnd))
                {
                    destRef = sourceStart.ToVector4();

                    sourceStart = ref Unsafe.Add(ref sourceStart, 1);
                    destRef = ref Unsafe.Add(ref destRef, 1);
                }
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            private static void UnsafeFromScaledVector4Core<TPixel>(
                ReadOnlySpan<Vector4> sourceVectors,
                Span<TPixel> destinationColors)
                where TPixel : unmanaged, IPixel<TPixel>
            {
                ref Vector4 sourceStart = ref MemoryMarshal.GetReference(sourceVectors);
                ref Vector4 sourceEnd = ref Unsafe.Add(ref sourceStart, sourceVectors.Length);
                ref TPixel destRef = ref MemoryMarshal.GetReference(destinationColors);

                while (Unsafe.IsAddressLessThan(ref sourceStart, ref sourceEnd))
                {
                    destRef.FromScaledVector4(sourceStart);

                    sourceStart = ref Unsafe.Add(ref sourceStart, 1);
                    destRef = ref Unsafe.Add(ref destRef, 1);
                }
            }

            [MethodImpl(InliningOptions.ShortMethod)]
            private static void UnsafeToScaledVector4Core<TPixel>(
                ReadOnlySpan<TPixel> sourceColors,
                Span<Vector4> destinationVectors)
                where TPixel : unmanaged, IPixel<TPixel>
            {
                ref TPixel sourceStart = ref MemoryMarshal.GetReference(sourceColors);
                ref TPixel sourceEnd = ref Unsafe.Add(ref sourceStart, sourceColors.Length);
                ref Vector4 destRef = ref MemoryMarshal.GetReference(destinationVectors);

                while (Unsafe.IsAddressLessThan(ref sourceStart, ref sourceEnd))
                {
                    destRef = sourceStart.ToScaledVector4();

                    sourceStart = ref Unsafe.Add(ref sourceStart, 1);
                    destRef = ref Unsafe.Add(ref destRef, 1);
                }
            }
        }
    }
}
