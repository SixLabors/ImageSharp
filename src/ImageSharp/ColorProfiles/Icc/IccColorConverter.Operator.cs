// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.ColorProfiles.Icc;

/// <summary>
/// Groups the configured ICC arithmetic shared by scalar and SIMD traversal.
/// </summary>
internal static partial class IccColorConverter
{
    // ICC XYZ stores 1.0 as 0x8000 in a sixteen-bit code. The pixel API normalizes the full 0xFFFF range.
    private const float PcsXyzDecodeScale = 65535F / 32768F;
    private const float PcsXyzEncodeScale = 32768F / 65535F;

    // Version 2 Lab uses 0xFF00 for its maximum. Later encodings use the complete sixteen-bit range.
    private const float LegacyLabDecodeScale = 65535F / 65280F;
    private const float LegacyLabEncodeScale = 65280F / 65535F;

    /// <summary>
    /// Defines one configured ICC transform for scalar values and blocks of independent colors.
    /// </summary>
    internal interface IIccColorConverterOperator
    {
        /// <summary>
        /// Gets a value indicating whether this transform uses tone response curves and a matrix instead of a color lookup table.
        /// </summary>
        bool IsTrc { get; }

        /// <summary>
        /// Gets a value indicating whether this transform uses the legacy PCS encoding of a sixteen-bit lookup table.
        /// </summary>
        bool Is16BitLut { get; }

        /// <summary>
        /// Converts one color using the same stages as the SIMD implementations.
        /// </summary>
        /// <param name="value">The color components in X, Y, Z and W.</param>
        /// <returns>The converted components.</returns>
        Vector4 Convert(Vector4 value);

        /// <summary>
        /// Converts four colors. Each register contains one component across all four colors.
        /// </summary>
        /// <param name="x">The first components.</param>
        /// <param name="y">The second components.</param>
        /// <param name="z">The third components.</param>
        /// <param name="w">The fourth components.</param>
        void Convert(ref Vector128<float> x, ref Vector128<float> y, ref Vector128<float> z, ref Vector128<float> w);

        /// <summary>
        /// Converts eight colors. Each register contains one component across all eight colors.
        /// </summary>
        /// <param name="x">The first components.</param>
        /// <param name="y">The second components.</param>
        /// <param name="z">The third components.</param>
        /// <param name="w">The fourth components.</param>
        void Convert(ref Vector256<float> x, ref Vector256<float> y, ref Vector256<float> z, ref Vector256<float> w);

        /// <summary>
        /// Converts sixteen colors. Each register contains one component across all sixteen colors.
        /// </summary>
        /// <param name="x">The first components.</param>
        /// <param name="y">The second components.</param>
        /// <param name="z">The third components.</param>
        /// <param name="w">The fourth components.</param>
        void Convert(ref Vector512<float> x, ref Vector512<float> y, ref Vector512<float> z, ref Vector512<float> w);
    }
}
