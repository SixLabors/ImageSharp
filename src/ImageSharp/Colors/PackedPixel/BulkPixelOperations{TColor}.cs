// <copyright file="BulkPixelOperations{TColor}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System.Numerics;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// A stateless class implementing Strategy Pattern for batched pixel-data conversion operations
    /// for pixel buffers of type <typeparamref name="TColor"/>.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    public unsafe class BulkPixelOperations<TColor>
        where TColor : struct, IPixel<TColor>
    {
        /// <summary>
        /// The size of <typeparamref name="TColor"/> in bytes
        /// </summary>
        private static readonly int ColorSize = Unsafe.SizeOf<TColor>();

        /// <summary>
        /// Gets the global <see cref="BulkPixelOperations{TColor}"/> instance for the pixel type <typeparamref name="TColor"/>
        /// </summary>
        public static BulkPixelOperations<TColor> Instance { get; } = default(TColor).BulkOperations;

        /// <summary>
        /// Bulk version of <see cref="IPixel.PackFromVector4(Vector4)"/>
        /// </summary>
        /// <param name="sourceVectors">The <see cref="ArrayPointer{Vector4}"/> to the source vectors.</param>
        /// <param name="destColors">The <see cref="ArrayPointer{TColor}"/> to the destination colors.</param>
        /// <param name="count">The number of pixels to convert.</param>
        internal virtual void PackFromVector4(
            ArrayPointer<Vector4> sourceVectors,
            ArrayPointer<TColor> destColors,
            int count)
        {
            Vector4* sp = (Vector4*)sourceVectors.PointerAtOffset;
            byte* dp = (byte*)destColors;

            for (int i = 0; i < count; i++)
            {
                Vector4 v = Unsafe.Read<Vector4>(sp);
                TColor c = default(TColor);
                c.PackFromVector4(v);
                Unsafe.Write(dp, c);

                sp++;
                dp += ColorSize;
            }
        }

        /// <summary>
        /// Bulk version of <see cref="IPixel.ToVector4()"/>.
        /// </summary>
        /// <param name="sourceColors">The <see cref="ArrayPointer{TColor}"/> to the source colors.</param>
        /// <param name="destVectors">The <see cref="ArrayPointer{Vector4}"/> to the destination vectors.</param>
        /// <param name="count">The number of pixels to convert.</param>
        internal virtual void PackToVector4(
            ArrayPointer<TColor> sourceColors,
            ArrayPointer<Vector4> destVectors,
            int count)
        {
            byte* sp = (byte*)sourceColors;
            Vector4* dp = (Vector4*)destVectors.PointerAtOffset;

            for (int i = 0; i < count; i++)
            {
                TColor c = Unsafe.Read<TColor>(sp);
                *dp = c.ToVector4();
                sp += ColorSize;
                dp++;
            }
        }

        /// <summary>
        /// Bulk version of <see cref="IPixel.PackFromBytes(byte, byte, byte, byte)"/> that converts data in <see cref="ComponentOrder.Xyz"/>.
        /// </summary>
        /// <param name="sourceBytes"></param>
        /// <param name="destColors"></param>
        /// <param name="count"></param>
        internal virtual void PackFromXyzBytes(
            ArrayPointer<byte> sourceBytes,
            ArrayPointer<TColor> destColors,
            int count)
        {
            byte* sp = (byte*)sourceBytes;
            byte* dp = (byte*)destColors.PointerAtOffset;

            for (int i = 0; i < count; i++)
            {
                TColor c = default(TColor);
                c.PackFromBytes(sp[0], sp[1], sp[2], 255);
                Unsafe.Write(dp, c);
                sp += 3;
                dp += ColorSize;
            }
        }

        /// <summary>
        /// Bulk version of <see cref="IPixel.ToXyzBytes(byte[], int)"/>.
        /// </summary>
        /// <param name="sourceColors"></param>
        /// <param name="destBytes"></param>
        /// <param name="count"></param>
        internal virtual void PackToXyzBytes(ArrayPointer<TColor> sourceColors, ArrayPointer<byte> destBytes, int count)
        {
            byte* sp = (byte*)sourceColors;
            byte[] dest = destBytes.Array;

            for (int i = destBytes.Offset; i < destBytes.Offset + count * 3; i += 3)
            {
                TColor c = Unsafe.Read<TColor>(sp);
                c.ToXyzBytes(dest, i);
                sp += ColorSize;
            }
        }

        /// <summary>
        /// Bulk version of <see cref="IPixel.PackFromBytes(byte, byte, byte, byte)"/> that converts data in <see cref="ComponentOrder.Xyzw"/>.
        /// </summary>
        /// <param name="sourceBytes"></param>
        /// <param name="destColors"></param>
        /// <param name="count"></param>
        internal virtual void PackFromXyzwBytes(
            ArrayPointer<byte> sourceBytes,
            ArrayPointer<TColor> destColors,
            int count)
        {
            byte* sp = (byte*)sourceBytes;
            byte* dp = (byte*)destColors.PointerAtOffset;

            for (int i = 0; i < count; i++)
            {
                TColor c = default(TColor);
                c.PackFromBytes(sp[0], sp[1], sp[2], sp[3]);
                Unsafe.Write(dp, c);
                sp += 4;
                dp += ColorSize;
            }
        }

        /// <summary>
        /// Bulk version of <see cref="IPixel.ToXyzwBytes(byte[], int)"/>.
        /// </summary>
        /// <param name="sourceColors"></param>
        /// <param name="destBytes"></param>
        /// <param name="count"></param>
        internal virtual void PackToXyzwBytes(
            ArrayPointer<TColor> sourceColors,
            ArrayPointer<byte> destBytes,
            int count)
        {
            byte* sp = (byte*)sourceColors;
            byte[] dest = destBytes.Array;

            for (int i = destBytes.Offset; i < destBytes.Offset + count * 4; i += 4)
            {
                TColor c = Unsafe.Read<TColor>(sp);
                c.ToXyzwBytes(dest, i);
                sp += ColorSize;
            }
        }

        /// <summary>
        /// Bulk version of <see cref="IPixel.PackFromBytes(byte, byte, byte, byte)"/> that converts data in <see cref="ComponentOrder.Zyx"/>.
        /// </summary>
        /// <param name="sourceBytes"></param>
        /// <param name="destColors"></param>
        /// <param name="count"></param>
        internal virtual void PackFromZyxBytes(
            ArrayPointer<byte> sourceBytes,
            ArrayPointer<TColor> destColors,
            int count)
        {
            byte* sp = (byte*)sourceBytes;
            byte* dp = (byte*)destColors.PointerAtOffset;

            for (int i = 0; i < count; i++)
            {
                TColor c = default(TColor);
                c.PackFromBytes(sp[2], sp[1], sp[0], 255);
                Unsafe.Write(dp, c);
                sp += 3;
                dp += ColorSize;
            }
        }

        /// <summary>
        /// Bulk version of <see cref="IPixel.ToZyxBytes(byte[], int)"/>.
        /// </summary>
        /// <param name="sourceColors"></param>
        /// <param name="destBytes"></param>
        /// <param name="count"></param>
        internal virtual void PackToZyxBytes(ArrayPointer<TColor> sourceColors, ArrayPointer<byte> destBytes, int count)
        {
            byte* sp = (byte*)sourceColors;
            byte[] dest = destBytes.Array;

            for (int i = destBytes.Offset; i < destBytes.Offset + count * 3; i += 3)
            {
                TColor c = Unsafe.Read<TColor>(sp);
                c.ToZyxBytes(dest, i);
                sp += ColorSize;
            }
        }

        /// <summary>
        /// Bulk version of <see cref="IPixel.PackFromBytes(byte, byte, byte, byte)"/> that converts data in <see cref="ComponentOrder.Zyxw"/>.
        /// </summary>
        /// <param name="sourceBytes"></param>
        /// <param name="destColors"></param>
        /// <param name="count"></param>
        internal virtual void PackFromZyxwBytes(
            ArrayPointer<byte> sourceBytes,
            ArrayPointer<TColor> destColors,
            int count)
        {
            byte* sp = (byte*)sourceBytes;
            byte* dp = (byte*)destColors.PointerAtOffset;

            for (int i = 0; i < count; i++)
            {
                TColor c = default(TColor);
                c.PackFromBytes(sp[2], sp[1], sp[0], sp[3]);
                Unsafe.Write(dp, c);
                sp += 4;
                dp += ColorSize;
            }
        }

        /// <summary>
        /// Bulk version of <see cref="IPixel.ToZyxwBytes(byte[], int)"/>.
        /// </summary>
        /// <param name="sourceColors"></param>
        /// <param name="destBytes"></param>
        /// <param name="count"></param>
        internal virtual void PackToZyxwBytes(
            ArrayPointer<TColor> sourceColors,
            ArrayPointer<byte> destBytes,
            int count)
        {
            byte* sp = (byte*)sourceColors;
            byte[] dest = destBytes.Array;

            for (int i = destBytes.Offset; i < destBytes.Offset + count * 4; i += 4)
            {
                TColor c = Unsafe.Read<TColor>(sp);
                c.ToZyxwBytes(dest, i);
                sp += ColorSize;
            }
        }
    }
}