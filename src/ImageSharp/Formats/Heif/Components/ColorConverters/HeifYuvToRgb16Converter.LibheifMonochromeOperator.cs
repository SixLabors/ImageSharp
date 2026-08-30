// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Components;

/// <content>
/// Provides pinned-libheif high-bit-depth monochrome presentation without luma-range expansion.
/// </content>
internal static partial class HeifYuvToRgb16Converter
{
    /// <summary>
    /// Copies source-precision luma into each source-precision RGB component.
    /// </summary>
    private readonly struct LibheifMonochromeOperator : IHeifYuvToRgb16Operator
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
            r = y;
            g = y;
            b = y;
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
            r = y;
            g = y;
            b = y;
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
            r = y;
            g = y;
            b = y;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Convert(
            ushort y,
            ushort cb,
            ushort cr,
            in ConversionParameters parameters,
            out int r,
            out int g,
            out int b)
        {
            r = y;
            g = y;
            b = y;
        }
    }
}
