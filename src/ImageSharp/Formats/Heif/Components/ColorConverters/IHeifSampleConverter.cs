// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Components;

/// <summary>
/// Defines SIMD widening and narrowing operations for one native HEIF sample representation.
/// </summary>
/// <typeparam name="TSample">The native sample type.</typeparam>
internal interface IHeifSampleConverter<TSample>
    where TSample : unmanaged
{
    /// <summary>
    /// Loads and widens four samples to single-precision lanes.
    /// </summary>
    /// <param name="source">The first source sample.</param>
    /// <returns>The widened samples.</returns>
    public static abstract Vector128<float> LoadVector128(ref TSample source);

    /// <summary>
    /// Loads and widens eight samples to single-precision lanes.
    /// </summary>
    /// <param name="source">The first source sample.</param>
    /// <returns>The widened samples.</returns>
    public static abstract Vector256<float> LoadVector256(ref TSample source);

    /// <summary>
    /// Loads and widens sixteen samples to single-precision lanes.
    /// </summary>
    /// <param name="source">The first source sample.</param>
    /// <returns>The widened samples.</returns>
    public static abstract Vector512<float> LoadVector512(ref TSample source);

    /// <summary>
    /// Narrows and stores four integer samples.
    /// </summary>
    /// <param name="source">The integer samples.</param>
    /// <param name="destination">The first destination sample.</param>
    public static abstract void Store(Vector128<int> source, ref TSample destination);

    /// <summary>
    /// Narrows and stores eight integer samples.
    /// </summary>
    /// <param name="source">The integer samples.</param>
    /// <param name="destination">The first destination sample.</param>
    public static abstract void Store(Vector256<int> source, ref TSample destination);

    /// <summary>
    /// Narrows and stores sixteen integer samples.
    /// </summary>
    /// <param name="source">The integer samples.</param>
    /// <param name="destination">The first destination sample.</param>
    public static abstract void Store(Vector512<int> source, ref TSample destination);
}

/// <summary>
/// Converts between eight-bit native samples and the planar conversion pipeline.
/// </summary>
internal readonly struct HeifByteSampleConverter : IHeifSampleConverter<byte>
{
    /// <inheritdoc/>
    public static Vector128<float> LoadVector128(ref byte source)
    {
        uint packed = Unsafe.ReadUnaligned<uint>(ref source);
        Vector128<ushort> samples16 = Vector128.WidenLower(Vector128.CreateScalarUnsafe(packed).AsByte());
        return Vector128.ConvertToSingle(Vector128.WidenLower(samples16));
    }

    /// <inheritdoc/>
    public static Vector256<float> LoadVector256(ref byte source)
    {
        ulong packed = Unsafe.ReadUnaligned<ulong>(ref source);
        Vector128<ushort> samples16 = Vector128.WidenLower(Vector128.CreateScalarUnsafe(packed).AsByte());
        Vector256<uint> samples32 = Vector256.Create(Vector128.WidenLower(samples16), Vector128.WidenUpper(samples16));
        return Vector256.ConvertToSingle(samples32);
    }

    /// <inheritdoc/>
    public static Vector512<float> LoadVector512(ref byte source)
    {
        Vector128<byte> packed = Unsafe.ReadUnaligned<Vector128<byte>>(ref source);
        (Vector128<ushort> lower16, Vector128<ushort> upper16) = Vector128.Widen(packed);
        Vector256<uint> lower32 = Vector256.Create(Vector128.WidenLower(lower16), Vector128.WidenUpper(lower16));
        Vector256<uint> upper32 = Vector256.Create(Vector128.WidenLower(upper16), Vector128.WidenUpper(upper16));
        return Vector512.ConvertToSingle(Vector512.Create(lower32, upper32));
    }

    /// <inheritdoc/>
    public static void Store(Vector128<int> source, ref byte destination)
    {
        Vector128<ushort> samples16 = Vector128.Narrow(source.AsUInt32(), Vector128<uint>.Zero);
        Vector128<byte> samples8 = Vector128.Narrow(samples16, Vector128<ushort>.Zero);

        // The lower four bytes contain the four source lanes after the two narrowing stages.
        Unsafe.WriteUnaligned(ref destination, samples8.AsUInt32().ToScalar());
    }

    /// <inheritdoc/>
    public static void Store(Vector256<int> source, ref byte destination)
    {
        Store(source.GetLower(), ref destination);
        Store(source.GetUpper(), ref Unsafe.Add(ref destination, Vector128<int>.Count));
    }

    /// <inheritdoc/>
    public static void Store(Vector512<int> source, ref byte destination)
    {
        Store(source.GetLower(), ref destination);
        Store(source.GetUpper(), ref Unsafe.Add(ref destination, Vector256<int>.Count));
    }
}

/// <summary>
/// Converts between unsigned 16-bit native samples and the planar conversion pipeline.
/// </summary>
internal readonly struct HeifUShortSampleConverter : IHeifSampleConverter<ushort>
{
    /// <inheritdoc/>
    public static Vector128<float> LoadVector128(ref ushort source)
    {
        ulong packed = Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<ushort, byte>(ref source));
        Vector128<ushort> samples16 = Vector128.CreateScalarUnsafe(packed).AsUInt16();
        return Vector128.ConvertToSingle(Vector128.WidenLower(samples16));
    }

    /// <inheritdoc/>
    public static Vector256<float> LoadVector256(ref ushort source)
    {
        Vector128<ushort> samples16 = Unsafe.ReadUnaligned<Vector128<ushort>>(ref Unsafe.As<ushort, byte>(ref source));
        Vector256<uint> samples32 = Vector256.Create(Vector128.WidenLower(samples16), Vector128.WidenUpper(samples16));
        return Vector256.ConvertToSingle(samples32);
    }

    /// <inheritdoc/>
    public static Vector512<float> LoadVector512(ref ushort source)
    {
        Vector256<ushort> samples16 = Unsafe.ReadUnaligned<Vector256<ushort>>(ref Unsafe.As<ushort, byte>(ref source));
        (Vector256<uint> lower32, Vector256<uint> upper32) = Vector256.Widen(samples16);
        return Vector512.ConvertToSingle(Vector512.Create(lower32, upper32));
    }

    /// <inheritdoc/>
    public static void Store(Vector128<int> source, ref ushort destination)
    {
        Vector128<ushort> samples = Vector128.Narrow(source.AsUInt32(), Vector128<uint>.Zero);

        // The lower four UInt16 values are contiguous and can be committed with one unaligned store.
        Unsafe.WriteUnaligned(ref Unsafe.As<ushort, byte>(ref destination), samples.AsUInt64().ToScalar());
    }

    /// <inheritdoc/>
    public static void Store(Vector256<int> source, ref ushort destination)
    {
        Store(source.GetLower(), ref destination);
        Store(source.GetUpper(), ref Unsafe.Add(ref destination, Vector128<int>.Count));
    }

    /// <inheritdoc/>
    public static void Store(Vector512<int> source, ref ushort destination)
    {
        Store(source.GetLower(), ref destination);
        Store(source.GetUpper(), ref Unsafe.Add(ref destination, Vector256<int>.Count));
    }
}
