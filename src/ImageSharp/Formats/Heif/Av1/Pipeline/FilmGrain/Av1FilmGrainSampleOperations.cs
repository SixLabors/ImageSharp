// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.FilmGrain;

/// <summary>
/// Loads and stores native AV1 samples for the film-grain arithmetic pipeline.
/// </summary>
/// <typeparam name="TSample">The native sample type.</typeparam>
/// <remarks>
/// The decoder closes this operator over <see cref="byte"/> for eight-bit planes and <see cref="ushort"/> for
/// high-bit-depth planes. The JIT removes the unused type branch, so widening and narrowing remain branch-free in the
/// row loops. All arithmetic uses signed 32-bit lanes; decoded samples are nonnegative and at most twelve bits, making
/// the intermediate signed 16-bit views safe wherever pairwise operations require them.
/// </remarks>
internal readonly struct Av1FilmGrainSampleOperations<TSample>
    where TSample : unmanaged
{
    /// <summary>
    /// Loads eight consecutive samples into 32-bit lanes.
    /// </summary>
    /// <param name="source">The first source sample.</param>
    /// <returns>The widened samples.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<int> Load8(ref TSample source)
    {
        if (typeof(TSample) == typeof(byte))
        {
            // Reading exactly eight bytes avoids touching the following row when the visible width has no padding.
            ref byte sourceBytes = ref Unsafe.As<TSample, byte>(ref source);
            ulong packed = Unsafe.ReadUnaligned<ulong>(ref sourceBytes);
            return Avx2.ConvertToVector256Int32(Vector128.CreateScalarUnsafe(packed).AsByte());
        }

        ref ushort sourceValues = ref Unsafe.As<TSample, ushort>(ref source);
        return Avx2.ConvertToVector256Int32(Vector128.LoadUnsafe(ref sourceValues));
    }

    /// <summary>
    /// Loads four consecutive samples into 32-bit lanes.
    /// </summary>
    /// <param name="source">The first source sample.</param>
    /// <returns>The widened samples.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<int> Load4(ref TSample source)
    {
        if (typeof(TSample) == typeof(byte))
        {
            // The source occupies only the low four byte lanes; two lower-half widens preserve their original order.
            ref byte sourceBytes = ref Unsafe.As<TSample, byte>(ref source);
            uint packedBytes = Unsafe.ReadUnaligned<uint>(ref sourceBytes);
            Vector128<ushort> widened = Vector128.WidenLower(Vector128.CreateScalarUnsafe(packedBytes).AsByte());
            return Vector128.WidenLower(widened).AsInt32();
        }

        ref ushort sourceValues = ref Unsafe.As<TSample, ushort>(ref source);
        ulong packedValues = Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<ushort, byte>(ref sourceValues));
        Vector64<ushort> packedSamples = Vector64.CreateScalarUnsafe(packedValues).AsUInt16();
        return Vector128.WidenLower(Vector128.Create(packedSamples, Vector64<ushort>.Zero)).AsInt32();
    }

    /// <summary>
    /// Loads the luma coordinates corresponding to eight chroma samples.
    /// </summary>
    /// <param name="source">The first luma sample.</param>
    /// <param name="subsamplingX">The horizontal chroma subsampling shift.</param>
    /// <returns>The luma values used by chroma scaling.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<int> LoadChromaLuma8(ref TSample source, int subsamplingX)
    {
        if (subsamplingX == 0)
        {
            return Load8(ref source);
        }

        Vector256<short> lumaPairs;
        if (typeof(TSample) == typeof(byte))
        {
            ref byte sourceBytes = ref Unsafe.As<TSample, byte>(ref source);
            Vector128<byte> packed = Vector128.LoadUnsafe(ref sourceBytes);
            lumaPairs = Vector256.Create(Vector128.WidenLower(packed).AsInt16(), Vector128.WidenUpper(packed).AsInt16());
        }
        else
        {
            ref ushort sourceValues = ref Unsafe.As<TSample, ushort>(ref source);
            lumaPairs = Vector256.LoadUnsafe(ref sourceValues).AsInt16();
        }

        // Horizontal 4:2:x chroma uses the rounded mean of each adjacent luma pair.
        return (Avx2.MultiplyAddAdjacent(lumaPairs, Vector256.Create((short)1)) + Vector256<int>.One) >> 1;
    }

    /// <summary>
    /// Loads the luma coordinates corresponding to four chroma samples.
    /// </summary>
    /// <param name="source">The first luma sample.</param>
    /// <param name="subsamplingX">The horizontal chroma subsampling shift.</param>
    /// <returns>The luma values used by chroma scaling.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<int> LoadChromaLuma4(ref TSample source, int subsamplingX)
    {
        if (subsamplingX == 0)
        {
            return Load4(ref source);
        }

        Vector128<short> lumaPairs;
        if (typeof(TSample) == typeof(byte))
        {
            ref byte sourceBytes = ref Unsafe.As<TSample, byte>(ref source);
            ulong packed = Unsafe.ReadUnaligned<ulong>(ref sourceBytes);
            lumaPairs = Vector128.WidenLower(Vector128.CreateScalarUnsafe(packed).AsByte()).AsInt16();
        }
        else
        {
            ref ushort sourceValues = ref Unsafe.As<TSample, ushort>(ref source);
            lumaPairs = Vector128.LoadUnsafe(ref sourceValues).AsInt16();
        }

        // Multiply-add with unity coefficients collapses four adjacent pairs without scalar deinterleaving.
        return (Vector128_.MultiplyAddAdjacent(lumaPairs, Vector128.Create((short)1)) + Vector128<int>.One) >> 1;
    }

    /// <summary>
    /// Stores eight already clipped 32-bit samples in their native representation.
    /// </summary>
    /// <param name="destination">The first destination sample.</param>
    /// <param name="values">The samples to store.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Store8(ref TSample destination, Vector256<int> values)
    {
        // AddNoise has already clipped every lane to the native sample range. Unsigned narrowing is therefore exact
        // and serves only to repack lane width; it does not supply saturation or alter out-of-range values.
        Vector128<ushort> packed = Vector128.Narrow(values.GetLower().AsUInt32(), values.GetUpper().AsUInt32());
        if (typeof(TSample) == typeof(byte))
        {
            Vector64<byte> bytes = Vector128.Narrow(packed, Vector128<ushort>.Zero).GetLower();
            bytes.StoreUnsafe(ref Unsafe.As<TSample, byte>(ref destination));
        }
        else
        {
            packed.StoreUnsafe(ref Unsafe.As<TSample, ushort>(ref destination));
        }
    }

    /// <summary>
    /// Stores four already clipped 32-bit samples in their native representation.
    /// </summary>
    /// <param name="destination">The first destination sample.</param>
    /// <param name="values">The samples to store.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Store4(ref TSample destination, Vector128<int> values)
    {
        // Only four destination samples are valid. Narrow through the low half, then write four bytes or four UInt16
        // values so the store cannot overwrite the next row or an adjacent plane allocation.
        Vector64<ushort> packed = Vector128.Narrow(values.AsUInt32(), Vector128<uint>.Zero).GetLower();
        if (typeof(TSample) == typeof(byte))
        {
            Vector64<byte> bytes = Vector128.Narrow(Vector128.Create(packed, Vector64<ushort>.Zero), Vector128<ushort>.Zero).GetLower();
            Unsafe.WriteUnaligned(ref Unsafe.As<TSample, byte>(ref destination), bytes.AsUInt32().GetElement(0));
        }
        else
        {
            packed.StoreUnsafe(ref Unsafe.As<TSample, ushort>(ref destination));
        }
    }

    /// <summary>
    /// Reads one native sample as an integer.
    /// </summary>
    /// <param name="source">The source sample.</param>
    /// <returns>The unsigned sample value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Load(ref TSample source)
        => typeof(TSample) == typeof(byte)
            ? Unsafe.As<TSample, byte>(ref source)
            : Unsafe.As<TSample, ushort>(ref source);

    /// <summary>
    /// Stores one already clipped sample in its native representation.
    /// </summary>
    /// <param name="destination">The destination sample.</param>
    /// <param name="value">The sample value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Store(ref TSample destination, int value)
    {
        if (typeof(TSample) == typeof(byte))
        {
            Unsafe.As<TSample, byte>(ref destination) = (byte)value;
        }
        else
        {
            Unsafe.As<TSample, ushort>(ref destination) = (ushort)value;
        }
    }
}
