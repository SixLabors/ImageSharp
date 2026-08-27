// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.IntraBlockCopy;

/// <content>
/// Provides the overflow-free rounded-average arithmetic shared by the interpolation operators.
/// </content>
internal static partial class Av1IntraBlockCopyPredictor
{
    /// <summary>
    /// Computes the AV1 rounded average of two unsigned 8-bit vectors without widening their lanes.
    /// </summary>
    /// <param name="left">The first source vector.</param>
    /// <param name="right">The second source vector.</param>
    /// <returns>The lane-wise rounded averages.</returns>
    private static Vector128<byte> AverageRounded(Vector128<byte> left, Vector128<byte> right)
        => (left | right) - ((left ^ right) >> 1);

    /// <summary>
    /// Computes the AV1 rounded average of two unsigned 8-bit vectors without widening their lanes.
    /// </summary>
    /// <param name="left">The first source vector.</param>
    /// <param name="right">The second source vector.</param>
    /// <returns>The lane-wise rounded averages.</returns>
    private static Vector256<byte> AverageRounded(Vector256<byte> left, Vector256<byte> right)
        => (left | right) - ((left ^ right) >> 1);

    /// <summary>
    /// Computes the AV1 rounded average of two unsigned 8-bit vectors without widening their lanes.
    /// </summary>
    /// <param name="left">The first source vector.</param>
    /// <param name="right">The second source vector.</param>
    /// <returns>The lane-wise rounded averages.</returns>
    private static Vector512<byte> AverageRounded(Vector512<byte> left, Vector512<byte> right)
        => (left | right) - ((left ^ right) >> 1);

    /// <summary>
    /// Computes the AV1 rounded average of two nonnegative high-bit-depth vectors without widening their lanes.
    /// </summary>
    /// <param name="left">The first source vector.</param>
    /// <param name="right">The second source vector.</param>
    /// <returns>The lane-wise rounded averages.</returns>
    private static Vector128<short> AverageRounded(Vector128<short> left, Vector128<short> right)
    {
        Vector128<ushort> leftUnsigned = left.AsUInt16();
        Vector128<ushort> rightUnsigned = right.AsUInt16();

        // (a | b) - ((a ^ b) >> 1) is ceil((a + b) / 2) without an overflowing lane-wise addition.
        return ((leftUnsigned | rightUnsigned) - ((leftUnsigned ^ rightUnsigned) >> 1)).AsInt16();
    }

    /// <summary>
    /// Computes the AV1 rounded average of two nonnegative high-bit-depth vectors without widening their lanes.
    /// </summary>
    /// <param name="left">The first source vector.</param>
    /// <param name="right">The second source vector.</param>
    /// <returns>The lane-wise rounded averages.</returns>
    private static Vector256<short> AverageRounded(Vector256<short> left, Vector256<short> right)
    {
        Vector256<ushort> leftUnsigned = left.AsUInt16();
        Vector256<ushort> rightUnsigned = right.AsUInt16();
        return ((leftUnsigned | rightUnsigned) - ((leftUnsigned ^ rightUnsigned) >> 1)).AsInt16();
    }

    /// <summary>
    /// Computes the AV1 rounded average of two nonnegative high-bit-depth vectors without widening their lanes.
    /// </summary>
    /// <param name="left">The first source vector.</param>
    /// <param name="right">The second source vector.</param>
    /// <returns>The lane-wise rounded averages.</returns>
    private static Vector512<short> AverageRounded(Vector512<short> left, Vector512<short> right)
    {
        Vector512<ushort> leftUnsigned = left.AsUInt16();
        Vector512<ushort> rightUnsigned = right.AsUInt16();
        return ((leftUnsigned | rightUnsigned) - ((leftUnsigned ^ rightUnsigned) >> 1)).AsInt16();
    }
}
