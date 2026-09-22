// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.ChromaFromLuma;

/// <content>
/// Loads reconstructed luma of either sample depth into the signed sixteen-bit lanes that the Q3
/// predictor surface is built from.
/// </content>
/// <remarks>
/// <para>
/// An eight-bit frame stores luma as <c>byte</c> and a high-bit-depth frame stores it as
/// <c>ushort</c>, but the predictor surface is <c>short</c> in both cases. Holding that difference
/// here, in one overload set per register width, lets the traversals be written once and closed
/// over the sample type, which is how the loop restoration filter and the film grain synthesis
/// handle the same difference.
/// </para>
/// <para>
/// Every pair sum fits a signed sixteen-bit lane. The largest case is a twelve-bit frame with both
/// axes subsampled: four samples of 4095 total 16380, and the Q3 shift of one leaves 32760.
/// </para>
/// </remarks>
internal partial class Av1ChromaFromLumaContext
{
    /// <summary>
    /// The fractional bits that every value of the predictor surface carries.
    /// </summary>
    /// <remarks>
    /// The surface is defined in Q3, so one luma sample shifts left by three, a sum of two luma
    /// samples by two, and a sum of four by one. Reference: the CFL_ADD_BITS scaling of
    /// cfl_luma_subsampling_420_lbd_c().
    /// </remarks>
    private const int Q3Shift = 3;

    /// <summary>
    /// Loads one sample.
    /// </summary>
    /// <typeparam name="TSample">Byte or ushort, selected by the frame.</typeparam>
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
    /// <typeparam name="TSample">Byte or ushort, selected by the frame.</typeparam>
    /// <param name="source">The first of exactly eight addressable samples.</param>
    /// <param name="width">The overload-selection value.</param>
    /// <returns>The samples in increasing column order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<short> LoadSamples<TSample>(ref TSample source, Vector128<short> width)
        where TSample : unmanaged
    {
        if (Unsafe.SizeOf<TSample>() == 1)
        {
            // Only the eight bytes this batch owns are read. Pairing them with a zero half and
            // widening the lower half cannot reach across the end of the row.
            Vector64<byte> packed = Vector64.LoadUnsafe(ref Unsafe.As<TSample, byte>(ref source));
            return Vector128.WidenLower(Vector128.Create(packed, Vector64<byte>.Zero)).AsInt16();
        }

        return Vector128.LoadUnsafe(ref Unsafe.As<TSample, short>(ref source));
    }

    /// <summary>
    /// Loads sixteen adjacent samples into signed sixteen-bit lanes.
    /// </summary>
    /// <typeparam name="TSample">Byte or ushort, selected by the frame.</typeparam>
    /// <param name="source">The first of exactly sixteen addressable samples.</param>
    /// <param name="width">The overload-selection value.</param>
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
    /// <typeparam name="TSample">Byte or ushort, selected by the frame.</typeparam>
    /// <param name="source">The first of exactly thirty-two addressable samples.</param>
    /// <param name="width">The overload-selection value.</param>
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
    /// <typeparam name="TSample">Byte or ushort, selected by the frame.</typeparam>
    /// <param name="source">The first of exactly sixteen addressable samples.</param>
    /// <param name="width">The overload-selection value.</param>
    /// <returns>The eight pair sums in increasing column order.</returns>
    /// <remarks>
    /// An eight-bit frame pairs sixteen samples in one call. A high-bit-depth frame produces
    /// thirty-two bit sums, so it takes two calls and narrows them back, which is exact because a
    /// pair sum of twelve-bit samples reaches only 8190.
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
        Vector128<int> lower = Vector128_.MultiplyAddAdjacent(
            Vector128.LoadUnsafe(ref samples), Vector128.Create((short)1));

        Vector128<int> upper = Vector128_.MultiplyAddAdjacent(
            Vector128.LoadUnsafe(ref samples, (nuint)Vector128<short>.Count), Vector128.Create((short)1));

        return Vector128.Narrow(lower, upper);
    }

    /// <summary>
    /// Sums the thirty-two adjacent sample pairs that produce sixteen chroma columns.
    /// </summary>
    /// <typeparam name="TSample">Byte or ushort, selected by the frame.</typeparam>
    /// <param name="source">The first of exactly thirty-two addressable samples.</param>
    /// <param name="width">The overload-selection value.</param>
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
        Vector256<int> lower = Vector256_.MultiplyAddAdjacent(
            Vector256.LoadUnsafe(ref samples), Vector256.Create((short)1));

        Vector256<int> upper = Vector256_.MultiplyAddAdjacent(
            Vector256.LoadUnsafe(ref samples, (nuint)Vector256<short>.Count), Vector256.Create((short)1));

        return Vector256.Narrow(lower, upper);
    }

    /// <summary>
    /// Sums the sixty-four adjacent sample pairs that produce thirty-two chroma columns.
    /// </summary>
    /// <typeparam name="TSample">Byte or ushort, selected by the frame.</typeparam>
    /// <param name="source">The first of exactly sixty-four addressable samples.</param>
    /// <param name="width">The overload-selection value.</param>
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
        Vector512<int> lower = Vector512_.MultiplyAddAdjacent(
            Vector512.LoadUnsafe(ref samples), Vector512.Create((short)1));

        Vector512<int> upper = Vector512_.MultiplyAddAdjacent(
            Vector512.LoadUnsafe(ref samples, (nuint)Vector512<short>.Count), Vector512.Create((short)1));

        return Vector512.Narrow(lower, upper);
    }
}
