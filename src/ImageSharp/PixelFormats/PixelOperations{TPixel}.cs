// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <summary>
    /// A stateless class implementing Strategy Pattern for batched pixel-data conversion operations
    /// for pixel buffers of type <typeparamref name="TPixel"/>.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    public partial class PixelOperations<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// It's not worth to bother the transitive pixel conversion method below this limit.
        /// The value depends on the actual gain brought by the SIMD characteristics of the executing CPU and JIT.
        /// </summary>
        private static readonly int Vector4ConversionThreshold = CalculateVector4ConversionThreshold();

        /// <summary>
        /// Gets the global <see cref="PixelOperations{TPixel}"/> instance for the pixel type <typeparamref name="TPixel"/>
        /// </summary>
        public static PixelOperations<TPixel> Instance { get; } = default(TPixel).CreatePixelOperations();

        /// <summary>
        /// Bulk version of <see cref="IPixel.FromVector4"/> converting 'sourceVectors.Length' pixels into 'destinationColors'.
        /// </summary>
        /// <param name="configuration">A <see cref="Configuration"/> to configure internal operations</param>
        /// <param name="sourceVectors">The <see cref="Span{T}"/> to the source vectors.</param>
        /// <param name="destPixels">The <see cref="Span{T}"/> to the destination colors.</param>
        internal virtual void FromVector4(
            Configuration configuration,
            ReadOnlySpan<Vector4> sourceVectors,
            Span<TPixel> destPixels)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(sourceVectors, destPixels, nameof(destPixels));

            FromVector4DefaultImpl(sourceVectors, destPixels);
        }

        /// <summary>
        /// Bulk version of <see cref="IPixel.ToVector4()"/> converting 'sourceColors.Length' pixels into 'destinationVectors'.
        /// </summary>
        /// <param name="configuration">A <see cref="Configuration"/> to configure internal operations</param>
        /// <param name="sourcePixels">The <see cref="Span{T}"/> to the source colors.</param>
        /// <param name="destVectors">The <see cref="Span{T}"/> to the destination vectors.</param>
        internal virtual void ToVector4(
            Configuration configuration,
            ReadOnlySpan<TPixel> sourcePixels,
            Span<Vector4> destVectors)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(sourcePixels, destVectors, nameof(destVectors));

            ToVector4DefaultImpl(sourcePixels, destVectors);
        }

        /// <summary>
        /// Bulk version of <see cref="IPixel.FromScaledVector4"/> converting 'sourceVectors.Length' pixels into 'destinationColors'.
        /// </summary>
        /// <param name="configuration">A <see cref="Configuration"/> to configure internal operations</param>
        /// <param name="sourceVectors">The <see cref="Span{T}"/> to the source vectors.</param>
        /// <param name="destinationColors">The <see cref="Span{T}"/> to the destination colors.</param>
        internal virtual void FromScaledVector4(
            Configuration configuration,
            ReadOnlySpan<Vector4> sourceVectors,
            Span<TPixel> destinationColors)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(sourceVectors, destinationColors, nameof(destinationColors));

            ref Vector4 sourceRef = ref MemoryMarshal.GetReference(sourceVectors);
            ref TPixel destRef = ref MemoryMarshal.GetReference(destinationColors);

            for (int i = 0; i < sourceVectors.Length; i++)
            {
                ref Vector4 sp = ref Unsafe.Add(ref sourceRef, i);
                ref TPixel dp = ref Unsafe.Add(ref destRef, i);
                dp.FromScaledVector4(sp);
            }
        }

        /// <summary>
        /// Bulk version of <see cref="IPixel.ToScaledVector4()"/> converting 'sourceColors.Length' pixels into 'destinationVectors'.
        /// </summary>
        /// <param name="configuration">A <see cref="Configuration"/> to configure internal operations</param>
        /// <param name="sourceColors">The <see cref="Span{T}"/> to the source colors.</param>
        /// <param name="destinationVectors">The <see cref="Span{T}"/> to the destination vectors.</param>
        internal virtual void ToScaledVector4(
            Configuration configuration,
            ReadOnlySpan<TPixel> sourceColors,
            Span<Vector4> destinationVectors)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(sourceColors, destinationVectors, nameof(destinationVectors));

            ref TPixel sourceRef = ref MemoryMarshal.GetReference(sourceColors);
            ref Vector4 destRef = ref MemoryMarshal.GetReference(destinationVectors);

            for (int i = 0; i < sourceColors.Length; i++)
            {
                ref TPixel sp = ref Unsafe.Add(ref sourceRef, i);
                ref Vector4 dp = ref Unsafe.Add(ref destRef, i);
                dp = sp.ToScaledVector4();
            }
        }

        /// <summary>
        /// Converts 'sourceColors.Length' pixels from 'sourceColors' into 'destinationColors'.
        /// </summary>
        /// <typeparam name="TDestinationPixel">The destination pixel type.</typeparam>
        /// <param name="configuration">A <see cref="Configuration"/> to configure internal operations</param>
        /// <param name="sourceColors">The <see cref="Span{T}"/> to the source colors.</param>
        /// <param name="destinationColors">The <see cref="Span{T}"/> to the destination colors.</param>
        internal virtual void To<TDestinationPixel>(
            Configuration configuration,
            ReadOnlySpan<TPixel> sourceColors,
            Span<TDestinationPixel> destinationColors)
            where TDestinationPixel : struct, IPixel<TDestinationPixel>
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(sourceColors, destinationColors, nameof(destinationColors));

            int count = sourceColors.Length;
            ref TPixel sourceRef = ref MemoryMarshal.GetReference(sourceColors);

            // Gray8 and Gray16 are special implementations of IPixel in that they do not conform to the
            // standard RGBA colorspace format and must be converted from RGBA using the special ITU BT709 alogrithm.
            // One of the requirements of FromScaledVector4/ToScaledVector4 is that it unaware of this and
            // packs/unpacks the pixel without and conversion so we employ custom methods do do this.
            if (typeof(TDestinationPixel) == typeof(Gray16))
            {
                ref Gray16 gray16Ref = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<TDestinationPixel, Gray16>(destinationColors));
                for (int i = 0; i < count; i++)
                {
                    ref TPixel sp = ref Unsafe.Add(ref sourceRef, i);
                    ref Gray16 dp = ref Unsafe.Add(ref gray16Ref, i);
                    dp.ConvertFromRgbaScaledVector4(sp.ToScaledVector4());
                }

                return;
            }

            if (typeof(TDestinationPixel) == typeof(Gray8))
            {
                ref Gray8 gray8Ref = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<TDestinationPixel, Gray8>(destinationColors));
                for (int i = 0; i < count; i++)
                {
                    ref TPixel sp = ref Unsafe.Add(ref sourceRef, i);
                    ref Gray8 dp = ref Unsafe.Add(ref gray8Ref, i);
                    dp.ConvertFromRgbaScaledVector4(sp.ToScaledVector4());
                }

                return;
            }

            // Normal conversion
            ref TDestinationPixel destRef = ref MemoryMarshal.GetReference(destinationColors);
            for (int i = 0; i < count; i++)
            {
                ref TPixel sp = ref Unsafe.Add(ref sourceRef, i);
                ref TDestinationPixel dp = ref Unsafe.Add(ref destRef, i);
                dp.FromScaledVector4(sp.ToScaledVector4());
            }
        }

        // TODO: The Vector4 helpers should be moved to a utility class.

        /// <summary>
        /// Provides an efficient default implementation for <see cref="ToVector4"/> and <see cref="ToScaledVector4"/>
        /// which is applicable for <see cref="Rgba32"/>-compatible pixel types where <see cref="IPixel.ToVector4"/>
        /// returns the same scaled result as <see cref="IPixel.ToScaledVector4"/>.
        /// The method is works by internally converting to a <see cref="Rgba32"/> therefore it's not applicable for that type!
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        internal void RunRgba32CompatibleToVector4Conversion(
            Configuration configuration,
            ReadOnlySpan<TPixel> sourcePixels,
            Span<Vector4> destVectors)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(sourcePixels, destVectors, nameof(destVectors));

            int count = sourcePixels.Length;

            // Not worth for small buffers:
            if (count < Vector4ConversionThreshold)
            {
                ToVector4DefaultImpl(sourcePixels, destVectors);
                return;
            }

            // Using the last quarter of 'destVectors' as a temporary buffer to avoid allocation:
            int countWithoutLastItem = count - 1;
            ReadOnlySpan<TPixel> reducedSource = sourcePixels.Slice(0, countWithoutLastItem);
            Span<Rgba32> lastQuarterOfDestBuffer = MemoryMarshal.Cast<Vector4, Rgba32>(destVectors).Slice((3 * count) + 1, countWithoutLastItem);
            this.ToRgba32(configuration, reducedSource, lastQuarterOfDestBuffer);

            // 'destVectors' and 'lastQuarterOfDestBuffer' are ovelapping buffers,
            // but we are always reading/writing at different positions:
            SimdUtils.BulkConvertByteToNormalizedFloat(
                MemoryMarshal.Cast<Rgba32, byte>(lastQuarterOfDestBuffer),
                MemoryMarshal.Cast<Vector4, float>(destVectors.Slice(0, countWithoutLastItem)));

            destVectors[countWithoutLastItem] = sourcePixels[countWithoutLastItem].ToVector4();
        }

        /// <summary>
        /// Provides an efficient default implementation for <see cref="FromVector4"/> and <see cref="FromScaledVector4"/>
        /// which is applicable for <see cref="Rgba32"/>-compatible pixel types where <see cref="IPixel.ToVector4"/>
        /// returns the same scaled result as <see cref="IPixel.ToScaledVector4"/>.
        /// The method is works by internally converting to a <see cref="Rgba32"/> therefore it's not applicable for that type!
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        internal void RunRgba32CompatibleFromVector4Conversion(
            Configuration configuration,
            ReadOnlySpan<Vector4> sourceVectors,
            Span<TPixel> destPixels)
        {
            Guard.NotNull(configuration, nameof(configuration));
            Guard.DestinationShouldNotBeTooShort(sourceVectors, destPixels, nameof(destPixels));

            int count = sourceVectors.Length;

            // Not worth for small buffers:
            if (count < Vector4ConversionThreshold)
            {
                FromVector4DefaultImpl(sourceVectors, destPixels);
                return;
            }

            // For the opposite direction it's not easy to implement the trick used in RunRgba32CompatibleToVector4Conversion,
            // so let's allocate a temporary buffer as usually:
            using (IMemoryOwner<Rgba32> tempBuffer = configuration.MemoryAllocator.Allocate<Rgba32>(count))
            {
                Span<Rgba32> tempSpan = tempBuffer.Memory.Span;

                SimdUtils.BulkConvertNormalizedFloatToByteClampOverflows(
                    MemoryMarshal.Cast<Vector4, float>(sourceVectors),
                    MemoryMarshal.Cast<Rgba32, byte>(tempSpan));

                this.FromRgba32(configuration, tempSpan, destPixels);
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static void FromVector4DefaultImpl(ReadOnlySpan<Vector4> sourceVectors, Span<TPixel> destPixels)
        {
            ref Vector4 sourceRef = ref MemoryMarshal.GetReference(sourceVectors);
            ref TPixel destRef = ref MemoryMarshal.GetReference(destPixels);

            for (int i = 0; i < sourceVectors.Length; i++)
            {
                ref Vector4 sp = ref Unsafe.Add(ref sourceRef, i);
                ref TPixel dp = ref Unsafe.Add(ref destRef, i);
                dp.FromVector4(sp);
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static void ToVector4DefaultImpl(ReadOnlySpan<TPixel> sourcePixels, Span<Vector4> destVectors)
        {
            ref TPixel sourceRef = ref MemoryMarshal.GetReference(sourcePixels);
            ref Vector4 destRef = ref MemoryMarshal.GetReference(destVectors);

            for (int i = 0; i < sourcePixels.Length; i++)
            {
                ref TPixel sp = ref Unsafe.Add(ref sourceRef, i);
                ref Vector4 dp = ref Unsafe.Add(ref destRef, i);
                dp = sp.ToVector4();
            }
        }

        private static int CalculateVector4ConversionThreshold()
        {
            if (!Vector.IsHardwareAccelerated)
            {
                return int.MaxValue;
            }

            return SimdUtils.ExtendedIntrinsics.IsAvailable && SimdUtils.IsAvx2CompatibleArchitecture ? 256 : 128;
        }
    }
}