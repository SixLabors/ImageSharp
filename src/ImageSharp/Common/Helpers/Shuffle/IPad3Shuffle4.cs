// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static SixLabors.ImageSharp.SimdUtils;

namespace SixLabors.ImageSharp;

/// <inheritdoc/>
internal interface IPad3Shuffle4 : IComponentShuffle
{
}

internal readonly struct DefaultPad3Shuffle4 : IPad3Shuffle4
{
    public DefaultPad3Shuffle4(byte control)
        => this.Control = control;

    public byte Control { get; }

    [MethodImpl(InliningOptions.ShortMethod)]
    public void ShuffleReduce(ref ReadOnlySpan<byte> source, ref Span<byte> dest)
        => HwIntrinsics.Pad3Shuffle4Reduce(ref source, ref dest, this.Control);

    [MethodImpl(InliningOptions.ShortMethod)]
    public void RunFallbackShuffle(ReadOnlySpan<byte> source, Span<byte> dest)
    {
        ref byte sBase = ref MemoryMarshal.GetReference(source);
        ref byte dBase = ref MemoryMarshal.GetReference(dest);

        Shuffle.InverseMMShuffle(this.Control, out int p3, out int p2, out int p1, out int p0);

        Span<byte> temp = stackalloc byte[4];
        ref byte t = ref MemoryMarshal.GetReference(temp);
        ref uint tu = ref Unsafe.As<byte, uint>(ref t);

        for (int i = 0, j = 0; i < source.Length; i += 3, j += 4)
        {
            ref byte s = ref Unsafe.Add(ref sBase, i);
            tu = Unsafe.As<byte, uint>(ref s) | 0xFF000000;

            Unsafe.Add(ref dBase, j) = Unsafe.Add(ref t, p0);
            Unsafe.Add(ref dBase, j + 1) = Unsafe.Add(ref t, p1);
            Unsafe.Add(ref dBase, j + 2) = Unsafe.Add(ref t, p2);
            Unsafe.Add(ref dBase, j + 3) = Unsafe.Add(ref t, p3);
        }
    }
}

internal readonly struct XYZWPad3Shuffle4 : IPad3Shuffle4
{
    [MethodImpl(InliningOptions.ShortMethod)]
    public void ShuffleReduce(ref ReadOnlySpan<byte> source, ref Span<byte> dest)
        => HwIntrinsics.Pad3Shuffle4Reduce(ref source, ref dest, Shuffle.MMShuffle3210);

    [MethodImpl(InliningOptions.ShortMethod)]
    public void RunFallbackShuffle(ReadOnlySpan<byte> source, Span<byte> dest)
    {
        ref byte sBase = ref MemoryMarshal.GetReference(source);
        ref byte dBase = ref MemoryMarshal.GetReference(dest);

        ref byte sEnd = ref Unsafe.Add(ref sBase, source.Length);
        ref byte sLoopEnd = ref Unsafe.Subtract(ref sEnd, 4);

        while (Unsafe.IsAddressLessThan(ref sBase, ref sLoopEnd))
        {
            Unsafe.As<byte, uint>(ref dBase) = Unsafe.As<byte, uint>(ref sBase) | 0xFF000000;

            sBase = ref Unsafe.Add(ref sBase, 3);
            dBase = ref Unsafe.Add(ref dBase, 4);
        }

        while (Unsafe.IsAddressLessThan(ref sBase, ref sEnd))
        {
            Unsafe.Add(ref dBase, 0) = Unsafe.Add(ref sBase, 0);
            Unsafe.Add(ref dBase, 1) = Unsafe.Add(ref sBase, 1);
            Unsafe.Add(ref dBase, 2) = Unsafe.Add(ref sBase, 2);
            Unsafe.Add(ref dBase, 3) = byte.MaxValue;

            sBase = ref Unsafe.Add(ref sBase, 3);
            dBase = ref Unsafe.Add(ref dBase, 4);
        }
    }
}
