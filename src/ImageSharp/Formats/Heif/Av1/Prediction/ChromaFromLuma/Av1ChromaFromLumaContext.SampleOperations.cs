// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.ChromaFromLuma;

/// <content>
/// Loads reconstructed luma of either sample depth into the signed sixteen-bit lanes of the Q3 predictor surface.
/// </content>
/// <remarks>
/// <para>
/// An eight-bit frame stores luma as <c>byte</c>, and a high-bit-depth frame stores it as nonnegative <c>short</c> values. The predictor surface is
/// <c>short</c> in both cases. This file holds that difference in one overload set per register width. Thus the traversals are written once and closed over the
/// sample type. The loop restoration filter and the film grain synthesis handle the same difference in this way.
/// </para>
/// <para>
/// Every pair sum fits a signed sixteen-bit lane. The largest case is a twelve-bit frame with both axes subsampled. Four samples of 4095 total 16380, and the
/// Q3 shift of one gives 32760.
/// </para>
/// </remarks>
internal partial class Av1ChromaFromLumaContext
{
    /// <summary>
    /// The fractional bits that every value of the predictor surface carries.
    /// </summary>
    /// <remarks>
    /// The surface is defined in Q3. Thus one luma sample shifts left by three, a sum of two luma samples by two, and a sum of four by one.
    /// </remarks>
    private const int Q3Shift = 3;

    /// <summary>
    /// Loads one sample.
    /// </summary>
    /// <typeparam name="TSample">The sample type, byte or short, that the frame bit depth selects.</typeparam>
    /// <param name="source">The sample.</param>
    /// <returns>The sample value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int LoadSample<TSample>(ref TSample source)
        where TSample : unmanaged
        => Unsafe.SizeOf<TSample>() == 1
            ? Unsafe.As<TSample, byte>(ref source)
            : Unsafe.As<TSample, ushort>(ref source);

    /// <summary>
    /// Loads eight adjacent samples into signed sixteen-bit lanes.
    /// </summary>
    /// <typeparam name="TSample">The sample type, byte or short, that the frame bit depth selects.</typeparam>
    /// <param name="source">The first of exactly eight addressable samples.</param>
    /// <param name="width">An unused value whose type selects the vector width.</param>
    /// <returns>The samples in increasing column order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<short> LoadSamples<TSample>(ref TSample source, Vector128<short> width)
        where TSample : unmanaged
    {
        if (Unsafe.SizeOf<TSample>() == 1)
        {
            // The load reads only the eight bytes of this batch. The code pairs them with a zero half and widens the lower half, so no read goes past the end
            // of the row.
            Vector64<byte> packed = Vector64.LoadUnsafe(ref Unsafe.As<TSample, byte>(ref source));
            return Vector128.WidenLower(Vector128.Create(packed, Vector64<byte>.Zero)).AsInt16();
        }

        return Vector128.LoadUnsafe(ref Unsafe.As<TSample, short>(ref source));
    }

    /// <summary>
    /// Loads sixteen adjacent samples into signed sixteen-bit lanes.
    /// </summary>
    /// <typeparam name="TSample">The sample type, byte or short, that the frame bit depth selects.</typeparam>
    /// <param name="source">The first of exactly sixteen addressable samples.</param>
    /// <param name="width">An unused value whose type selects the vector width.</param>
    /// <returns>The samples in increasing column order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<short> LoadSamples<TSample>(ref TSample source, Vector256<short> width)
        where TSample : unmanaged
    {
        if (Unsafe.SizeOf<TSample>() == 1)
        {
            Vector128<byte> packed = Vector128.LoadUnsafe(ref Unsafe.As<TSample, byte>(ref source));
            return Vector256.WidenLower(Vector256.Create(packed, Vector128<byte>.Zero)).AsInt16();
        }

        return Vector256.LoadUnsafe(ref Unsafe.As<TSample, short>(ref source));
    }

    /// <summary>
    /// Loads thirty-two adjacent samples into signed sixteen-bit lanes.
    /// </summary>
    /// <typeparam name="TSample">The sample type, byte or short, that the frame bit depth selects.</typeparam>
    /// <param name="source">The first of exactly thirty-two addressable samples.</param>
    /// <param name="width">An unused value whose type selects the vector width.</param>
    /// <returns>The samples in increasing column order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<short> LoadSamples<TSample>(ref TSample source, Vector512<short> width)
        where TSample : unmanaged
    {
        if (Unsafe.SizeOf<TSample>() == 1)
        {
            Vector256<byte> packed = Vector256.LoadUnsafe(ref Unsafe.As<TSample, byte>(ref source));
            return Vector512.WidenLower(Vector512.Create(packed, Vector256<byte>.Zero)).AsInt16();
        }

        return Vector512.LoadUnsafe(ref Unsafe.As<TSample, short>(ref source));
    }

