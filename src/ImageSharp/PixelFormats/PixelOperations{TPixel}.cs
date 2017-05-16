// <copyright file="PixelOperations{TPixel}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.PixelFormats
{
    using System;
    using System.Numerics;
    using System.Runtime.CompilerServices;

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
        /// Bulk version of <see cref="IPixel.PackFromVector4(Vector4)"/>
        /// </summary>
        /// <param name="sourceVectors">The <see cref="Span{T}"/> to the source vectors.</param>
        /// <param name="destColors">The <see cref="Span{T}"/> to the destination colors.</param>
        /// <param name="count">The number of pixels to convert.</param>
        internal virtual void PackFromVector4(Span<Vector4> sourceVectors, Span<TPixel> destColors, int count)
        {
            Guard.MustBeSizedAtLeast(sourceVectors, count, nameof(sourceVectors));
            Guard.MustBeSizedAtLeast(destColors, count, nameof(destColors));

            ref Vector4 sourceRef = ref sourceVectors.DangerousGetPinnableReference();
            ref TPixel destRef = ref destColors.DangerousGetPinnableReference();

            for (int i = 0; i < count; i++)
            {
                ref Vector4 sp = ref Unsafe.Add(ref sourceRef, i);
                ref TPixel dp = ref Unsafe.Add(ref destRef, i);
                dp.PackFromVector4(sp);
            }
        }

        /// <summary>
        /// Bulk version of <see cref="IPixel.ToVector4()"/>.
        /// </summary>
        /// <param name="sourceColors">The <see cref="Span{T}"/> to the source colors.</param>
        /// <param name="destVectors">The <see cref="Span{T}"/> to the destination vectors.</param>
        /// <param name="count">The number of pixels to convert.</param>
        internal virtual void ToVector4(Span<TPixel> sourceColors, Span<Vector4> destVectors, int count)
        {
            Guard.MustBeSizedAtLeast(sourceColors, count, nameof(sourceColors));
            Guard.MustBeSizedAtLeast(destVectors, count, nameof(destVectors));

            ref TPixel sourceRef = ref sourceColors.DangerousGetPinnableReference();
            ref Vector4 destRef = ref destVectors.DangerousGetPinnableReference();

            for (int i = 0; i < count; i++)
            {
                ref TPixel sp = ref Unsafe.Add(ref sourceRef, i);
                ref Vector4 dp = ref Unsafe.Add(ref destRef, i);
                dp = sp.ToVector4();
            }
        }

        /// <summary>
        /// Bulk version of <see cref="IPixel.PackFromBytes(byte, byte, byte, byte)"/> that converts data in <see cref="ComponentOrder.Xyz"/>.
        /// </summary>
        /// <param name="sourceBytes">The <see cref="Span{T}"/> to the source bytes.</param>
        /// <param name="destColors">The <see cref="Span{T}"/> to the destination colors.</param>
        /// <param name="count">The number of pixels to convert.</param>
        internal virtual void PackFromXyzBytes(Span<byte> sourceBytes, Span<TPixel> destColors, int count)
        {
            Guard.MustBeSizedAtLeast(sourceBytes, count * 3, nameof(sourceBytes));
            Guard.MustBeSizedAtLeast(destColors, count, nameof(destColors));

            ref byte sourceRef = ref sourceBytes.DangerousGetPinnableReference();
            ref TPixel destRef = ref destColors.DangerousGetPinnableReference();

            for (int i = 0; i < count; i++)
            {
                int i3 = i * 3;
                ref TPixel dp = ref Unsafe.Add(ref destRef, i);
                dp.PackFromBytes(
                    Unsafe.Add(ref sourceRef, i3),
                    Unsafe.Add(ref sourceRef, i3 + 1),
                    Unsafe.Add(ref sourceRef, i3 + 2),
                    255);
            }
        }

        /// <summary>
        /// Bulk version of <see cref="PixelConversionExtensions.ToXyzBytes{TPixel}(TPixel, Span{byte}, int)"/>.
        /// </summary>
        /// <param name="sourceColors">The <see cref="Span{T}"/> to the source colors.</param>
        /// <param name="destBytes">The <see cref="Span{T}"/> to the destination bytes.</param>
        /// <param name="count">The number of pixels to convert.</param>
        internal virtual void ToXyzBytes(Span<TPixel> sourceColors, Span<byte> destBytes, int count)
        {
            Guard.MustBeSizedAtLeast(sourceColors, count, nameof(sourceColors));
            Guard.MustBeSizedAtLeast(destBytes, count * 3, nameof(destBytes));

            ref TPixel sourceRef = ref sourceColors.DangerousGetPinnableReference();

            for (int i = 0; i < count; i++)
            {
                ref TPixel sp = ref Unsafe.Add(ref sourceRef, i);
                sp.ToXyzBytes(destBytes, i * 3);
            }
        }

        /// <summary>
        /// Bulk version of <see cref="IPixel.PackFromBytes(byte, byte, byte, byte)"/> that converts data in <see cref="ComponentOrder.Xyzw"/>.
        /// </summary>
        /// <param name="sourceBytes">The <see cref="Span{T}"/> to the source bytes.</param>
        /// <param name="destColors">The <see cref="Span{T}"/> to the destination colors.</param>
        /// <param name="count">The number of pixels to convert.</param>
        internal virtual void PackFromXyzwBytes(Span<byte> sourceBytes, Span<TPixel> destColors, int count)
        {
            Guard.MustBeSizedAtLeast(sourceBytes, count * 4, nameof(sourceBytes));
            Guard.MustBeSizedAtLeast(destColors, count, nameof(destColors));

            ref byte sourceRef = ref sourceBytes.DangerousGetPinnableReference();
            ref TPixel destRef = ref destColors.DangerousGetPinnableReference();

            for (int i = 0; i < count; i++)
            {
                int i4 = i * 4;
                ref TPixel dp = ref Unsafe.Add(ref destRef, i);
                dp.PackFromBytes(
                    Unsafe.Add(ref sourceRef, i4),
                    Unsafe.Add(ref sourceRef, i4 + 1),
                    Unsafe.Add(ref sourceRef, i4 + 2),
                    Unsafe.Add(ref sourceRef, i4 + 3));
            }
        }

        /// <summary>
        /// Bulk version of <see cref="PixelConversionExtensions.ToXyzwBytes{TPixel}(TPixel, Span{byte}, int)"/>
        /// </summary>
        /// <param name="sourceColors">The <see cref="Span{T}"/> to the source colors.</param>
        /// <param name="destBytes">The <see cref="Span{T}"/> to the destination bytes.</param>
        /// <param name="count">The number of pixels to convert.</param>
        internal virtual void ToXyzwBytes(Span<TPixel> sourceColors, Span<byte> destBytes, int count)
        {
            Guard.MustBeSizedAtLeast(sourceColors, count, nameof(sourceColors));
            Guard.MustBeSizedAtLeast(destBytes, count * 4, nameof(destBytes));

            ref TPixel sourceRef = ref sourceColors.DangerousGetPinnableReference();

            for (int i = 0; i < count; i++)
            {
                ref TPixel sp = ref Unsafe.Add(ref sourceRef, i);
                sp.ToXyzwBytes(destBytes, i * 4);
            }
        }

        /// <summary>
        /// Bulk version of <see cref="IPixel.PackFromBytes(byte, byte, byte, byte)"/> that converts data in <see cref="ComponentOrder.Zyx"/>.
        /// </summary>
        /// <param name="sourceBytes">The <see cref="Span{T}"/> to the source bytes.</param>
        /// <param name="destColors">The <see cref="Span{T}"/> to the destination colors.</param>
        /// <param name="count">The number of pixels to convert.</param>
        internal virtual void PackFromZyxBytes(Span<byte> sourceBytes, Span<TPixel> destColors, int count)
        {
            Guard.MustBeSizedAtLeast(sourceBytes, count * 3, nameof(sourceBytes));
            Guard.MustBeSizedAtLeast(destColors, count, nameof(destColors));

            ref byte sourceRef = ref sourceBytes.DangerousGetPinnableReference();
            ref TPixel destRef = ref destColors.DangerousGetPinnableReference();

            for (int i = 0; i < count; i++)
            {
                int i3 = i * 3;
                ref TPixel dp = ref Unsafe.Add(ref destRef, i);
                dp.PackFromBytes(
                    Unsafe.Add(ref sourceRef, i3 + 2),
                    Unsafe.Add(ref sourceRef, i3 + 1),
                    Unsafe.Add(ref sourceRef, i3),
                    255);
            }
        }

        /// <summary>
        /// Bulk version of <see cref="PixelConversionExtensions.ToZyxBytes{TPixel}(TPixel, Span{byte}, int)"/>.
        /// </summary>
        /// <param name="sourceColors">The <see cref="Span{T}"/> to the source colors.</param>
        /// <param name="destBytes">The <see cref="Span{T}"/> to the destination bytes.</param>
        /// <param name="count">The number of pixels to convert.</param>
        internal virtual void ToZyxBytes(Span<TPixel> sourceColors, Span<byte> destBytes, int count)
        {
            Guard.MustBeSizedAtLeast(sourceColors, count, nameof(sourceColors));
            Guard.MustBeSizedAtLeast(destBytes, count * 3, nameof(destBytes));

            ref TPixel sourceRef = ref sourceColors.DangerousGetPinnableReference();

            for (int i = 0; i < count; i++)
            {
                ref TPixel sp = ref Unsafe.Add(ref sourceRef, i);
                sp.ToZyxBytes(destBytes, i * 3);
            }
        }

        /// <summary>
        /// Bulk version of <see cref="IPixel.PackFromBytes(byte, byte, byte, byte)"/> that converts data in <see cref="ComponentOrder.Zyxw"/>.
        /// </summary>
        /// <param name="sourceBytes">The <see cref="Span{T}"/> to the source bytes.</param>
        /// <param name="destColors">The <see cref="Span{T}"/> to the destination colors.</param>
        /// <param name="count">The number of pixels to convert.</param>
        internal virtual void PackFromZyxwBytes(Span<byte> sourceBytes, Span<TPixel> destColors, int count)
        {
            Guard.MustBeSizedAtLeast(sourceBytes, count * 4, nameof(sourceBytes));
            Guard.MustBeSizedAtLeast(destColors, count, nameof(destColors));

            ref byte sourceRef = ref sourceBytes.DangerousGetPinnableReference();
            ref TPixel destRef = ref destColors.DangerousGetPinnableReference();

            for (int i = 0; i < count; i++)
            {
                int i4 = i * 4;
                ref TPixel dp = ref Unsafe.Add(ref destRef, i);
                dp.PackFromBytes(
                    Unsafe.Add(ref sourceRef, i4 + 2),
                    Unsafe.Add(ref sourceRef, i4 + 1),
                    Unsafe.Add(ref sourceRef, i4),
                    Unsafe.Add(ref sourceRef, i4 + 3));
            }
        }

        /// <summary>
        /// Bulk version of <see cref="PixelConversionExtensions.ToZyxwBytes{TPixel}(TPixel, Span{byte}, int)"/>.
        /// </summary>
        /// <param name="sourceColors">The <see cref="Span{T}"/> to the source colors.</param>
        /// <param name="destBytes">The <see cref="Span{T}"/> to the destination bytes.</param>
        /// <param name="count">The number of pixels to convert.</param>
        internal virtual void ToZyxwBytes(Span<TPixel> sourceColors, Span<byte> destBytes, int count)
        {
            Guard.MustBeSizedAtLeast(sourceColors, count, nameof(sourceColors));
            Guard.MustBeSizedAtLeast(destBytes, count * 4, nameof(destBytes));

            ref TPixel sourceRef = ref sourceColors.DangerousGetPinnableReference();

            for (int i = 0; i < count; i++)
            {
                ref TPixel sp = ref Unsafe.Add(ref sourceRef, i);
                sp.ToZyxwBytes(destBytes, i * 4);
            }
        }
    }
}