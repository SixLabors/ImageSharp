// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <summary>
    /// Helper methods for packing and unpacking floating point values
    /// </summary>
    internal class HalfTypeHelper
    {
        /// <summary>
        /// Packs a <see cref="float"/> into an <see cref="ushort"/>
        /// </summary>
        /// <param name="value">The float to pack</param>
        /// <returns>The <see cref="ushort"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ushort Pack(float value)
        {
            var uif = new Uif { F = value };
            return Pack(uif.I);
        }

        /// <summary>
        /// Packs an <see cref="int"/> into a <see cref="ushort"/>
        /// </summary>
        /// <param name="value">The integer to pack.</param>
        /// <returns>The <see cref="ushort"/></returns>
        internal static ushort Pack(int value)
        {
            int s = (value >> 16) & 0x00008000;
            int e = ((value >> 23) & 0x000000ff) - (127 - 15);
            int m = value & 0x007fffff;

            if (e <= 0)
            {
                if (e < -10)
                {
                    return (ushort)s;
                }

                m = m | 0x00800000;

                int t = 14 - e;
                int a = (1 << (t - 1)) - 1;
                int b = (m >> t) & 1;

                m = (m + a + b) >> t;

                return (ushort)(s | m);
            }

            if (e == 0xff - (127 - 15))
            {
                if (m == 0)
                {
                    return (ushort)(s | 0x7c00);
                }

                m >>= 13;
                return (ushort)(s | 0x7c00 | m | ((m == 0) ? 1 : 0));
            }

            m = m + 0x00000fff + ((m >> 13) & 1);

            if ((m & 0x00800000) != 0)
            {
                m = 0;
                e += 1;
            }

            if (e > 30)
            {
                return (ushort)(s | 0x7c00);
            }

            return (ushort)(s | (e << 10) | (m >> 13));
        }

        /// <summary>
        /// Unpacks a <see cref="ushort"/> into a <see cref="float"/>.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The <see cref="float"/>.</returns>
        internal static float Unpack(ushort value)
        {
            uint result;
            uint mantissa = (uint)(value & 1023);
            uint exponent = 0xfffffff2;

            if ((value & -33792) == 0)
            {
                if (mantissa != 0)
                {
                    while ((mantissa & 1024) == 0)
                    {
                        exponent--;
                        mantissa = mantissa << 1;
                    }

                    mantissa &= 0xfffffbff;
                    result = ((uint)((((uint)value & 0x8000) << 16) | ((exponent + 127) << 23))) | (mantissa << 13);
                }
                else
                {
                    result = (uint)((value & 0x8000) << 16);
                }
            }
            else
            {
                result = ((((uint)value & 0x8000) << 16) | ((((((uint)value >> 10) & 0x1f) - 15) + 127) << 23)) | (mantissa << 13);
            }

            var uif = new Uif { U = result };
            return uif.F;
        }

        /// <summary>
        /// Maps the position of number types in memory
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        private struct Uif
        {
            /// <summary>
            /// The float.
            /// </summary>
            [FieldOffset(0)]
            public float F;

            /// <summary>
            /// The integer.
            /// </summary>
            [FieldOffset(0)]
            public int I;

            /// <summary>
            /// The unsigned integer.
            /// </summary>
            [FieldOffset(0)]
            public uint U;
        }
    }
}