    /// <summary>
    /// Sums the sixteen adjacent sample pairs that produce eight chroma columns.
    /// </summary>
    /// <typeparam name="TSample">The sample type, byte or short, that the frame bit depth selects.</typeparam>
    /// <param name="source">The first of exactly sixteen addressable samples.</param>
    /// <param name="width">An unused value whose type selects the vector width.</param>
    /// <returns>The eight pair sums in increasing column order.</returns>
    /// <remarks>
    /// A multiply-add by one adds adjacent lanes, so lane i of the result is the sum of samples 2i and 2i + 1. An eight-bit frame pairs sixteen samples in one
    /// multiply-add. A high-bit-depth frame produces 32-bit sums, so it uses two multiply-adds and narrows the results. The narrowing is exact, because a pair
    /// sum of twelve-bit samples reaches only 8190.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<short> LoadPairSums<TSample>(ref TSample source, Vector128<short> width)
        where TSample : unmanaged
    {
        if (Unsafe.SizeOf<TSample>() == 1)
        {
            return Vector128_.MultiplyAddAdjacent(
                Vector128.LoadUnsafe(ref Unsafe.As<TSample, byte>(ref source)), Vector128.Create((sbyte)1));
        }

        ref short samples = ref Unsafe.As<TSample, short>(ref source);
        Vector128<int> lower = Vector128_.MultiplyAddAdjacent(Vector128.LoadUnsafe(ref samples), Vector128.Create((short)1));

        Vector128<int> upper = Vector128_.MultiplyAddAdjacent(
            Vector128.LoadUnsafe(ref samples, (nuint)Vector128<short>.Count), Vector128.Create((short)1));

        return Vector128.Narrow(lower, upper);
    }

    /// <summary>
    /// Sums the thirty-two adjacent sample pairs that produce sixteen chroma columns.
    /// </summary>
    /// <typeparam name="TSample">The sample type, byte or short, that the frame bit depth selects.</typeparam>
    /// <param name="source">The first of exactly thirty-two addressable samples.</param>
    /// <param name="width">An unused value whose type selects the vector width.</param>
    /// <returns>The sixteen pair sums in increasing column order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<short> LoadPairSums<TSample>(ref TSample source, Vector256<short> width)
        where TSample : unmanaged
    {
        if (Unsafe.SizeOf<TSample>() == 1)
        {
            return Vector256_.MultiplyAddAdjacent(
                Vector256.LoadUnsafe(ref Unsafe.As<TSample, byte>(ref source)), Vector256.Create((sbyte)1));
        }

        ref short samples = ref Unsafe.As<TSample, short>(ref source);
        Vector256<int> lower = Vector256_.MultiplyAddAdjacent(Vector256.LoadUnsafe(ref samples), Vector256.Create((short)1));

        Vector256<int> upper = Vector256_.MultiplyAddAdjacent(
            Vector256.LoadUnsafe(ref samples, (nuint)Vector256<short>.Count), Vector256.Create((short)1));

        return Vector256.Narrow(lower, upper);
    }

    /// <summary>
    /// Sums the sixty-four adjacent sample pairs that produce thirty-two chroma columns.
    /// </summary>
    /// <typeparam name="TSample">The sample type, byte or short, that the frame bit depth selects.</typeparam>
    /// <param name="source">The first of exactly sixty-four addressable samples.</param>
    /// <param name="width">An unused value whose type selects the vector width.</param>
    /// <returns>The thirty-two pair sums in increasing column order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<short> LoadPairSums<TSample>(ref TSample source, Vector512<short> width)
        where TSample : unmanaged
    {
        if (Unsafe.SizeOf<TSample>() == 1)
        {
            return Vector512_.MultiplyAddAdjacent(
                Vector512.LoadUnsafe(ref Unsafe.As<TSample, byte>(ref source)), Vector512.Create((sbyte)1));
        }

        ref short samples = ref Unsafe.As<TSample, short>(ref source);
        Vector512<int> lower = Vector512_.MultiplyAddAdjacent(Vector512.LoadUnsafe(ref samples), Vector512.Create((short)1));

        Vector512<int> upper = Vector512_.MultiplyAddAdjacent(
            Vector512.LoadUnsafe(ref samples, (nuint)Vector512<short>.Count), Vector512.Create((short)1));

        return Vector512.Narrow(lower, upper);
    }
}
