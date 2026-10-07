// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Webp.ColorConverters;

internal abstract partial class WebpColorConverterBase
{
    /// <summary>
    /// Converts native lossy YCbCr samples to BGR using the WebP fixed-point matrix.
    /// </summary>
    internal readonly struct YCbCrOperator : IWebpColorConverterOperator
    {
        /// <summary>
        /// The fractional bits in the WebP YCbCr-to-RGB matrix coefficients.
        /// </summary>
        private const int DecodePrecision = 14;

        /// <summary>
        /// The fractional product bits removed before combining YCbCr-to-RGB terms.
        /// </summary>
        private const int DecodeProductShift = 8;

        /// <summary>
        /// The fractional bits remaining when the YCbCr-to-RGB terms are combined.
        /// </summary>
        private const int DecodeResultShift = DecodePrecision - DecodeProductShift;

        /// <summary>
        /// The Y contribution to each RGB channel, scaled by 2^14.
        /// </summary>
        private const int YMult = 19077;

        /// <summary>
        /// The U contribution to blue, scaled by 2^14.
        /// </summary>
        private const int BCbMult = 33050;

        /// <summary>
        /// The magnitude of the U contribution subtracted from green, scaled by 2^14.
        /// </summary>
        private const int GCbMult = 6419;

        /// <summary>
        /// The magnitude of the V contribution subtracted from green, scaled by 2^14.
        /// </summary>
        private const int GCrMult = 13320;

        /// <summary>
        /// The V contribution to red, scaled by 2^14.
        /// </summary>
        private const int RCrMult = 26149;

        /// <summary>
        /// The blue offset subtracted after the product shift; includes Y/U offsets and rounding at six fractional bits.
        /// </summary>
        private const int BOffset = 17685;

        /// <summary>
        /// The green offset added after the product shift; includes Y/U/V offsets and rounding at six fractional bits.
        /// </summary>
        private const int GOffset = 8708;

        /// <summary>
        /// The red offset subtracted after the product shift; includes Y/V offsets and rounding at six fractional bits.
        /// </summary>
        private const int ROffset = 14234;

        /// <summary>
        /// The fractional bits in the WebP RGB-to-YCbCr matrix coefficients.
        /// </summary>
        private const int EncodePrecision = 16;

        /// <summary>
        /// The red contribution to Y, scaled by 2^16.
        /// </summary>
        private const int YRMult = 16839;

        /// <summary>
        /// The green contribution to Y, scaled by 2^16.
        /// </summary>
        private const int YGMult = 33059;

        /// <summary>
        /// The blue contribution to Y, scaled by 2^16.
        /// </summary>
        private const int YBMult = 6420;

        /// <summary>
        /// The magnitude of the red contribution subtracted from U, scaled by 2^16.
        /// </summary>
        private const int URMult = 9719;

        /// <summary>
        /// The magnitude of the green contribution subtracted from U, scaled by 2^16.
        /// </summary>
        private const int UGMult = 19081;

        /// <summary>
        /// The blue contribution to U, scaled by 2^16.
        /// </summary>
        private const int UBMult = 28800;

        /// <summary>
        /// The red contribution to V, scaled by 2^16.
        /// </summary>
        private const int VRMult = 28800;

        /// <summary>
        /// The magnitude of the green contribution subtracted from V, scaled by 2^16.
        /// </summary>
        private const int VGMult = 24116;

        /// <summary>
        /// The magnitude of the blue contribution subtracted from V, scaled by 2^16.
        /// </summary>
        private const int VBMult = 4684;

        /// <summary>
        /// The encoded luma code for black.
        /// </summary>
        private const int YBlack = 16;

        /// <summary>
        /// The encoded U and V code for neutral chroma.
        /// </summary>
        private const int ChromaCenter = 128;

        /// <inheritdoc />
        public static void ConvertToSamples(ref int c0, ref int c1, ref int c2)
        {
            // WebP's YCbCr matrix uses coefficients scaled by 2^14. The first shift removes eight fractional bits;
            // the final shift removes six. Offsets include Y=16, U=V=128 and rounding, producing B, G, R in c0, c1, c2.
            int y = c0;
            int u = c1;
            int v = c2;
            int luma = (y * YMult) >> DecodeProductShift;

            // Each product is truncated before the channel terms are combined, as in WebP's scalar decoder.
            c0 = Clip((luma + ((u * BCbMult) >> DecodeProductShift) - BOffset) >> DecodeResultShift);
            c1 = Clip((luma - ((u * GCbMult) >> DecodeProductShift) - ((v * GCrMult) >> DecodeProductShift) + GOffset) >> DecodeResultShift);
            c2 = Clip((luma + ((v * RCrMult) >> DecodeProductShift) - ROffset) >> DecodeResultShift);
        }

        /// <inheritdoc />
        public static void ConvertToSamples(ref Vector128<int> c0, ref Vector128<int> c1, ref Vector128<int> c2)
        {
            // Each int lane is one pixel. Keep six fractional bits after the first shift, then apply the BGR offsets.
            // Arithmetic shifts preserve negative intermediate values so clipping maps them to zero.
            Vector128<int> luma = Vector128.ShiftRightArithmetic(c0 * Vector128.Create(YMult), DecodeProductShift);

            Vector128<int> uBlue = Vector128.ShiftRightArithmetic(c1 * Vector128.Create(BCbMult), DecodeProductShift);
            Vector128<int> uGreen = Vector128.ShiftRightArithmetic(c1 * Vector128.Create(GCbMult), DecodeProductShift);
            Vector128<int> vGreen = Vector128.ShiftRightArithmetic(c2 * Vector128.Create(GCrMult), DecodeProductShift);
            Vector128<int> vRed = Vector128.ShiftRightArithmetic(c2 * Vector128.Create(RCrMult), DecodeProductShift);

            c0 = Clip(Vector128.ShiftRightArithmetic(luma + uBlue - Vector128.Create(BOffset), DecodeResultShift));
            c1 = Clip(Vector128.ShiftRightArithmetic(luma - uGreen - vGreen + Vector128.Create(GOffset), DecodeResultShift));
            c2 = Clip(Vector128.ShiftRightArithmetic(luma + vRed - Vector128.Create(ROffset), DecodeResultShift));
        }

        /// <inheritdoc />
        public static void ConvertToSamples(ref Vector256<int> c0, ref Vector256<int> c1, ref Vector256<int> c2)
        {
            // Each int lane is one pixel. Keep six fractional bits after the first shift, then apply the BGR offsets.
            // Arithmetic shifts preserve negative intermediate values so clipping maps them to zero.
            Vector256<int> luma = Vector256.ShiftRightArithmetic(c0 * Vector256.Create(YMult), DecodeProductShift);

            Vector256<int> uBlue = Vector256.ShiftRightArithmetic(c1 * Vector256.Create(BCbMult), DecodeProductShift);
            Vector256<int> uGreen = Vector256.ShiftRightArithmetic(c1 * Vector256.Create(GCbMult), DecodeProductShift);
            Vector256<int> vGreen = Vector256.ShiftRightArithmetic(c2 * Vector256.Create(GCrMult), DecodeProductShift);
            Vector256<int> vRed = Vector256.ShiftRightArithmetic(c2 * Vector256.Create(RCrMult), DecodeProductShift);

            c0 = Clip(Vector256.ShiftRightArithmetic(luma + uBlue - Vector256.Create(BOffset), DecodeResultShift));
            c1 = Clip(Vector256.ShiftRightArithmetic(luma - uGreen - vGreen + Vector256.Create(GOffset), DecodeResultShift));
            c2 = Clip(Vector256.ShiftRightArithmetic(luma + vRed - Vector256.Create(ROffset), DecodeResultShift));
        }

        /// <inheritdoc />
        public static void ConvertToSamples(ref Vector512<int> c0, ref Vector512<int> c1, ref Vector512<int> c2)
        {
            // Each int lane is one pixel. Keep six fractional bits after the first shift, then apply the BGR offsets.
            // Arithmetic shifts preserve negative intermediate values so clipping maps them to zero.
            Vector512<int> luma = Vector512.ShiftRightArithmetic(c0 * Vector512.Create(YMult), DecodeProductShift);

            Vector512<int> uBlue = Vector512.ShiftRightArithmetic(c1 * Vector512.Create(BCbMult), DecodeProductShift);
            Vector512<int> uGreen = Vector512.ShiftRightArithmetic(c1 * Vector512.Create(GCbMult), DecodeProductShift);
            Vector512<int> vGreen = Vector512.ShiftRightArithmetic(c2 * Vector512.Create(GCrMult), DecodeProductShift);
            Vector512<int> vRed = Vector512.ShiftRightArithmetic(c2 * Vector512.Create(RCrMult), DecodeProductShift);

            c0 = Clip(Vector512.ShiftRightArithmetic(luma + uBlue - Vector512.Create(BOffset), DecodeResultShift));
            c1 = Clip(Vector512.ShiftRightArithmetic(luma - uGreen - vGreen + Vector512.Create(GOffset), DecodeResultShift));
            c2 = Clip(Vector512.ShiftRightArithmetic(luma + vRed - Vector512.Create(ROffset), DecodeResultShift));
        }

        /// <summary>
        /// Clamps a signed channel code to the byte range.
        /// </summary>
        /// <param name="value">The signed channel code with no remaining fractional bits.</param>
        /// <returns>Codes below zero become zero; codes above 255 become 255. Other codes remain unchanged.</returns>
        private static int Clip(int value)
        {
            // Clamp before narrowing to byte, which would otherwise wrap out-of-range matrix results.
            return Math.Clamp(value, 0, byte.MaxValue);
        }

        /// <summary>
        /// Clamps signed channel codes to the byte range.
        /// </summary>
        /// <param name="value">Signed channel codes with no remaining fractional bits.</param>
        /// <returns>Codes below zero become zero; codes above 255 become 255. Other codes remain unchanged.</returns>
        private static Vector128<int> Clip(Vector128<int> value)
        {
            // The matrix can produce codes outside the byte range. Clamp before narrowing, which would otherwise wrap those codes.
            return Vector128.Clamp(value, Vector128<int>.Zero, Vector128.Create((int)byte.MaxValue));
        }

        /// <summary>
        /// Clamps signed channel codes to the byte range.
        /// </summary>
        /// <param name="value">Signed channel codes with no remaining fractional bits.</param>
        /// <returns>Codes below zero become zero; codes above 255 become 255. Other codes remain unchanged.</returns>
        private static Vector256<int> Clip(Vector256<int> value)
        {
            // The matrix can produce codes outside the byte range. Clamp before narrowing, which would otherwise wrap those codes.
            return Vector256.Clamp(value, Vector256<int>.Zero, Vector256.Create((int)byte.MaxValue));
        }

        /// <summary>
        /// Clamps signed channel codes to the byte range.
        /// </summary>
        /// <param name="value">Signed channel codes with no remaining fractional bits.</param>
        /// <returns>Codes below zero become zero; codes above 255 become 255. Other codes remain unchanged.</returns>
        private static Vector512<int> Clip(Vector512<int> value)
        {
            // The matrix can produce codes outside the byte range. Clamp before narrowing, which would otherwise wrap those codes.
            return Vector512.Clamp(value, Vector512<int>.Zero, Vector512.Create((int)byte.MaxValue));
        }

        /// <inheritdoc />
        public static void ConvertSamplesWithIcc(
            Configuration configuration, ReadOnlySpan<byte> source, ColorProfileConverter converter, Span<Vector4> vectors, Span<Rgb> rgb)
        {
            // Three-byte decoder output stores B, G, R. The pixel API exposes straight RGB vectors to the ICC transform.
            ReadOnlySpan<Bgr24> bgr = MemoryMarshal.Cast<byte, Bgr24>(source);
            PixelOperations<Bgr24>.Instance.ToUnassociatedScaledVector4(configuration, bgr, vectors);
            converter.Convert(vectors, rgb);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(int r, int g, int b, int inputShift, out int c0, out int c1, out int c2)
        {
            // WebP's coefficients have sixteen fractional bits. Downsampled chroma inputs retain two more bits;
            // include those in the final shift instead of rounding the resampled RGB values first.
            // The offsets put black at Y=16 and neutral chroma at U=V=128. Half a unit rounds ties upward;
            // chroma is clipped to the byte range after the signed shift.
            int shift = EncodePrecision + inputShift;
            int rounding = 1 << (shift - 1);

            c0 = ((YRMult * r) + (YGMult * g) + (YBMult * b) + rounding + (YBlack << shift)) >> shift;
            c1 = Math.Clamp(((-URMult * r) - (UGMult * g) + (UBMult * b) + rounding + (ChromaCenter << shift)) >> shift, 0, byte.MaxValue);
            c2 = Math.Clamp(((VRMult * r) - (VGMult * g) - (VBMult * b) + rounding + (ChromaCenter << shift)) >> shift, 0, byte.MaxValue);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(
            Vector128<int> r, Vector128<int> g, Vector128<int> b, int inputShift, out Vector128<int> c0, out Vector128<int> c1, out Vector128<int> c2)
        {
            // Each int lane holds one sample. Coefficients have sixteen fractional bits; resampled RGB retains two more.
            // Y=16 is black and U=V=128 is neutral chroma. Add half a unit before the signed shift to round ties upward.
            // Chroma clipping follows that shift; the luma coefficients already produce byte-range values.
            int shift = EncodePrecision + inputShift;
            Vector128<int> rounding = Vector128.Create(1 << (shift - 1));
            Vector128<int> yOffset = Vector128.Create(YBlack << shift);
            Vector128<int> uvOffset = Vector128.Create(ChromaCenter << shift);

            c0 = ((r * Vector128.Create(YRMult)) + (g * Vector128.Create(YGMult)) + (b * Vector128.Create(YBMult)) + rounding + yOffset) >> shift;
            c1 = Clip((-(r * Vector128.Create(URMult)) - (g * Vector128.Create(UGMult)) + (b * Vector128.Create(UBMult)) + rounding + uvOffset) >> shift);
            c2 = Clip(((r * Vector128.Create(VRMult)) - (g * Vector128.Create(VGMult)) - (b * Vector128.Create(VBMult)) + rounding + uvOffset) >> shift);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(
            Vector256<int> r, Vector256<int> g, Vector256<int> b, int inputShift, out Vector256<int> c0, out Vector256<int> c1, out Vector256<int> c2)
        {
            // Each int lane holds one sample. Coefficients have sixteen fractional bits; resampled RGB retains two more.
            // Y=16 is black and U=V=128 is neutral chroma. Add half a unit before the signed shift to round ties upward.
            // Chroma clipping follows that shift; the luma coefficients already produce byte-range values.
            int shift = EncodePrecision + inputShift;
            Vector256<int> rounding = Vector256.Create(1 << (shift - 1));
            Vector256<int> yOffset = Vector256.Create(YBlack << shift);
            Vector256<int> uvOffset = Vector256.Create(ChromaCenter << shift);

            c0 = ((r * Vector256.Create(YRMult)) + (g * Vector256.Create(YGMult)) + (b * Vector256.Create(YBMult)) + rounding + yOffset) >> shift;
            c1 = Clip((-(r * Vector256.Create(URMult)) - (g * Vector256.Create(UGMult)) + (b * Vector256.Create(UBMult)) + rounding + uvOffset) >> shift);
            c2 = Clip(((r * Vector256.Create(VRMult)) - (g * Vector256.Create(VGMult)) - (b * Vector256.Create(VBMult)) + rounding + uvOffset) >> shift);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConvertFromRgb(
            Vector512<int> r, Vector512<int> g, Vector512<int> b, int inputShift, out Vector512<int> c0, out Vector512<int> c1, out Vector512<int> c2)
        {
            // Each int lane holds one sample. Coefficients have sixteen fractional bits; resampled RGB retains two more.
            // Y=16 is black and U=V=128 is neutral chroma. Add half a unit before the signed shift to round ties upward.
            // Chroma clipping follows that shift; the luma coefficients already produce byte-range values.
            int shift = EncodePrecision + inputShift;
            Vector512<int> rounding = Vector512.Create(1 << (shift - 1));
            Vector512<int> yOffset = Vector512.Create(YBlack << shift);
            Vector512<int> uvOffset = Vector512.Create(ChromaCenter << shift);

            c0 = ((r * Vector512.Create(YRMult)) + (g * Vector512.Create(YGMult)) + (b * Vector512.Create(YBMult)) + rounding + yOffset) >> shift;
            c1 = Clip((-(r * Vector512.Create(URMult)) - (g * Vector512.Create(UGMult)) + (b * Vector512.Create(UBMult)) + rounding + uvOffset) >> shift);
            c2 = Clip(((r * Vector512.Create(VRMult)) - (g * Vector512.Create(VGMult)) - (b * Vector512.Create(VBMult)) + rounding + uvOffset) >> shift);
        }
    }
}
