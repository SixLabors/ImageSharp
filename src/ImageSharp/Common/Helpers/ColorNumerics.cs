// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp;

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
    private static readonly Vector4 Bt709 = new(.2126f, .7152f, .0722f, 0.0f);

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
    /// Gets the luminance from the rgb components using the formula
    /// as specified by ITU-R Recommendation BT.709.
    /// </summary>
    /// <param name="r">The red component.</param>
    /// <param name="g">The green component.</param>
    /// <param name="b">The blue component.</param>
    /// <returns>The <see cref="byte"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte Get8BitBT709Luminance(ushort r, ushort g, ushort b)
        => (byte)((From16BitTo8Bit(r) * .2126F) +
                  (From16BitTo8Bit(g) * .7152F) +
                  (From16BitTo8Bit(b) * .0722F) + 0.5F);

    /// <summary>
    /// Gets the luminance from the rgb components using the formula as
    /// specified by ITU-R Recommendation BT.709.
    /// </summary>
    /// <param name="r">The red component.</param>
    /// <param name="g">The green component.</param>
    /// <param name="b">The blue component.</param>
    /// <returns>The <see cref="ushort"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort Get16BitBT709Luminance(byte r, byte g, byte b)
        => (ushort)((From8BitTo16Bit(r) * .2126F) +
                    (From8BitTo16Bit(g) * .7152F) +
                    (From8BitTo16Bit(b) * .0722F) + 0.5F);

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
    public static byte From16BitTo8Bit(ushort component) =>

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
        (byte)(((component * 255) + 32895) >> 16);

    /// <summary>
    /// Scales a value from an 8 bit <see cref="byte"/> to
    /// an 16 bit <see cref="ushort"/> equivalent.
    /// </summary>
    /// <param name="component">The 8 bit component value.</param>
    /// <returns>The <see cref="ushort"/></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort From8BitTo16Bit(byte component)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector4 Transform(Vector4 vector, in ColorMatrix.Impl matrix)
    {
        Vector4 result = matrix.X * vector.X;

        result += matrix.Y * vector.Y;
        result += matrix.Z * vector.Z;
        result += matrix.W * vector.W;
        result += matrix.V;

        return result;
    }

    /// <summary>
    /// Transforms a vector by the given color matrix.
    /// </summary>
    /// <param name="vector">The source vector.</param>
    /// <param name="matrix">The transformation color matrix.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Transform(ref Vector4 vector, ref ColorMatrix matrix)
        => vector = Transform(vector, matrix.AsImpl());

    /// <summary>
    /// Bulk variant of <see cref="Transform(ref Vector4, ref ColorMatrix)"/>.
    /// </summary>
    /// <param name="vectors">The span of vectors</param>
    /// <param name="matrix">The transformation color matrix.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Transform(Span<Vector4> vectors, ref ColorMatrix matrix)
    {
        for (int i = 0; i < vectors.Length; i++)
        {
            ref Vector4 v = ref vectors[i];
            Transform(ref v, ref matrix);
        }
    }
}
