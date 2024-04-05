// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static SixLabors.ImageSharp.SimdUtils;

namespace SixLabors.ImageSharp;

/// <inheritdoc/>
internal interface IPad3Shuffle4 : IComponentShuffle
{
}

internal readonly struct DefaultPad3Shuffle4([ConstantExpected] byte control) : IPad3Shuffle4
{
    public byte Control { get; } = control;

    [MethodImpl(InliningOptions.ShortMethod)]
    public void ShuffleReduce(ref ReadOnlySpan<byte> source, ref Span<byte> destination)
#pragma warning disable CA1857 // A constant is expected for the parameter
        => HwIntrinsics.Pad3Shuffle4Reduce(ref source, ref destination, this.Control);
#pragma warning restore CA1857 // A constant is expected for the parameter

    [MethodImpl(InliningOptions.ShortMethod)]
    public void Shuffle(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        ref byte sBase = ref MemoryMarshal.GetReference(source);
        ref byte dBase = ref MemoryMarshal.GetReference(destination);

        SimdUtils.Shuffle.InverseMMShuffle(this.Control, out uint p3, out uint p2, out uint p1, out uint p0);

        Span<byte> temp = stackalloc byte[4];
        ref byte t = ref MemoryMarshal.GetReference(temp);
        ref uint tu = ref Unsafe.As<byte, uint>(ref t);

        for (nuint i = 0, j = 0; i < (uint)source.Length; i += 3, j += 4)
        {
            ref byte s = ref Unsafe.Add(ref sBase, i);
            tu = Unsafe.As<byte, uint>(ref s) | 0xFF000000;

            Unsafe.Add(ref dBase, j + 0) = Unsafe.Add(ref t, p0);
            Unsafe.Add(ref dBase, j + 1) = Unsafe.Add(ref t, p1);
            Unsafe.Add(ref dBase, j + 2) = Unsafe.Add(ref t, p2);
            Unsafe.Add(ref dBase, j + 3) = Unsafe.Add(ref t, p3);
        }
    }
}

internal readonly struct XYZWPad3Shuffle4 : IPad3Shuffle4
{
    [MethodImpl(InliningOptions.ShortMethod)]
    public void ShuffleReduce(ref ReadOnlySpan<byte> source, ref Span<byte> destination)
        => HwIntrinsics.Pad3Shuffle4Reduce(ref source, ref destination, SimdUtils.Shuffle.MMShuffle3210);

    [MethodImpl(InliningOptions.ShortMethod)]
    public void Shuffle(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        ref byte sBase = ref MemoryMarshal.GetReference(source);
        ref byte dBase = ref MemoryMarshal.GetReference(destination);

        ref byte sEnd = ref Unsafe.Add(ref sBase, (uint)source.Length);
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
