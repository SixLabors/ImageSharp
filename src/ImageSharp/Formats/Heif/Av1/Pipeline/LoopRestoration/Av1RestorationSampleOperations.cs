// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopRestoration;

/// <summary>
/// Loads and stores unsigned restoration samples without changing their numerical scale.
/// </summary>
internal static class Av1RestorationSampleOperations
{
    /// <summary>
    /// Loads one unsigned sample.
    /// </summary>
    /// <typeparam name="TSample">Byte or ushort, selected once by the frame's physical sample precision.</typeparam>
    /// <param name="sample">The stored sample.</param>
    /// <returns>The unscaled sample value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Load<TSample>(TSample sample)
        where TSample : unmanaged
        => Unsafe.SizeOf<TSample>() == 1 ? Unsafe.As<TSample, byte>(ref sample) : Unsafe.As<TSample, ushort>(ref sample);

    /// <summary>
    /// Stores an already clipped sample at the selected physical precision.
    /// </summary>
    /// <typeparam name="TSample">Byte or ushort, selected by the frame.</typeparam>
    /// <param name="value">The clipped sample value.</param>
    /// <returns>The stored sample representation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TSample FromInt32<TSample>(int value)
        where TSample : unmanaged
    {
        // Only the two frame-selected instantiations reach this path. The size branch is constant in
        // either instantiation; narrowing preserves the value because the filter has already clipped it.
        TSample result = default;
        if (Unsafe.SizeOf<TSample>() == 1)
        {
            Unsafe.As<TSample, byte>(ref result) = (byte)value;
        }
        else
        {
            Unsafe.As<TSample, ushort>(ref result) = (ushort)value;
        }

        return result;
    }

    /// <summary>
    /// Loads 8 adjacent samples into unsigned 16-bit lanes.
    /// </summary>
    /// <typeparam name="TSample">Byte or ushort, selected by the frame.</typeparam>
    /// <param name="source">The first of exactly 8 addressable samples.</param>
    /// <param name="vector">The overload-selection value.</param>
    /// <returns>The unscaled samples in increasing column order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<ushort> LoadToUInt16<TSample>(ref TSample source, Vector128<ushort> vector)
        where TSample : unmanaged
    {
        if (Unsafe.SizeOf<TSample>() == 1)
        {
            // Read only the bytes owned by this batch. The zero upper half supplies unused lanes,
            // so widening cannot read across the row boundary to fill a full-width source vector.
            Vector64<byte> packed = Vector64.LoadUnsafe(ref Unsafe.As<TSample, byte>(ref source));
            return Vector128.WidenLower(Vector128.Create(packed, Vector64<byte>.Zero));
        }

        return Vector128.LoadUnsafe(ref Unsafe.As<TSample, ushort>(ref source));
    }

    /// <summary>
    /// Loads 16 adjacent samples into unsigned 16-bit lanes.
    /// </summary>
    /// <typeparam name="TSample">Byte or ushort, selected by the frame.</typeparam>
    /// <param name="source">The first of exactly 16 addressable samples.</param>
    /// <param name="vector">The overload-selection value.</param>
    /// <returns>The unscaled samples in increasing column order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<ushort> LoadToUInt16<TSample>(ref TSample source, Vector256<ushort> vector)
        where TSample : unmanaged
    {
        if (Unsafe.SizeOf<TSample>() == 1)
        {
            // Read only the bytes owned by this batch. The zero upper half supplies unused lanes,
            // so widening cannot read across the row boundary to fill a full-width source vector.
            Vector128<byte> packed = Vector128.LoadUnsafe(ref Unsafe.As<TSample, byte>(ref source));
            return Vector256.WidenLower(Vector256.Create(packed, Vector128<byte>.Zero));
        }

        return Vector256.LoadUnsafe(ref Unsafe.As<TSample, ushort>(ref source));
    }

    /// <summary>
    /// Loads 32 adjacent samples into unsigned 16-bit lanes.
    /// </summary>
    /// <typeparam name="TSample">Byte or ushort, selected by the frame.</typeparam>
    /// <param name="source">The first of exactly 32 addressable samples.</param>
    /// <param name="vector">The overload-selection value.</param>
    /// <returns>The unscaled samples in increasing column order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<ushort> LoadToUInt16<TSample>(ref TSample source, Vector512<ushort> vector)
        where TSample : unmanaged
    {
        if (Unsafe.SizeOf<TSample>() == 1)
        {
            // Read only the bytes owned by this batch. The zero upper half supplies unused lanes,
            // so widening cannot read across the row boundary to fill a full-width source vector.
            Vector256<byte> packed = Vector256.LoadUnsafe(ref Unsafe.As<TSample, byte>(ref source));
            return Vector512.WidenLower(Vector512.Create(packed, Vector256<byte>.Zero));
        }

        return Vector512.LoadUnsafe(ref Unsafe.As<TSample, ushort>(ref source));
    }

    /// <summary>
    /// Loads 4 adjacent samples into signed 32-bit arithmetic lanes.
    /// </summary>
    /// <typeparam name="TSample">Byte or ushort, selected by the frame.</typeparam>
    /// <param name="source">The first of exactly 4 addressable samples.</param>
    /// <param name="vector">The overload-selection value.</param>
    /// <returns>The nonnegative sample values in increasing column order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<int> LoadToInt32<TSample>(ref TSample source, Vector128<int> vector)
        where TSample : unmanaged
    {
        if (Unsafe.SizeOf<TSample>() == 1)
        {
            // Four bytes are enough for four result lanes. Both widening steps consume only
            // their initialized lower halves; no padding or neighboring sample is needed.
            uint packed = Unsafe.ReadUnaligned<uint>(ref Unsafe.As<TSample, byte>(ref source));
            Vector128<ushort> words = Vector128.WidenLower(Vector128.CreateScalar(packed).AsByte());
            return Vector128.WidenLower(words).AsInt32();
        }

        Vector64<ushort> samples = Vector64.LoadUnsafe(ref Unsafe.As<TSample, ushort>(ref source));
        return Vector128.WidenLower(Vector128.Create(samples, Vector64<ushort>.Zero)).AsInt32();
    }

