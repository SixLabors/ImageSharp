// <copyright file="BulkPixelOperations{TPixel}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.PixelFormats
{
    using System.Numerics;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// A stateless class implementing Strategy Pattern for batched pixel-data conversion operations
    /// for pixel buffers of type <typeparamref name="TPixel"/>.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    public class BulkPixelOperations<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Gets the global <see cref="BulkPixelOperations{TPixel}"/> instance for the pixel type <typeparamref name="TPixel"/>
        /// </summary>
        public static BulkPixelOperations<TPixel> Instance { get; } = default(TPixel).CreateBulkOperations();

        /// <summary>
        /// Bulk version of <see cref="IPixel.PackFromVector4(Vector4)"/>
        /// </summary>
        /// <param name="sourceVectors">The <see cref="BufferSpan{T}"/> to the source vectors.</param>
        /// <param name="destColors">The <see cref="BufferSpan{T}"/> to the destination colors.</param>
        /// <param name="count">The number of pixels to convert.</param>
        internal virtual void PackFromVector4(BufferSpan<Vector4> sourceVectors, BufferSpan<TPixel> destColors, int count)
        {
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
        /// <param name="sourceColors">The <see cref="BufferSpan{T}"/> to the source colors.</param>
        /// <param name="destVectors">The <see cref="BufferSpan{T}"/> to the destination vectors.</param>
        /// <param name="count">The number of pixels to convert.</param>
        internal virtual void ToVector4(BufferSpan<TPixel> sourceColors, BufferSpan<Vector4> destVectors, int count)
        {
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
        /// <param name="sourceBytes">The <see cref="BufferSpan{T}"/> to the source bytes.</param>
        /// <param name="destColors">The <see cref="BufferSpan{T}"/> to the destination colors.</param>
        /// <param name="count">The number of pixels to convert.</param>
        internal virtual void PackFromXyzBytes(BufferSpan<byte> sourceBytes, BufferSpan<TPixel> destColors, int count)
        {
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
        /// Bulk version of <see cref="IPixel.ToXyzBytes(byte[], int)"/>.
        /// </summary>
        /// <param name="sourceColors">The <see cref="BufferSpan{T}"/> to the source colors.</param>
        /// <param name="destBytes">The <see cref="BufferSpan{T}"/> to the destination bytes.</param>
        /// <param name="count">The number of pixels to convert.</param>
        internal virtual void ToXyzBytes(BufferSpan<TPixel> sourceColors, BufferSpan<byte> destBytes, int count)
        {
            ref TPixel sourceRef = ref sourceColors.DangerousGetPinnableReference();
            byte[] dest = destBytes.Array;

            for (int i = 0; i < count; i++)
            {
                ref TPixel sp = ref Unsafe.Add(ref sourceRef, i);
                sp.ToXyzBytes(dest, destBytes.Start + (i * 3));
            }
        }

        /// <summary>
        /// Bulk version of <see cref="IPixel.PackFromBytes(byte, byte, byte, byte)"/> that converts data in <see cref="ComponentOrder.Xyzw"/>.
        /// </summary>
        /// <param name="sourceBytes">The <see cref="BufferSpan{T}"/> to the source bytes.</param>
        /// <param name="destColors">The <see cref="BufferSpan{T}"/> to the destination colors.</param>
        /// <param name="count">The number of pixels to convert.</param>
        internal virtual void PackFromXyzwBytes(BufferSpan<byte> sourceBytes, BufferSpan<TPixel> destColors, int count)
        {
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
        /// Bulk version of <see cref="IPixel.ToXyzwBytes(byte[], int)"/>.
        /// </summary>
        /// <param name="sourceColors">The <see cref="BufferSpan{T}"/> to the source colors.</param>
        /// <param name="destBytes">The <see cref="BufferSpan{T}"/> to the destination bytes.</param>
        /// <param name="count">The number of pixels to convert.</param>
        internal virtual void ToXyzwBytes(BufferSpan<TPixel> sourceColors, BufferSpan<byte> destBytes, int count)
        {
            ref TPixel sourceRef = ref sourceColors.DangerousGetPinnableReference();
            byte[] dest = destBytes.Array;

            for (int i = 0; i < count; i++)
            {
                ref TPixel sp = ref Unsafe.Add(ref sourceRef, i);
                sp.ToXyzwBytes(dest, destBytes.Start + (i * 4));
            }
        }

        /// <summary>
        /// Bulk version of <see cref="IPixel.PackFromBytes(byte, byte, byte, byte)"/> that converts data in <see cref="ComponentOrder.Zyx"/>.
        /// </summary>
        /// <param name="sourceBytes">The <see cref="BufferSpan{T}"/> to the source bytes.</param>
        /// <param name="destColors">The <see cref="BufferSpan{T}"/> to the destination colors.</param>
        /// <param name="count">The number of pixels to convert.</param>
        internal virtual void PackFromZyxBytes(BufferSpan<byte> sourceBytes, BufferSpan<TPixel> destColors, int count)
        {
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
        /// Bulk version of <see cref="IPixel.ToZyxBytes(byte[], int)"/>.
        /// </summary>
        /// <param name="sourceColors">The <see cref="BufferSpan{T}"/> to the source colors.</param>
        /// <param name="destBytes">The <see cref="BufferSpan{T}"/> to the destination bytes.</param>
        /// <param name="count">The number of pixels to convert.</param>
        internal virtual void ToZyxBytes(BufferSpan<TPixel> sourceColors, BufferSpan<byte> destBytes, int count)
        {
            ref TPixel sourceRef = ref sourceColors.DangerousGetPinnableReference();
            byte[] dest = destBytes.Array;

            for (int i = 0; i < count; i++)
            {
                ref TPixel sp = ref Unsafe.Add(ref sourceRef, i);
                sp.ToZyxBytes(dest, destBytes.Start + (i * 3));
            }
        }

        /// <summary>
        /// Bulk version of <see cref="IPixel.PackFromBytes(byte, byte, byte, byte)"/> that converts data in <see cref="ComponentOrder.Zyxw"/>.
        /// </summary>
        /// <param name="sourceBytes">The <see cref="BufferSpan{T}"/> to the source bytes.</param>
        /// <param name="destColors">The <see cref="BufferSpan{T}"/> to the destination colors.</param>
        /// <param name="count">The number of pixels to convert.</param>
        internal virtual void PackFromZyxwBytes(BufferSpan<byte> sourceBytes, BufferSpan<TPixel> destColors, int count)
        {
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
        /// Bulk version of <see cref="IPixel.ToZyxwBytes(byte[], int)"/>.
        /// </summary>
        /// <param name="sourceColors">The <see cref="BufferSpan{T}"/> to the source colors.</param>
        /// <param name="destBytes">The <see cref="BufferSpan{T}"/> to the destination bytes.</param>
        /// <param name="count">The number of pixels to convert.</param>
        internal virtual void ToZyxwBytes(BufferSpan<TPixel> sourceColors, BufferSpan<byte> destBytes, int count)
        {
            ref TPixel sourceRef = ref sourceColors.DangerousGetPinnableReference();
            byte[] dest = destBytes.Array;

            for (int i = 0; i < count; i++)
            {
                ref TPixel sp = ref Unsafe.Add(ref sourceRef, i);
                sp.ToZyxwBytes(dest, destBytes.Start + (i * 4));
            }
        }
    }
}