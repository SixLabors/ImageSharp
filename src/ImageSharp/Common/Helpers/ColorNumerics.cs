// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Provides optimized static methods for common mathematical functions specific
    /// to color processing.
    /// </summary>
    internal static class ColorNumerics
    {
        /// <summary>
        /// Vector for converting pixel to gray value as specified by
        /// ITU-R Recommendation BT.709.
        /// </summary>
        private static readonly Vector4 Bt709 = new Vector4(.2126f, .7152f, .0722f, 0.0f);

        /// <summary>
        /// Convert a pixel value to grayscale using ITU-R Recommendation BT.709.
        /// </summary>
        /// <param name="vector">The vector to get the luminance from.</param>
        /// <param name="luminanceLevels">
        /// The number of luminance levels (256 for 8 bit, 65536 for 16 bit grayscale images).
        /// </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetBT709Luminance(ref Vector4 vector, int luminanceLevels)
            => (int)MathF.Round(Vector4.Dot(vector, Bt709) * (luminanceLevels - 1));

        /// <summary>
        /// Gets the luminance from the rgb components using the formula
        /// as specified by ITU-R Recommendation BT.709.
        /// </summary>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        /// <returns>The <see cref="byte"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Get8BitBT709Luminance(byte r, byte g, byte b)
            => (byte)((r * .2126F) + (g * .7152F) + (b * .0722F) + 0.5F);

        /// <summary>
        /// Gets the luminance from the rgb components using the formula as
        /// specified by ITU-R Recommendation BT.709.
        /// </summary>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        /// <returns>The <see cref="ushort"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort Get16BitBT709Luminance(ushort r, ushort g, ushort b)
            => (ushort)((r * .2126F) + (g * .7152F) + (b * .0722F) + 0.5F);

        /// <summary>
        /// Gets the luminance from the rgb components using the formula as specified
        /// by ITU-R Recommendation BT.709.
        /// </summary>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        /// <returns>The <see cref="ushort"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort Get16BitBT709Luminance(float r, float g, float b)
            => (ushort)((r * .2126F) + (g * .7152F) + (b * .0722F) + 0.5F);

        /// <summary>
        /// Scales a value from a 16 bit <see cref="ushort"/> to an
        /// 8 bit <see cref="byte"/> equivalent.
        /// </summary>
        /// <param name="component">The 8 bit component value.</param>
        /// <returns>The <see cref="byte"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte DownScaleFrom16BitTo8Bit(ushort component)
        {
            // To scale to 8 bits From a 16-bit value V the required value (from the PNG specification) is:
            //
            //    (V * 255) / 65535
            //
            // This reduces to round(V / 257), or floor((V + 128.5)/257)
            //
            // Represent V as the two byte value vhi.vlo.  Make a guess that the
            // result is the top byte of V, vhi, then the correction to this value
            // is:
            //
            //    error = floor(((V-vhi.vhi) + 128.5) / 257)
            //          = floor(((vlo-vhi) + 128.5) / 257)
            //
            // This can be approximated using integer arithmetic (and a signed
            // shift):
            //
            //    error = (vlo-vhi+128) >> 8;
            //
            // The approximate differs from the exact answer only when (vlo-vhi) is
            // 128; it then gives a correction of +1 when the exact correction is
            // 0.  This gives 128 errors.  The exact answer (correct for all 16-bit
            // input values) is:
            //
            //    error = (vlo-vhi+128)*65535 >> 24;
            //
            // An alternative arithmetic calculation which also gives no errors is:
            //
            //    (V * 255 + 32895) >> 16
            return (byte)(((component * 255) + 32895) >> 16);
        }

        /// <summary>
        /// Scales a value from an 8 bit <see cref="byte"/> to
        /// an 16 bit <see cref="ushort"/> equivalent.
        /// </summary>
        /// <param name="component">The 8 bit component value.</param>
        /// <returns>The <see cref="ushort"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort UpscaleFrom8BitTo16Bit(byte component)
            => (ushort)(component * 257);

        /// <summary>
        /// Returns how many bits are required to store the specified number of colors.
        /// Performs a Log2() on the value.
        /// </summary>
        /// <param name="colors">The number of colors.</param>
        /// <returns>
        /// The <see cref="int"/>
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetBitsNeededForColorDepth(int colors)
            => Math.Max(1, (int)Math.Ceiling(Math.Log(colors, 2)));

        /// <summary>
        /// Returns how many colors will be created by the specified number of bits.
        /// </summary>
        /// <param name="bitDepth">The bit depth.</param>
        /// <returns>The <see cref="int"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetColorCountForBitDepth(int bitDepth)
            => 1 << bitDepth;

        /// <summary>
        /// Transforms a vector by the given color matrix.
        /// </summary>
        /// <param name="vector">The source vector.</param>
        /// <param name="matrix">The transformation color matrix.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Transform(ref Vector4 vector, ref ColorMatrix matrix)
        {
            float x = vector.X;
            float y = vector.Y;
            float z = vector.Z;
            float w = vector.W;

            vector.X = (x * matrix.M11) + (y * matrix.M21) + (z * matrix.M31) + (w * matrix.M41) + matrix.M51;
            vector.Y = (x * matrix.M12) + (y * matrix.M22) + (z * matrix.M32) + (w * matrix.M42) + matrix.M52;
            vector.Z = (x * matrix.M13) + (y * matrix.M23) + (z * matrix.M33) + (w * matrix.M43) + matrix.M53;
            vector.W = (x * matrix.M14) + (y * matrix.M24) + (z * matrix.M34) + (w * matrix.M44) + matrix.M54;
        }

        /// <summary>
        /// Bulk variant of <see cref="Transform(ref Vector4, ref ColorMatrix)"/>.
        /// </summary>
        /// <param name="vectors">The span of vectors</param>
        /// <param name="matrix">The transformation color matrix.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Transform(Span<Vector4> vectors, ref ColorMatrix matrix)
        {
            ref Vector4 baseRef = ref MemoryMarshal.GetReference(vectors);

            for (int i = 0; i < vectors.Length; i++)
            {
                ref Vector4 v = ref Unsafe.Add(ref baseRef, i);
                Transform(ref v, ref matrix);
            }
        }
    }
}
