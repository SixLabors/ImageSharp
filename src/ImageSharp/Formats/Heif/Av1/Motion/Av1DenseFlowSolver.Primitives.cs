// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

/// <content>
/// Provides the lane primitives shared by the dense inverse search kernels.
/// </content>
internal static partial class Av1DenseFlowSolver
{
    /// <summary>
    /// Loads eight samples and widens them to sixteen-bit lanes.
    /// </summary>
    /// <param name="plane">The first sample of the plane.</param>
    /// <param name="offset">The signed sample offset, which may reach into the border.</param>
    /// <returns>The widened samples.</returns>
    /// <remarks>
    /// The offset is signed because a patch kernel reads one sample before the patch and the plane
    /// carries a replicated border for exactly that reason.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<short> WidenSamples(ref byte plane, nint offset)
    {
        Vector128<byte> samples = Vector128.LoadUnsafe(ref Unsafe.Add(ref plane, offset));
        (Vector128<ushort> low, Vector128<ushort> _) = Vector128.Widen(samples);
        return low.AsInt16();
    }

    /// <summary>
    /// Loads eight samples and widens them to two vectors of thirty-two bit lanes.
    /// </summary>
    /// <param name="plane">The first sample of the plane.</param>
    /// <param name="offset">The signed sample offset, which may reach into the border.</param>
    /// <param name="low">Receives the first four samples.</param>
    /// <param name="high">Receives the second four samples.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WidenSamples(ref byte plane, nint offset, out Vector128<int> low, out Vector128<int> high)
    {
        Vector128<short> samples = WidenSamples(ref plane, offset);
        (low, high) = Widen(samples);
    }

    /// <summary>
    /// Widens eight sixteen-bit lanes to two vectors of thirty-two bit lanes.
    /// </summary>
    /// <param name="source">The signed source lanes.</param>
    /// <returns>The first four and second four widened lanes.</returns>
    /// <remarks>
    /// The widening is sign preserving, which matters because the gradients and the interpolated
    /// values are both signed.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (Vector128<int> Low, Vector128<int> High) Widen(Vector128<short> source)
    {
        (Vector128<int> low, Vector128<int> high) = Vector128.Widen(source);
        return (low, high);
    }

    /// <summary>
    /// Applies a rounding right shift to thirty-two bit lanes.
    /// </summary>
    /// <param name="value">The lanes to shift.</param>
    /// <param name="bits">The shift, which is always one or more here.</param>
    /// <returns>The shifted lanes.</returns>
    /// <remarks>
    /// The rounding term is added before the arithmetic shift, which is the reference's
    /// ROUND_POWER_OF_TWO for a signed value.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> RoundShift(Vector128<int> value, int bits)
        => (value + Vector128.Create(1 << (bits - 1))) >> bits;
}
