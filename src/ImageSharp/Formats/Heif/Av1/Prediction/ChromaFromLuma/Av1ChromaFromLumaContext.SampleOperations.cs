// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
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
    /// An eight-bit frame has one instruction for this on every supported path. A high-bit-depth
    /// frame has one on x86 alone, so the two halves are loaded and added instead, which reaches the
    /// same lane order without a shuffle.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<short> LoadPairSums<TSample>(ref TSample source, Vector128<short> width)
        where TSample : unmanaged
    {
        if (Unsafe.SizeOf<TSample>() == 1)
        {
            return PairSum(Vector128.LoadUnsafe(ref Unsafe.As<TSample, byte>(ref source)), Vector128.Create((sbyte)1));
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
            ref byte samples = ref Unsafe.As<TSample, byte>(ref source);
            if (Avx2.IsSupported)
            {
                return Avx2.MultiplyAddAdjacent(Vector256.LoadUnsafe(ref samples), Vector256.Create((sbyte)1));
            }

            // Without a 256-bit adjacent multiply-add the two halves take the 128-bit form, which
            // every supported path provides, and are then joined in column order.
            Vector128<sbyte> ones = Vector128.Create((sbyte)1);
            return Vector256.Create(
                PairSum(Vector128.LoadUnsafe(ref samples), ones),
                PairSum(Vector128.LoadUnsafe(ref samples, (nuint)Vector128<byte>.Count), ones));
        }

        ref short wide = ref Unsafe.As<TSample, short>(ref source);
        Vector256<int> lower = Vector256_.MultiplyAddAdjacent(
            Vector256.LoadUnsafe(ref wide), Vector256.Create((short)1));

        Vector256<int> upper = Vector256_.MultiplyAddAdjacent(
            Vector256.LoadUnsafe(ref wide, (nuint)Vector256<short>.Count), Vector256.Create((short)1));

        return Vector256.Narrow(lower, upper);
    }

    /// <summary>
    /// Sums the sixty-four adjacent sample pairs that produce thirty-two chroma columns.
    /// </summary>
    /// <typeparam name="TSample">Byte or ushort, selected by the frame.</typeparam>
    /// <param name="source">The first of exactly sixty-four addressable samples.</param>
    /// <param name="width">The overload-selection value.</param>
    /// <returns>The thirty-two pair sums in increasing column order.</returns>
    /// <remarks>
    /// No instruction set offers an adjacent multiply-add of this width for both sample depths, so
    /// the two halves take the 256-bit form and are joined in column order. The join is free, so
    /// this still moves twice the samples of the narrower stage per iteration.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<short> LoadPairSums<TSample>(ref TSample source, Vector512<short> width)
        where TSample : unmanaged
        => Vector512.Create(
            LoadPairSums(ref source, Vector256<short>.Zero),
            LoadPairSums(ref Unsafe.Add(ref source, 2 * Vector256<short>.Count), Vector256<short>.Zero));

    /// <summary>
    /// Adds adjacent eight-bit samples into eight sixteen-bit lanes.
    /// </summary>
    /// <param name="samples">The packed source samples.</param>
    /// <param name="ones">The multiplier used by the x86 adjacent multiply-add instruction.</param>
    /// <returns>The adjacent pair sums.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<short> PairSum(Vector128<byte> samples, Vector128<sbyte> ones)
    {
        if (Ssse3.IsSupported)
        {
            return Ssse3.MultiplyAddAdjacent(samples, ones);
        }

        if (AdvSimd.IsSupported)
        {
            return AdvSimd.AddPairwiseWidening(samples).AsInt16();
        }

        // WebAssembly has byte shuffles but no pairwise-widening instruction. Grouping the even and
        // the odd bytes before widening keeps all eight additions in vectors.
        Vector128<byte> even = Vector128.Shuffle(samples, Vector128.Create((byte)0, 2, 4, 6, 8, 10, 12, 14, 255, 255, 255, 255, 255, 255, 255, 255));
        Vector128<byte> odd = Vector128.Shuffle(samples, Vector128.Create((byte)1, 3, 5, 7, 9, 11, 13, 15, 255, 255, 255, 255, 255, 255, 255, 255));
        return (Vector128.WidenLower(even) + Vector128.WidenLower(odd)).AsInt16();
    }
}
