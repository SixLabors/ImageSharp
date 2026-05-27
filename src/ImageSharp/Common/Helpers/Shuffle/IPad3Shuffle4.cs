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

        for (nuint i = 0, j = 0; i < (uint)source.Length; i += 3, j += 4)
        {
            // Expanding 3-byte pixels to 4 bytes can overwrite the next source
            // triplet when spans overlap. Assemble the padded pixel first, then
            // shuffle from the staged uint.
            uint packed =
                Unsafe.Add(ref sBase, i + 0u) |
                ((uint)Unsafe.Add(ref sBase, i + 1u) << 8) |
                ((uint)Unsafe.Add(ref sBase, i + 2u) << 16) |
                0xFF000000;

            ref byte pBase = ref Unsafe.As<uint, byte>(ref packed);

            Unsafe.Add(ref dBase, j + 0u) = Unsafe.Add(ref pBase, p0);
            Unsafe.Add(ref dBase, j + 1u) = Unsafe.Add(ref pBase, p1);
            Unsafe.Add(ref dBase, j + 2u) = Unsafe.Add(ref pBase, p2);
            Unsafe.Add(ref dBase, j + 3u) = Unsafe.Add(ref pBase, p3);
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
            // The fast scalar path reads one extra byte past the source triplet.
            // Keep that widened read in a local before writing the expanded pixel
            // so overlapping destinations cannot change what was read.
            uint packed = Unsafe.As<byte, uint>(ref sBase) | 0xFF000000;

            Unsafe.As<byte, uint>(ref dBase) = packed;

            sBase = ref Unsafe.Add(ref sBase, 3);
            dBase = ref Unsafe.Add(ref dBase, 4);
        }

        while (Unsafe.IsAddressLessThan(ref sBase, ref sEnd))
        {
            // The final triplet cannot use the widened read above, so assemble
            // the same padded uint byte-by-byte before the overlapping store.
            uint packed =
                Unsafe.Add(ref sBase, 0u) |
                ((uint)Unsafe.Add(ref sBase, 1u) << 8) |
                ((uint)Unsafe.Add(ref sBase, 2u) << 16) |
                0xFF000000;

            Unsafe.As<byte, uint>(ref dBase) = packed;

            sBase = ref Unsafe.Add(ref sBase, 3);
            dBase = ref Unsafe.Add(ref dBase, 4);
        }
    }
}
