// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static SixLabors.ImageSharp.SimdUtils;

namespace SixLabors.ImageSharp;

/// <inheritdoc/>
internal interface IShuffle4 : IComponentShuffle
{
}

internal readonly struct DefaultShuffle4([ConstantExpected] byte control) : IShuffle4
{
    public byte Control { get; } = control;

    [MethodImpl(InliningOptions.ShortMethod)]
    public void ShuffleReduce(ref ReadOnlySpan<byte> source, ref Span<byte> destination)
#pragma warning disable CA1857 // A constant is expected for the parameter
        => HwIntrinsics.Shuffle4Reduce(ref source, ref destination, this.Control);
#pragma warning restore CA1857 // A constant is expected for the parameter

    [MethodImpl(InliningOptions.ShortMethod)]
    public void Shuffle(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        ref byte sBase = ref MemoryMarshal.GetReference(source);
        ref byte dBase = ref MemoryMarshal.GetReference(destination);

        SimdUtils.Shuffle.InverseMMShuffle(this.Control, out uint p3, out uint p2, out uint p1, out uint p0);

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
    public void ShuffleReduce(ref ReadOnlySpan<byte> source, ref Span<byte> destination)
        => HwIntrinsics.Shuffle4Reduce(ref source, ref destination, SimdUtils.Shuffle.MMShuffle2103);

    [MethodImpl(InliningOptions.ShortMethod)]
    public void Shuffle(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        ref uint sBase = ref Unsafe.As<byte, uint>(ref MemoryMarshal.GetReference(source));
        ref uint dBase = ref Unsafe.As<byte, uint>(ref MemoryMarshal.GetReference(destination));
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
    public void ShuffleReduce(ref ReadOnlySpan<byte> source, ref Span<byte> destination)
        => HwIntrinsics.Shuffle4Reduce(ref source, ref destination, SimdUtils.Shuffle.MMShuffle0123);

    [MethodImpl(InliningOptions.ShortMethod)]
    public void Shuffle(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        ref uint sBase = ref Unsafe.As<byte, uint>(ref MemoryMarshal.GetReference(source));
        ref uint dBase = ref Unsafe.As<byte, uint>(ref MemoryMarshal.GetReference(destination));
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
    public void ShuffleReduce(ref ReadOnlySpan<byte> source, ref Span<byte> destination)
        => HwIntrinsics.Shuffle4Reduce(ref source, ref destination, SimdUtils.Shuffle.MMShuffle0321);

    [MethodImpl(InliningOptions.ShortMethod)]
    public void Shuffle(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        ref uint sBase = ref Unsafe.As<byte, uint>(ref MemoryMarshal.GetReference(source));
        ref uint dBase = ref Unsafe.As<byte, uint>(ref MemoryMarshal.GetReference(destination));
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
    public void ShuffleReduce(ref ReadOnlySpan<byte> source, ref Span<byte> destination)
        => HwIntrinsics.Shuffle4Reduce(ref source, ref destination, SimdUtils.Shuffle.MMShuffle3012);

    [MethodImpl(InliningOptions.ShortMethod)]
    public void Shuffle(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        ref uint sBase = ref Unsafe.As<byte, uint>(ref MemoryMarshal.GetReference(source));
        ref uint dBase = ref Unsafe.As<byte, uint>(ref MemoryMarshal.GetReference(destination));
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
    public void ShuffleReduce(ref ReadOnlySpan<byte> source, ref Span<byte> destination)
        => HwIntrinsics.Shuffle4Reduce(ref source, ref destination, SimdUtils.Shuffle.MMShuffle1230);

    [MethodImpl(InliningOptions.ShortMethod)]
    public void Shuffle(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        ref uint sBase = ref Unsafe.As<byte, uint>(ref MemoryMarshal.GetReference(source));
        ref uint dBase = ref Unsafe.As<byte, uint>(ref MemoryMarshal.GetReference(destination));
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
