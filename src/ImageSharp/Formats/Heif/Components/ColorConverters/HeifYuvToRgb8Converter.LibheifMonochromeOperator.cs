// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Components;

/// <content>
/// Provides pinned-libheif monochrome presentation. The luma code value is reduced directly to eight bits and copied
/// to all RGB components; signaled luma-range expansion is intentionally absent because libheif's direct monochrome
/// operation does not apply it.
/// </content>
internal static partial class HeifYuvToRgb8Converter
{
    /// <summary>
    /// Implements pinned-libheif monochrome conversion for scalar and SIMD lanes.
    /// </summary>
    private readonly struct LibheifMonochromeOperator : IHeifYuvToRgb8Operator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Convert(
            Vector512<int> y,
            Vector512<int> cb,
            Vector512<int> cr,
            in ConversionParameters parameters,
            out Vector512<int> r,
            out Vector512<int> g,
            out Vector512<int> b)
        {
            Vector512<int> value = y >> parameters.LibheifSixteenLane.OutputShift;
            r = value;
            g = value;
            b = value;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Convert(
            Vector256<int> y,
            Vector256<int> cb,
            Vector256<int> cr,
            in ConversionParameters parameters,
            out Vector256<int> r,
            out Vector256<int> g,
            out Vector256<int> b)
        {
            Vector256<int> value = y >> parameters.LibheifEightLane.OutputShift;
            r = value;
            g = value;
            b = value;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Convert(
            Vector128<int> y,
            Vector128<int> cb,
            Vector128<int> cr,
            in ConversionParameters parameters,
            out Vector128<int> r,
            out Vector128<int> g,
            out Vector128<int> b)
        {
            Vector128<int> value = y >> parameters.LibheifFourLane.OutputShift;
            r = value;
            g = value;
            b = value;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Convert(ushort y, ushort cb, ushort cr, in ConversionParameters parameters, out byte r, out byte g, out byte b)
        {
            byte value = (byte)(y >> parameters.LibheifScalar.OutputShift);
            r = value;
            g = value;
            b = value;
        }
    }
}
