// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Formats.Webp.ColorConverters;

internal abstract partial class WebpColorConverterBase
{
    /// <summary>
    /// Defines scalar and SIMD conversion between RGB and native WebP samples.
    /// </summary>
    internal interface IWebpColorConverterOperator
    {
        /// <summary>
        /// Converts one native sample to three destination byte codes.
        /// </summary>
        /// <param name="c0">The first native source channel.</param>
        /// <param name="c1">The second native source channel.</param>
        /// <param name="c2">The third native source channel.</param>
        public static abstract void ConvertToSamples(ref int c0, ref int c1, ref int c2);

        /// <summary>
        /// Converts four native samples using 128-bit integer lanes.
        /// </summary>
        /// <param name="c0">The first native source channel in four lanes.</param>
        /// <param name="c1">The second native source channel in four lanes.</param>
        /// <param name="c2">The third native source channel in four lanes.</param>
        public static abstract void ConvertToSamples(ref Vector128<int> c0, ref Vector128<int> c1, ref Vector128<int> c2);

        /// <summary>
        /// Converts eight native samples using 256-bit integer lanes.
        /// </summary>
        /// <param name="c0">The first native source channel in eight lanes.</param>
        /// <param name="c1">The second native source channel in eight lanes.</param>
        /// <param name="c2">The third native source channel in eight lanes.</param>
        public static abstract void ConvertToSamples(ref Vector256<int> c0, ref Vector256<int> c1, ref Vector256<int> c2);

        /// <summary>
        /// Converts sixteen native samples using 512-bit integer lanes.
        /// </summary>
        /// <param name="c0">The first native source channel in sixteen lanes.</param>
        /// <param name="c1">The second native source channel in sixteen lanes.</param>
        /// <param name="c2">The third native source channel in sixteen lanes.</param>
        public static abstract void ConvertToSamples(ref Vector512<int> c0, ref Vector512<int> c1, ref Vector512<int> c2);

        /// <summary>
        /// Converts one row of native source bytes through its ICC profile.
        /// </summary>
        /// <param name="configuration">The decoding configuration.</param>
        /// <param name="source">The native three-byte samples.</param>
        /// <param name="converter">The ICC converter.</param>
        /// <param name="vectors">Vector scratch with one entry per pixel.</param>
        /// <param name="rgb">RGB scratch with one entry per pixel.</param>
        public static abstract void ConvertSamplesWithIcc(
            Configuration configuration, ReadOnlySpan<byte> source, ColorProfileConverter converter, Span<Vector4> vectors, Span<Rgb> rgb);

        /// <summary>
        /// Converts one RGB sample to the selected native components.
        /// </summary>
        /// <param name="r">The red codes, including the declared fractional bits.</param>
        /// <param name="g">The green codes, including the declared fractional bits.</param>
        /// <param name="b">The blue codes, including the declared fractional bits.</param>
        /// <param name="inputShift">The fractional input bits: zero for pixel samples or two for resampled chroma inputs.</param>
        /// <param name="c0">The first native byte-range component.</param>
        /// <param name="c1">The second native byte-range component.</param>
        /// <param name="c2">The third native byte-range component.</param>
        public static abstract void ConvertFromRgb(int r, int g, int b, int inputShift, out int c0, out int c1, out int c2);

        /// <summary>
        /// Converts four RGB samples to the selected native components.
        /// </summary>
        /// <param name="r">The red codes, including the declared fractional bits.</param>
        /// <param name="g">The green codes, including the declared fractional bits.</param>
        /// <param name="b">The blue codes, including the declared fractional bits.</param>
        /// <param name="inputShift">The fractional input bits: zero for pixel samples or two for resampled chroma inputs.</param>
        /// <param name="c0">The first native byte-range component.</param>
        /// <param name="c1">The second native byte-range component.</param>
        /// <param name="c2">The third native byte-range component.</param>
        public static abstract void ConvertFromRgb(
            Vector128<int> r, Vector128<int> g, Vector128<int> b, int inputShift, out Vector128<int> c0, out Vector128<int> c1, out Vector128<int> c2);

        /// <summary>
        /// Converts eight RGB samples to the selected native components.
        /// </summary>
        /// <param name="r">The red codes, including the declared fractional bits.</param>
        /// <param name="g">The green codes, including the declared fractional bits.</param>
        /// <param name="b">The blue codes, including the declared fractional bits.</param>
        /// <param name="inputShift">The fractional input bits: zero for pixel samples or two for resampled chroma inputs.</param>
        /// <param name="c0">The first native byte-range component.</param>
        /// <param name="c1">The second native byte-range component.</param>
        /// <param name="c2">The third native byte-range component.</param>
        public static abstract void ConvertFromRgb(
            Vector256<int> r, Vector256<int> g, Vector256<int> b, int inputShift, out Vector256<int> c0, out Vector256<int> c1, out Vector256<int> c2);

        /// <summary>
        /// Converts sixteen RGB samples to the selected native components.
        /// </summary>
        /// <param name="r">The red codes, including the declared fractional bits.</param>
        /// <param name="g">The green codes, including the declared fractional bits.</param>
        /// <param name="b">The blue codes, including the declared fractional bits.</param>
        /// <param name="inputShift">The fractional input bits: zero for pixel samples or two for resampled chroma inputs.</param>
        /// <param name="c0">The first native byte-range component.</param>
        /// <param name="c1">The second native byte-range component.</param>
        /// <param name="c2">The third native byte-range component.</param>
        public static abstract void ConvertFromRgb(
            Vector512<int> r, Vector512<int> g, Vector512<int> b, int inputShift, out Vector512<int> c0, out Vector512<int> c1, out Vector512<int> c2);
    }
}
