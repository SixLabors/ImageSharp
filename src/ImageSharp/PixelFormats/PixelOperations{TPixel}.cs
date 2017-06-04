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
        /// Bulk version of <see cref="IPixel.PackFromRgba32(Rgba32)"/> that converts data in <see cref="ComponentOrder.Xyz"/>.
        /// </summary>
        /// <param name="sourceBytes">The <see cref="Span{T}"/> to the source bytes.</param>
        /// <param name="destColors">The <see cref="Span{T}"/> to the destination colors.</param>
        /// <param name="count">The number of pixels to convert.</param>
        internal virtual void PackFromXyzBytes(Span<byte> sourceBytes, Span<TPixel> destColors, int count)
        {
            this.PackFromRgb24(sourceBytes.NonPortableCast<byte, Rgb24>(), destColors, count);
        }

        internal virtual void PackFromRgb24(Span<Rgb24> source, Span<TPixel> destPixels, int count)
        {
            Guard.MustBeSizedAtLeast(source, count, nameof(source));
            Guard.MustBeSizedAtLeast(destPixels, count, nameof(destPixels));

            ref Rgb24 sourceRef = ref source.DangerousGetPinnableReference();
            ref TPixel destRef = ref destPixels.DangerousGetPinnableReference();

            Rgba32 rgba = new Rgba32(0, 0, 0, 255);

            for (int i = 0; i < count; i++)
            {
                ref TPixel dp = ref Unsafe.Add(ref destRef, i);
                rgba.Rgb = Unsafe.Add(ref sourceRef, i);
                dp.PackFromRgba32(rgba);
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
        /// Bulk version of <see cref="IPixel.PackFromRgba32(Rgba32)"/> that converts data in <see cref="ComponentOrder.Xyzw"/>.
        /// </summary>
        /// <param name="sourceBytes">The <see cref="Span{T}"/> to the source bytes.</param>
        /// <param name="destColors">The <see cref="Span{T}"/> to the destination colors.</param>
        /// <param name="count">The number of pixels to convert.</param>
        internal virtual void PackFromXyzwBytes(Span<byte> sourceBytes, Span<TPixel> destColors, int count)
        {
            this.PackFromRgba32(sourceBytes.NonPortableCast<byte, Rgba32>(), destColors, count);
        }

        internal virtual void PackFromRgba32(Span<Rgba32> source, Span<TPixel> destPixels, int count)
        {
            Guard.MustBeSizedAtLeast(source, count, nameof(source));
            Guard.MustBeSizedAtLeast(destPixels, count, nameof(destPixels));

            ref Rgba32 sourceRef = ref source.DangerousGetPinnableReference();
            ref TPixel destRef = ref destPixels.DangerousGetPinnableReference();

            Rgba32 rgba = new Rgba32(0, 0, 0, 255);

            for (int i = 0; i < count; i++)
            {
                ref TPixel dp = ref Unsafe.Add(ref destRef, i);
                rgba = Unsafe.Add(ref sourceRef, i);
                dp.PackFromRgba32(rgba);
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
        /// Bulk version of <see cref="IPixel.PackFromRgba32(Rgba32)"/> that converts data in <see cref="ComponentOrder.Zyx"/>.
        /// </summary>
        /// <param name="sourceBytes">The <see cref="Span{T}"/> to the source bytes.</param>
        /// <param name="destColors">The <see cref="Span{T}"/> to the destination colors.</param>
        /// <param name="count">The number of pixels to convert.</param>
        internal virtual void PackFromZyxBytes(Span<byte> sourceBytes, Span<TPixel> destColors, int count)
        {
            this.PackFromBgr24(sourceBytes.NonPortableCast<byte, Bgr24>(), destColors, count);
        }

        internal virtual void PackFromBgr24(Span<Bgr24> source, Span<TPixel> destPixels, int count)
        {
            Guard.MustBeSizedAtLeast(source, count, nameof(source));
            Guard.MustBeSizedAtLeast(destPixels, count, nameof(destPixels));

            ref Bgr24 sourceRef = ref source.DangerousGetPinnableReference();
            ref TPixel destRef = ref destPixels.DangerousGetPinnableReference();

            Rgba32 rgba = new Rgba32(0, 0, 0, 255);

            for (int i = 0; i < count; i++)
            {
                ref TPixel dp = ref Unsafe.Add(ref destRef, i);
                rgba.Bgr = Unsafe.Add(ref sourceRef, i);
                dp.PackFromRgba32(rgba);
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
        /// Bulk version of <see cref="IPixel.PackFromRgba32(Rgba32)"/> that converts data in <see cref="ComponentOrder.Zyxw"/>.
        /// </summary>
        /// <param name="sourceBytes">The <see cref="Span{T}"/> to the source bytes.</param>
        /// <param name="destColors">The <see cref="Span{T}"/> to the destination colors.</param>
        /// <param name="count">The number of pixels to convert.</param>
        internal virtual void PackFromZyxwBytes(Span<byte> sourceBytes, Span<TPixel> destColors, int count)
        {
            this.PackFromBgra32(sourceBytes.NonPortableCast<byte, Bgra32>(), destColors, count);
        }

        internal virtual void PackFromBgra32(Span<Bgra32> source, Span<TPixel> destPixels, int count)
        {
            Guard.MustBeSizedAtLeast(source, count, nameof(source));
            Guard.MustBeSizedAtLeast(destPixels, count, nameof(destPixels));

            ref Bgra32 sourceRef = ref source.DangerousGetPinnableReference();
            ref TPixel destRef = ref destPixels.DangerousGetPinnableReference();
            Rgba32 rgba = new Rgba32(0, 0, 0, 255);

            for (int i = 0; i < count; i++)
            {
                ref TPixel dp = ref Unsafe.Add(ref destRef, i);
                rgba = Unsafe.Add(ref sourceRef, i).ToRgba32();
                dp.PackFromRgba32(rgba);
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