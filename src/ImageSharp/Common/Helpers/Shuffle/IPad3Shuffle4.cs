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

        Shuffle.InverseMMShuffle(this.Control, out uint p3, out uint p2, out uint p1, out uint p0);

        Span<byte> temp = stackalloc byte[4];
        ref byte t = ref MemoryMarshal.GetReference(temp);
        ref uint tu = ref Unsafe.As<byte, uint>(ref t);

        for (nuint i = 0, j = 0; i < (uint)source.Length; i += 3, j += 4)
        {
            ref byte s = ref Extensions.UnsafeAdd(ref sBase, i);
            tu = Unsafe.As<byte, uint>(ref s) | 0xFF000000;

            Extensions.UnsafeAdd(ref dBase, j + 0) = Extensions.UnsafeAdd(ref t, p0);
            Extensions.UnsafeAdd(ref dBase, j + 1) = Extensions.UnsafeAdd(ref t, p1);
            Extensions.UnsafeAdd(ref dBase, j + 2) = Extensions.UnsafeAdd(ref t, p2);
            Extensions.UnsafeAdd(ref dBase, j + 3) = Extensions.UnsafeAdd(ref t, p3);
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

        ref byte sEnd = ref Extensions.UnsafeAdd(ref sBase, (uint)source.Length);
        ref byte sLoopEnd = ref Unsafe.Subtract(ref sEnd, 4);

        while (Unsafe.IsAddressLessThan(ref sBase, ref sLoopEnd))
        {
            Unsafe.As<byte, uint>(ref dBase) = Unsafe.As<byte, uint>(ref sBase) | 0xFF000000;

            sBase = ref Extensions.UnsafeAdd(ref sBase, 3);
            dBase = ref Extensions.UnsafeAdd(ref dBase, 4);
        }

        while (Unsafe.IsAddressLessThan(ref sBase, ref sEnd))
        {
            Extensions.UnsafeAdd(ref dBase, 0) = Extensions.UnsafeAdd(ref sBase, 0);
            Extensions.UnsafeAdd(ref dBase, 1) = Extensions.UnsafeAdd(ref sBase, 1);
            Extensions.UnsafeAdd(ref dBase, 2) = Extensions.UnsafeAdd(ref sBase, 2);
            Extensions.UnsafeAdd(ref dBase, 3) = byte.MaxValue;

            sBase = ref Extensions.UnsafeAdd(ref sBase, 3);
            dBase = ref Extensions.UnsafeAdd(ref dBase, 4);
        }
    }
}
