// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static SixLabors.ImageSharp.SimdUtils;

namespace SixLabors.ImageSharp;

/// <inheritdoc/>
internal interface IShuffle3 : IComponentShuffle
{
}

internal readonly struct DefaultShuffle3([ConstantExpected] byte control) : IShuffle3
{
    public byte Control { get; } = control;

    [MethodImpl(InliningOptions.ShortMethod)]
    public void ShuffleReduce(ref ReadOnlySpan<byte> source, ref Span<byte> destination)
#pragma warning disable CA1857 // A constant is expected for the parameter
        => HwIntrinsics.Shuffle3Reduce(ref source, ref destination, this.Control);
#pragma warning restore CA1857 // A constant is expected for the parameter

    [MethodImpl(InliningOptions.ShortMethod)]
    public void Shuffle(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        ref byte sBase = ref MemoryMarshal.GetReference(source);
        ref byte dBase = ref MemoryMarshal.GetReference(destination);

        SimdUtils.Shuffle.InverseMMShuffle(this.Control, out _, out uint p2, out uint p1, out uint p0);

        for (nuint i = 0; i < (uint)source.Length; i += 3)
        {
            Unsafe.Add(ref dBase, i + 0) = Unsafe.Add(ref sBase, p0 + i);
            Unsafe.Add(ref dBase, i + 1) = Unsafe.Add(ref sBase, p1 + i);
            Unsafe.Add(ref dBase, i + 2) = Unsafe.Add(ref sBase, p2 + i);
        }
    }
}
