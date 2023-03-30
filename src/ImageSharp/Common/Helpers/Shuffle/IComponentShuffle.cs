// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static SixLabors.ImageSharp.SimdUtils;

// The JIT can detect and optimize rotation idioms ROTL (Rotate Left)
// and ROTR (Rotate Right) emitting efficient CPU instructions:
// https://github.com/dotnet/coreclr/pull/1830
namespace SixLabors.ImageSharp;

/// <summary>
/// Defines the contract for methods that allow the shuffling of pixel components.
/// Used for shuffling on platforms that do not support Hardware Intrinsics.
/// </summary>
internal interface IComponentShuffle
{
    /// <summary>
    /// Shuffles then slices 8-bit integers within 128-bit lanes in <paramref name="source"/>
    /// using the control and store the results in <paramref name="dest"/>.
    /// </summary>
    /// <param name="source">The source span of bytes.</param>
    /// <param name="dest">The destination span of bytes.</param>
    void ShuffleReduce(ref ReadOnlySpan<byte> source, ref Span<byte> dest);

    /// <summary>
    /// Shuffle 8-bit integers within 128-bit lanes in <paramref name="source"/>
    /// using the control and store the results in <paramref name="dest"/>.
    /// </summary>
    /// <param name="source">The source span of bytes.</param>
    /// <param name="dest">The destination span of bytes.</param>
    /// <remarks>
    /// Implementation can assume that source.Length is less or equal than dest.Length.
    /// Loops should iterate using source.Length.
    /// </remarks>
    void RunFallbackShuffle(ReadOnlySpan<byte> source, Span<byte> dest);
}

/// <inheritdoc/>
internal interface IShuffle4 : IComponentShuffle
{
}

internal readonly struct DefaultShuffle4 : IShuffle4
{
    public DefaultShuffle4(byte control)
        => this.Control = control;

    public byte Control { get; }

    [MethodImpl(InliningOptions.ShortMethod)]
    public void ShuffleReduce(ref ReadOnlySpan<byte> source, ref Span<byte> dest)
        => HwIntrinsics.Shuffle4Reduce(ref source, ref dest, this.Control);

    [MethodImpl(InliningOptions.ShortMethod)]
    public void RunFallbackShuffle(ReadOnlySpan<byte> source, Span<byte> dest)
    {
        ref byte sBase = ref MemoryMarshal.GetReference(source);
        ref byte dBase = ref MemoryMarshal.GetReference(dest);

        Shuffle.InverseMMShuffle(this.Control, out uint p3, out uint p2, out uint p1, out uint p0);

        for (nuint i = 0; i < (uint)source.Length; i += 4)
        {
            Unsafe.Add(ref dBase, i + 0) = Unsafe.Add(ref sBase, p0 + i);
            Unsafe.Add(ref dBase, i + 1) = Unsafe.Add(ref sBase, p1 + i);
            Unsafe.Add(ref dBase, i + 2) = Unsafe.Add(ref sBase, p2 + i);
            Unsafe.Add(ref dBase, i + 3) = Unsafe.Add(ref sBase, p3 + i);
        }
    }
}

internal readonly struct WXYZShuffle4 : IShuffle4
{
    [MethodImpl(InliningOptions.ShortMethod)]
    public void ShuffleReduce(ref ReadOnlySpan<byte> source, ref Span<byte> dest)
        => HwIntrinsics.Shuffle4Reduce(ref source, ref dest, Shuffle.MMShuffle2103);

    [MethodImpl(InliningOptions.ShortMethod)]
    public void RunFallbackShuffle(ReadOnlySpan<byte> source, Span<byte> dest)
    {
        ref uint sBase = ref Unsafe.As<byte, uint>(ref MemoryMarshal.GetReference(source));
        ref uint dBase = ref Unsafe.As<byte, uint>(ref MemoryMarshal.GetReference(dest));
        uint n = (uint)source.Length / 4;

        for (nuint i = 0; i < n; i++)
        {
            uint packed = Unsafe.Add(ref sBase, i);

            // packed          = [W Z Y X]
            // ROTL(8, packed) = [Z Y X W]
            Unsafe.Add(ref dBase, i) = (packed << 8) | (packed >> 24);
        }
    }
}