    /// <summary>
    /// Loads 8 adjacent samples into signed 32-bit arithmetic lanes.
    /// </summary>
    /// <typeparam name="TSample">Byte or ushort, selected by the frame.</typeparam>
    /// <param name="source">The first of exactly 8 addressable samples.</param>
    /// <param name="vector">The overload-selection value.</param>
    /// <returns>The nonnegative sample values in increasing column order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<int> LoadToInt32<TSample>(ref TSample source, Vector256<int> vector)
        where TSample : unmanaged
    {
        if (Unsafe.SizeOf<TSample>() == 1)
        {
            Vector64<byte> packed = Vector64.LoadUnsafe(ref Unsafe.As<TSample, byte>(ref source));
            Vector128<ushort> words = Vector128.WidenLower(Vector128.Create(packed, Vector64<byte>.Zero));
            return Vector256.WidenLower(Vector256.Create(words, Vector128<ushort>.Zero)).AsInt32();
        }

        Vector128<ushort> samples = Vector128.LoadUnsafe(ref Unsafe.As<TSample, ushort>(ref source));
        return Vector256.WidenLower(Vector256.Create(samples, Vector128<ushort>.Zero)).AsInt32();
    }

    /// <summary>
    /// Stores 4 already clipped samples at the frame's physical precision.
    /// </summary>
    /// <typeparam name="TSample">Byte or ushort, selected by the frame.</typeparam>
    /// <param name="samples">The clipped samples in increasing column order.</param>
    /// <param name="destination">The first of exactly 4 writable samples.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Store<TSample>(Vector64<ushort> samples, ref TSample destination)
        where TSample : unmanaged
    {
        if (Unsafe.SizeOf<TSample>() == 1)
        {
            // Clipping has restricted every lane to 0..255. Store just the populated half after
            // narrowing, so neither a row tail nor an adjacent destination row is overwritten.
            Vector128<ushort> words = Vector128.Create(samples, Vector64<ushort>.Zero);
            uint packed = Vector128.Narrow(words, Vector128<ushort>.Zero).AsUInt32().GetElement(0);
            Unsafe.WriteUnaligned(ref Unsafe.As<TSample, byte>(ref destination), packed);
        }
        else
        {
            samples.StoreUnsafe(ref Unsafe.As<TSample, ushort>(ref destination));
        }
    }

    /// <summary>
    /// Stores 8 already clipped samples at the frame's physical precision.
    /// </summary>
    /// <typeparam name="TSample">Byte or ushort, selected by the frame.</typeparam>
    /// <param name="samples">The clipped samples in increasing column order.</param>
    /// <param name="destination">The first of exactly 8 writable samples.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Store<TSample>(Vector128<ushort> samples, ref TSample destination)
        where TSample : unmanaged
    {
        if (Unsafe.SizeOf<TSample>() == 1)
        {
            // Clipping has restricted every lane to 0..255. Store just the populated half after
            // narrowing, so neither a row tail nor an adjacent destination row is overwritten.
            Vector64<byte> packed = Vector128.Narrow(samples, Vector128<ushort>.Zero).GetLower();
            packed.StoreUnsafe(ref Unsafe.As<TSample, byte>(ref destination));
        }
        else
        {
            samples.StoreUnsafe(ref Unsafe.As<TSample, ushort>(ref destination));
        }
    }

    /// <summary>
    /// Stores 16 already clipped samples at the frame's physical precision.
    /// </summary>
    /// <typeparam name="TSample">Byte or ushort, selected by the frame.</typeparam>
    /// <param name="samples">The clipped samples in increasing column order.</param>
    /// <param name="destination">The first of exactly 16 writable samples.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Store<TSample>(Vector256<ushort> samples, ref TSample destination)
        where TSample : unmanaged
    {
        if (Unsafe.SizeOf<TSample>() == 1)
        {
            // Clipping has restricted every lane to 0..255. Store just the populated half after
            // narrowing, so neither a row tail nor an adjacent destination row is overwritten.
            Vector128<byte> packed = Vector256.Narrow(samples, Vector256<ushort>.Zero).GetLower();
            packed.StoreUnsafe(ref Unsafe.As<TSample, byte>(ref destination));
        }
        else
        {
            samples.StoreUnsafe(ref Unsafe.As<TSample, ushort>(ref destination));
        }
    }

    /// <summary>
    /// Stores 32 already clipped samples at the frame's physical precision.
    /// </summary>
    /// <typeparam name="TSample">Byte or ushort, selected by the frame.</typeparam>
    /// <param name="samples">The clipped samples in increasing column order.</param>
    /// <param name="destination">The first of exactly 32 writable samples.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Store<TSample>(Vector512<ushort> samples, ref TSample destination)
        where TSample : unmanaged
    {
        if (Unsafe.SizeOf<TSample>() == 1)
        {
            // Clipping has restricted every lane to 0..255. Store just the populated half after
            // narrowing, so neither a row tail nor an adjacent destination row is overwritten.
            Vector256<byte> packed = Vector512.Narrow(samples, Vector512<ushort>.Zero).GetLower();
            packed.StoreUnsafe(ref Unsafe.As<TSample, byte>(ref destination));
        }
        else
        {
            samples.StoreUnsafe(ref Unsafe.As<TSample, ushort>(ref destination));
        }
    }
}
