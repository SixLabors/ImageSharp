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
    /// Provides efficient implementations for batched conversion between RGBA-compatible pixel types and <see cref="Vector4"/> values.
    /// </summary>
    public static class RgbaCompatible
    {
        /// <summary>
        /// It's not worth to bother the transitive pixel conversion method below this limit.
        /// The value depends on the actual gain brought by the SIMD characteristics of the executing CPU and JIT.
        /// </summary>
        private static readonly int Vector4ConversionThreshold = CalculateVector4ConversionThreshold();

        /// <summary>
        /// Converts RGBA-compatible pixels to vectors with unassociated alpha.
        /// </summary>
        /// <typeparam name="TPixel">The type of pixel format.</typeparam>
        /// <param name="configuration">The configuration.</param>
        /// <param name="pixelOperations">The pixel operations instance.</param>
        /// <param name="source">The source buffer.</param>
        /// <param name="destination">The destination buffer.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void ToUnassociatedVector4<TPixel>(
            Configuration configuration,
            PixelOperations<TPixel> pixelOperations,
            ReadOnlySpan<TPixel> source,
            Span<Vector4> destination)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            if (source.Length < Vector4ConversionThreshold)
            {
                Default.UnsafeToUnassociatedVector4<TPixel>(source, destination);

                return;
            }

            UnpackVector4(configuration, pixelOperations, source, destination);
        }

        /// <summary>
        /// Converts RGBA-compatible pixels to vectors with associated alpha.
        /// </summary>
        /// <typeparam name="TPixel">The type of pixel format.</typeparam>
        /// <param name="configuration">The configuration.</param>
        /// <param name="pixelOperations">The pixel operations instance.</param>
        /// <param name="source">The source buffer.</param>
        /// <param name="destination">The destination buffer.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void ToAssociatedVector4<TPixel>(
            Configuration configuration,
            PixelOperations<TPixel> pixelOperations,
            ReadOnlySpan<TPixel> source,
            Span<Vector4> destination)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            if (source.Length < Vector4ConversionThreshold)
            {
                Default.UnsafeToAssociatedVector4<TPixel>(source, destination);

                return;
            }

            UnpackVector4(configuration, pixelOperations, source, destination);
            Numerics.Premultiply(destination[..source.Length]);
        }

        /// <summary>
        /// Converts vectors with unassociated alpha to RGBA-compatible pixels.
        /// </summary>
        /// <typeparam name="TPixel">The type of pixel format.</typeparam>
        /// <param name="configuration">The configuration.</param>
        /// <param name="pixelOperations">The pixel operations instance.</param>
        /// <param name="source">The source buffer.</param>
        /// <param name="destination">The destination buffer.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void FromUnassociatedVector4<TPixel>(
            Configuration configuration,
            PixelOperations<TPixel> pixelOperations,
            Span<Vector4> source,
            Span<TPixel> destination)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            if (source.Length < Vector4ConversionThreshold)
            {
                Default.UnsafeFromUnassociatedVector4<TPixel>(source, destination);

                return;
            }

            PackVector4(configuration, pixelOperations, source, destination);
        }

        /// <summary>
        /// Converts vectors with associated alpha to RGBA-compatible pixels.
        /// </summary>
        /// <typeparam name="TPixel">The type of pixel format.</typeparam>
        /// <param name="configuration">The configuration.</param>
        /// <param name="pixelOperations">The pixel operations instance.</param>
        /// <param name="source">The source buffer.</param>
        /// <param name="destination">The destination buffer.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void FromAssociatedVector4<TPixel>(
            Configuration configuration,
            PixelOperations<TPixel> pixelOperations,
            Span<Vector4> source,
            Span<TPixel> destination)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

            if (source.Length < Vector4ConversionThreshold)
            {
                Default.UnsafeFromAssociatedVector4<TPixel>(source, destination);

                return;
            }

            Numerics.UnPremultiply(source);
            PackVector4(configuration, pixelOperations, source, destination);
        }

        /// <summary>
        /// Unpacks RGBA-compatible pixels into normalized vectors using the shared staging layout.
        /// </summary>
        /// <typeparam name="TPixel">The type of pixel format.</typeparam>
        /// <param name="configuration">The configuration.</param>
        /// <param name="pixelOperations">The pixel operations instance.</param>
        /// <param name="source">The source buffer.</param>
        /// <param name="destination">The destination buffer.</param>
        private static void UnpackVector4<TPixel>(
            Configuration configuration,
            PixelOperations<TPixel> pixelOperations,
            ReadOnlySpan<TPixel> source,
            Span<Vector4> destination)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int count = source.Length;
            destination = destination[..count];

            // The destination tail holds packed RGBA staging while the earlier vectors expand to floats.
            int countWithoutLastItem = count - 1;
            ReadOnlySpan<TPixel> reducedSource = source[..countWithoutLastItem];
            Span<Rgba32> rgbaStaging = MemoryMarshal.Cast<Vector4, Rgba32>(destination).Slice((3 * count) + 1, countWithoutLastItem);
            pixelOperations.ToRgba32(configuration, reducedSource, rgbaStaging);

            // Staging overlaps the final output vector, which remains unwritten until the staged bytes are consumed.
            SimdUtils.ByteToNormalizedFloat(
                MemoryMarshal.Cast<Rgba32, byte>(rgbaStaging),
                MemoryMarshal.Cast<Vector4, float>(destination[..countWithoutLastItem]));

            destination[countWithoutLastItem] = source[countWithoutLastItem].ToUnassociatedVector4();
        }

        /// <summary>
        /// Packs normalized vectors into RGBA-compatible pixels.
        /// </summary>
        /// <typeparam name="TPixel">The type of pixel format.</typeparam>
        /// <param name="configuration">The configuration.</param>
        /// <param name="pixelOperations">The pixel operations instance.</param>
        /// <param name="source">The source buffer.</param>
        /// <param name="destination">The destination buffer.</param>
        private static void PackVector4<TPixel>(
            Configuration configuration,
            PixelOperations<TPixel> pixelOperations,
            Span<Vector4> source,
            Span<TPixel> destination)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int count = source.Length;

            // Source defines the conversion length. Do not expose spare destination capacity
            // to downstream shuffles whose scalar tail is destination-length driven.
            destination = destination[..count];

            // The opposite direction cannot stage inside the input because every vector is read by the saturating packer.
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