internal readonly struct WZYXShuffle4 : IShuffle4
{
    [MethodImpl(InliningOptions.ShortMethod)]
    public void ShuffleReduce(ref ReadOnlySpan<byte> source, ref Span<byte> dest)
        => HwIntrinsics.Shuffle4Reduce(ref source, ref dest, Shuffle.MMShuffle0123);

    [MethodImpl(InliningOptions.ShortMethod)]
    public void RunFallbackShuffle(ReadOnlySpan<byte> source, Span<byte> dest)
    {
        ref uint sBase = ref Unsafe.As<byte, uint>(ref MemoryMarshal.GetReference(source));
        ref uint dBase = ref Unsafe.As<byte, uint>(ref MemoryMarshal.GetReference(dest));
        uint n = (uint)source.Length / 4;

        for (nuint i = 0; i < n; i++)
        {
            uint packed = Unsafe.Add(ref sBase, i);

            // packed              = [W Z Y X]
            // REVERSE(packedArgb) = [X Y Z W]
            Unsafe.Add(ref dBase, i) = BinaryPrimitives.ReverseEndianness(packed);
        }
    }
}

internal readonly struct YZWXShuffle4 : IShuffle4
{
    [MethodImpl(InliningOptions.ShortMethod)]
    public void ShuffleReduce(ref ReadOnlySpan<byte> source, ref Span<byte> dest)
        => HwIntrinsics.Shuffle4Reduce(ref source, ref dest, Shuffle.MMShuffle0321);

    [MethodImpl(InliningOptions.ShortMethod)]
    public void RunFallbackShuffle(ReadOnlySpan<byte> source, Span<byte> dest)
    {
        ref uint sBase = ref Unsafe.As<byte, uint>(ref MemoryMarshal.GetReference(source));
        ref uint dBase = ref Unsafe.As<byte, uint>(ref MemoryMarshal.GetReference(dest));
        uint n = (uint)source.Length / 4;

        for (nuint i = 0; i < n; i++)
        {
            uint packed = Unsafe.Add(ref sBase, i);

            // packed              = [W Z Y X]
            // ROTR(8, packedArgb) = [Y Z W X]
            Unsafe.Add(ref dBase, i) = BitOperations.RotateRight(packed, 8);
        }
    }
}

internal readonly struct ZYXWShuffle4 : IShuffle4
{
    [MethodImpl(InliningOptions.ShortMethod)]
    public void ShuffleReduce(ref ReadOnlySpan<byte> source, ref Span<byte> dest)
        => HwIntrinsics.Shuffle4Reduce(ref source, ref dest, Shuffle.MMShuffle3012);

    [MethodImpl(InliningOptions.ShortMethod)]
    public void RunFallbackShuffle(ReadOnlySpan<byte> source, Span<byte> dest)
    {
        ref uint sBase = ref Unsafe.As<byte, uint>(ref MemoryMarshal.GetReference(source));
        ref uint dBase = ref Unsafe.As<byte, uint>(ref MemoryMarshal.GetReference(dest));
        uint n = (uint)source.Length / 4;

        for (nuint i = 0; i < n; i++)
        {
            uint packed = Unsafe.Add(ref sBase, i);

            // packed              = [W Z Y X]
            // tmp1                = [W 0 Y 0]
            // tmp2                = [0 Z 0 X]
            // tmp3=ROTL(16, tmp2) = [0 X 0 Z]
            // tmp1 + tmp3         = [W X Y Z]
            uint tmp1 = packed & 0xFF00FF00;
            uint tmp2 = packed & 0x00FF00FF;
            uint tmp3 = BitOperations.RotateLeft(tmp2, 16);

            Unsafe.Add(ref dBase, i) = tmp1 + tmp3;
        }
    }
}

internal readonly struct XWZYShuffle4 : IShuffle4
{
    [MethodImpl(InliningOptions.ShortMethod)]
    public void ShuffleReduce(ref ReadOnlySpan<byte> source, ref Span<byte> dest)
        => HwIntrinsics.Shuffle4Reduce(ref source, ref dest, Shuffle.MMShuffle1230);

    [MethodImpl(InliningOptions.ShortMethod)]
    public void RunFallbackShuffle(ReadOnlySpan<byte> source, Span<byte> dest)
    {
        ref uint sBase = ref Unsafe.As<byte, uint>(ref MemoryMarshal.GetReference(source));
        ref uint dBase = ref Unsafe.As<byte, uint>(ref MemoryMarshal.GetReference(dest));
        uint n = (uint)source.Length / 4;

        for (nuint i = 0; i < n; i++)
        {
            uint packed = Unsafe.Add(ref sBase, i);

            // packed              = [W Z Y X]
            // tmp1                = [0 Z 0 X]
            // tmp2                = [W 0 Y 0]
            // tmp3=ROTL(16, tmp2) = [Y 0 W 0]
            // tmp1 + tmp3         = [Y Z W X]
            uint tmp1 = packed & 0x00FF00FF;
            uint tmp2 = packed & 0xFF00FF00;
            uint tmp3 = BitOperations.RotateLeft(tmp2, 16);

            Unsafe.Add(ref dBase, i) = tmp1 + tmp3;
        }
    }
}
