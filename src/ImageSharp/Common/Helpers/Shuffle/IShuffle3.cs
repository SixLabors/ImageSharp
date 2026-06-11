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
            // The scalar remainder can run in-place after the vector body. Load
            // the full 3-byte pixel into a register-sized value before stores so
            // channel swaps cannot corrupt later reads from the same pixel.
            uint packed =
                Unsafe.Add(ref sBase, i + 0u) |
                ((uint)Unsafe.Add(ref sBase, i + 1u) << 8) |
                ((uint)Unsafe.Add(ref sBase, i + 2u) << 16);

            ref byte pBase = ref Unsafe.As<uint, byte>(ref packed);

            Unsafe.Add(ref dBase, i + 0u) = Unsafe.Add(ref pBase, p0);
            Unsafe.Add(ref dBase, i + 1u) = Unsafe.Add(ref pBase, p1);
            Unsafe.Add(ref dBase, i + 2u) = Unsafe.Add(ref pBase, p2);
        }
    }
}
