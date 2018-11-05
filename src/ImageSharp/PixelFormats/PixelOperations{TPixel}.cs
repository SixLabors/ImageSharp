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

            Utils.Vector4Converters.Default.DangerousFromVector4(sourceVectors, destPixels);
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

            Utils.Vector4Converters.Default.DangerousToVector4(sourcePixels, destVectors);
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

            Utils.Vector4Converters.Default.DangerousFromScaledVector4(sourceVectors, destinationColors);
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

            Utils.Vector4Converters.Default.DangerousToScaledVector4(sourceColors, destinationVectors);
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
            // standard RGBA colorspace format and must be converted from RGBA using the special ITU BT709 algorithm.
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
    }
}