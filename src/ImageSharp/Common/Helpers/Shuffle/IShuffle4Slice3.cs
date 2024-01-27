// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static SixLabors.ImageSharp.SimdUtils;

namespace SixLabors.ImageSharp;

/// <inheritdoc/>
internal interface IShuffle4Slice3 : IComponentShuffle
{
}

internal readonly struct DefaultShuffle4Slice3 : IShuffle4Slice3
{
    public DefaultShuffle4Slice3(byte control)
        => this.Control = control;

    public byte Control { get; }

    [MethodImpl(InliningOptions.ShortMethod)]
    public void ShuffleReduce(ref ReadOnlySpan<byte> source, ref Span<byte> dest)
        => HwIntrinsics.Shuffle4Slice3Reduce(ref source, ref dest, this.Control);

    [MethodImpl(InliningOptions.ShortMethod)]
    public void RunFallbackShuffle(ReadOnlySpan<byte> source, Span<byte> dest)
    {
        ref byte sBase = ref MemoryMarshal.GetReference(source);
        ref byte dBase = ref MemoryMarshal.GetReference(dest);

        Shuffle.InverseMMShuffle(this.Control, out _, out uint p2, out uint p1, out uint p0);

        for (nuint i = 0, j = 0; i < (uint)dest.Length; i += 3, j += 4)
        {
            Extensions.UnsafeAdd(ref dBase, i + 0) = Extensions.UnsafeAdd(ref sBase, p0 + j);
            Extensions.UnsafeAdd(ref dBase, i + 1) = Extensions.UnsafeAdd(ref sBase, p1 + j);
            Extensions.UnsafeAdd(ref dBase, i + 2) = Extensions.UnsafeAdd(ref sBase, p2 + j);
        }
    }
}

internal readonly struct XYZWShuffle4Slice3 : IShuffle4Slice3
{
    [MethodImpl(InliningOptions.ShortMethod)]
    public void ShuffleReduce(ref ReadOnlySpan<byte> source, ref Span<byte> dest)
        => HwIntrinsics.Shuffle4Slice3Reduce(ref source, ref dest, Shuffle.MMShuffle3210);

    [MethodImpl(InliningOptions.ShortMethod)]
    public void RunFallbackShuffle(ReadOnlySpan<byte> source, Span<byte> dest)
    {
        ref uint sBase = ref Unsafe.As<byte, uint>(ref MemoryMarshal.GetReference(source));
        ref Byte3 dBase = ref Unsafe.As<byte, Byte3>(ref MemoryMarshal.GetReference(dest));

        nint n = (nint)(uint)source.Length / 4;
        nint m = Numerics.Modulo4(n);
        nint u = n - m;

        ref uint sLoopEnd = ref Extensions.UnsafeAdd(ref sBase, u);
        ref uint sEnd = ref Extensions.UnsafeAdd(ref sBase, n);

        while (Unsafe.IsAddressLessThan(ref sBase, ref sLoopEnd))
        {
            Extensions.UnsafeAdd(ref dBase, 0) = Unsafe.As<uint, Byte3>(ref Extensions.UnsafeAdd(ref sBase, 0));
            Extensions.UnsafeAdd(ref dBase, 1) = Unsafe.As<uint, Byte3>(ref Extensions.UnsafeAdd(ref sBase, 1));
            Extensions.UnsafeAdd(ref dBase, 2) = Unsafe.As<uint, Byte3>(ref Extensions.UnsafeAdd(ref sBase, 2));
            Extensions.UnsafeAdd(ref dBase, 3) = Unsafe.As<uint, Byte3>(ref Extensions.UnsafeAdd(ref sBase, 3));

            sBase = ref Extensions.UnsafeAdd(ref sBase, 4);
            dBase = ref Extensions.UnsafeAdd(ref dBase, 4);
        }

        while (Unsafe.IsAddressLessThan(ref sBase, ref sEnd))
        {
            Extensions.UnsafeAdd(ref dBase, 0) = Unsafe.As<uint, Byte3>(ref Extensions.UnsafeAdd(ref sBase, 0));

            sBase = ref Extensions.UnsafeAdd(ref sBase, 1);
            dBase = ref Extensions.UnsafeAdd(ref dBase, 1);
        }
    }
}

[StructLayout(LayoutKind.Explicit, Size = 3)]
internal readonly struct Byte3
{
}
