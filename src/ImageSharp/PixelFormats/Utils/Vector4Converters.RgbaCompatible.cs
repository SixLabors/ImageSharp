// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.PixelFormats.Utils
{
    /// <content>
    /// Contains <see cref="RgbaCompatible"/>
    /// </content>
    internal static partial class Vector4Converters
    {
        /// <summary>
        /// Provides efficient implementations for batched to/from <see cref="Vector4"/> conversion.
        /// which is applicable for <see cref="Rgba32"/>-compatible pixel types where <see cref="IPixel.ToVector4"/>
        /// returns the same scaled result as <see cref="IPixel.ToScaledVector4"/>.
        /// The method is works by internally converting to a <see cref="Rgba32"/> therefore it's not applicable for that type!
        /// </summary>
        public static class RgbaCompatible
        {
            /// <summary>
            /// It's not worth to bother the transitive pixel conversion method below this limit.
            /// The value depends on the actual gain brought by the SIMD characteristics of the executing CPU and JIT.
            /// </summary>
            private static readonly int Vector4ConversionThreshold = CalculateVector4ConversionThreshold();

            /// <summary>
            /// Provides an efficient default implementation for <see cref="PixelOperations{TPixel}.ToVector4(SixLabors.ImageSharp.Configuration,System.ReadOnlySpan{TPixel},System.Span{System.Numerics.Vector4},SixLabors.ImageSharp.PixelFormats.PixelConversionModifiers)"/>
            /// The method works by internally converting to a <see cref="Rgba32"/> therefore it's not applicable for that type!
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            internal static void ToVector4<TPixel>(
                Configuration configuration,
                PixelOperations<TPixel> pixelOperations,
                ReadOnlySpan<TPixel> sourcePixels,
                Span<Vector4> destVectors,
                PixelConversionModifiers modifiers)
                where TPixel : unmanaged, IPixel<TPixel>
            {
                Guard.NotNull(configuration, nameof(configuration));
                Guard.DestinationShouldNotBeTooShort(sourcePixels, destVectors, nameof(destVectors));

                int count = sourcePixels.Length;

                // Not worth for small buffers:
                if (count < Vector4ConversionThreshold)
                {
                    Default.UnsafeToVector4(sourcePixels, destVectors, modifiers);

                    return;
                }

                // Using the last quarter of 'destVectors' as a temporary buffer to avoid allocation:
                int countWithoutLastItem = count - 1;
                ReadOnlySpan<TPixel> reducedSource = sourcePixels.Slice(0, countWithoutLastItem);
                Span<Rgba32> lastQuarterOfDestBuffer = MemoryMarshal.Cast<Vector4, Rgba32>(destVectors).Slice((3 * count) + 1, countWithoutLastItem);
                pixelOperations.ToRgba32(configuration, reducedSource, lastQuarterOfDestBuffer);

                // 'destVectors' and 'lastQuarterOfDestBuffer' are overlapping buffers,
                // but we are always reading/writing at different positions:
                SimdUtils.ByteToNormalizedFloat(
                    MemoryMarshal.Cast<Rgba32, byte>(lastQuarterOfDestBuffer),
                    MemoryMarshal.Cast<Vector4, float>(destVectors.Slice(0, countWithoutLastItem)));

                destVectors[countWithoutLastItem] = sourcePixels[countWithoutLastItem].ToVector4();

                // TODO: Investigate optimized 1-pass approach!
                ApplyForwardConversionModifiers(destVectors, modifiers);
            }

            /// <summary>
            /// Provides an efficient default implementation for <see cref="PixelOperations{TPixel}.FromVector4Destructive(SixLabors.ImageSharp.Configuration,System.Span{System.Numerics.Vector4},System.Span{TPixel},SixLabors.ImageSharp.PixelFormats.PixelConversionModifiers)"/>
            /// The method is works by internally converting to a <see cref="Rgba32"/> therefore it's not applicable for that type!
            /// </summary>
            [MethodImpl(InliningOptions.ShortMethod)]
            internal static void FromVector4<TPixel>(
                Configuration configuration,
                PixelOperations<TPixel> pixelOperations,
                Span<Vector4> sourceVectors,
                Span<TPixel> destPixels,
                PixelConversionModifiers modifiers)
                where TPixel : unmanaged, IPixel<TPixel>
            {
                Guard.NotNull(configuration, nameof(configuration));
                Guard.DestinationShouldNotBeTooShort(sourceVectors, destPixels, nameof(destPixels));

                int count = sourceVectors.Length;

                // Not worth for small buffers:
                if (count < Vector4ConversionThreshold)
                {
                    Default.UnsafeFromVector4(sourceVectors, destPixels, modifiers);

                    return;
                }

                // TODO: Investigate optimized 1-pass approach!
                ApplyBackwardConversionModifiers(sourceVectors, modifiers);

                // For the opposite direction it's not easy to implement the trick used in RunRgba32CompatibleToVector4Conversion,
                // so let's allocate a temporary buffer as usually:
                using (IMemoryOwner<Rgba32> tempBuffer = configuration.MemoryAllocator.Allocate<Rgba32>(count))
                {
                    Span<Rgba32> tempSpan = tempBuffer.Memory.Span;

                    SimdUtils.NormalizedFloatToByteSaturate(
                        MemoryMarshal.Cast<Vector4, float>(sourceVectors),
                        MemoryMarshal.Cast<Rgba32, byte>(tempSpan));

                    pixelOperations.FromRgba32(configuration, tempSpan, destPixels);
                }
            }

            private static int CalculateVector4ConversionThreshold()
            {
                if (!Vector.IsHardwareAccelerated)
                {
                    return int.MaxValue;
                }

                return SimdUtils.ExtendedIntrinsics.IsAvailable && SimdUtils.HasVector8 ? 256 : 128;
            }
        }
    }
}
