// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.PixelFormats.Utils;

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
        /// Provides an efficient default implementation for <see cref="PixelOperations{TPixel}.ToVector4(Configuration,ReadOnlySpan{TPixel},Span{Vector4},PixelConversionModifiers)"/>
        /// The method works by internally converting to a <see cref="Rgba32"/> therefore it's not applicable for that type!
        /// </summary>
        /// <typeparam name="TPixel">The type of pixel format.</typeparam>
        /// <param name="configuration">The configuration.</param>
        /// <param name="pixelOperations">The pixel operations instance.</param>
        /// <param name="source">The source buffer.</param>
        /// <param name="destination">The destination buffer.</param>
        /// <param name="modifiers">The conversion modifier flags.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void ToVector4<TPixel>(
            Configuration configuration,
            PixelOperations<TPixel> pixelOperations,
            ReadOnlySpan<TPixel> source,
            Span<Vector4> destination,
            PixelConversionModifiers modifiers)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            int count = source.Length;

            // Not worth for small buffers:
            if (count < Vector4ConversionThreshold)
            {
                Default.UnsafeToVector4(source, destination, modifiers);

                return;
            }

            // Using the last quarter of 'destination' as a temporary buffer to avoid allocation:
            int countWithoutLastItem = count - 1;
            ReadOnlySpan<TPixel> reducedSource = source[..countWithoutLastItem];
            Span<Rgba32> lastQuarterOfDestination = MemoryMarshal.Cast<Vector4, Rgba32>(destination).Slice((3 * count) + 1, countWithoutLastItem);
            pixelOperations.ToRgba32(configuration, reducedSource, lastQuarterOfDestination);

            // 'destination' and 'lastQuarterOfDestination' are overlapping buffers,
            // but we are always reading/writing at different positions:
            SimdUtils.ByteToNormalizedFloat(
                MemoryMarshal.Cast<Rgba32, byte>(lastQuarterOfDestination),
                MemoryMarshal.Cast<Vector4, float>(destination[..countWithoutLastItem]));

            destination[countWithoutLastItem] = source[countWithoutLastItem].ToVector4();

            // TODO: Investigate optimized 1-pass approach!
            ApplyForwardConversionModifiers(destination, modifiers);
        }

        /// <summary>
        /// Provides an efficient default implementation for <see cref="PixelOperations{TPixel}.FromVector4Destructive(Configuration,Span{Vector4},Span{TPixel},PixelConversionModifiers)"/>
        /// The method is works by internally converting to a <see cref="Rgba32"/> therefore it's not applicable for that type!
        /// </summary>
        /// <typeparam name="TPixel">The type of pixel format.</typeparam>
        /// <param name="configuration">The configuration.</param>
        /// <param name="pixelOperations">The pixel operations instance.</param>
        /// <param name="source">The source buffer.</param>
        /// <param name="destination">The destination buffer.</param>
        /// <param name="modifiers">The conversion modifier flags.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void FromVector4<TPixel>(
            Configuration configuration,
            PixelOperations<TPixel> pixelOperations,
            Span<Vector4> source,
            Span<TPixel> destination,
            PixelConversionModifiers modifiers)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            int count = source.Length;

            // Not worth for small buffers:
            if (count < Vector4ConversionThreshold)
            {
                Default.UnsafeFromVector4(source, destination, modifiers);

                return;
            }

            // TODO: Investigate optimized 1-pass approach!
            ApplyBackwardConversionModifiers(source, modifiers);

            // For the opposite direction it's not easy to implement the trick used in RunRgba32CompatibleToVector4Conversion,
            // so let's allocate a temporary buffer as usually:
            using IMemoryOwner<Rgba32> tempBuffer = configuration.MemoryAllocator.Allocate<Rgba32>(count);
            Span<Rgba32> tempSpan = tempBuffer.Memory.Span;

            SimdUtils.NormalizedFloatToByteSaturate(
                MemoryMarshal.Cast<Vector4, float>(source),
                MemoryMarshal.Cast<Rgba32, byte>(tempSpan));

            pixelOperations.FromRgba32(configuration, tempSpan, destination);
        }

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
    }
}
