// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp
{
    /// <inheritdoc/>
    internal interface IShuffle4Slice3 : IComponentShuffle
    {
    }

    internal readonly struct DefaultShuffle4Slice3 : IShuffle4Slice3
    {
        private readonly byte p2;
        private readonly byte p1;
        private readonly byte p0;

        public DefaultShuffle4Slice3(byte p3, byte p2, byte p1, byte p0)
        {
            DebugGuard.MustBeBetweenOrEqualTo<byte>(p3, 0, 3, nameof(p3));
            DebugGuard.MustBeBetweenOrEqualTo<byte>(p2, 0, 3, nameof(p2));
            DebugGuard.MustBeBetweenOrEqualTo<byte>(p1, 0, 3, nameof(p1));
            DebugGuard.MustBeBetweenOrEqualTo<byte>(p0, 0, 3, nameof(p0));

            this.p2 = p2;
            this.p1 = p1;
            this.p0 = p0;
            this.Control = SimdUtils.Shuffle.MmShuffle(p3, p2, p1, p0);
        }

        public byte Control { get; }

        [MethodImpl(InliningOptions.ShortMethod)]
        public void RunFallbackShuffle(ReadOnlySpan<byte> source, Span<byte> dest)
        {
            ref byte sBase = ref MemoryMarshal.GetReference(source);
            ref byte dBase = ref MemoryMarshal.GetReference(dest);

            int p2 = this.p2;
            int p1 = this.p1;
            int p0 = this.p0;

            for (int i = 0, j = 0; i < dest.Length; i += 3, j += 4)
            {
                Unsafe.Add(ref dBase, i) = Unsafe.Add(ref sBase, p0 + j);
                Unsafe.Add(ref dBase, i + 1) = Unsafe.Add(ref sBase, p1 + j);
                Unsafe.Add(ref dBase, i + 2) = Unsafe.Add(ref sBase, p2 + j);
            }
        }
    }

    internal readonly struct XYZWShuffle4Slice3 : IShuffle4Slice3
    {
        public byte Control
        {
            [MethodImpl(InliningOptions.ShortMethod)]
            get => SimdUtils.Shuffle.MmShuffle(3, 2, 1, 0);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public void RunFallbackShuffle(ReadOnlySpan<byte> source, Span<byte> dest)
        {
            ref uint sBase = ref Unsafe.As<byte, uint>(ref MemoryMarshal.GetReference(source));
            ref Byte3 dBase = ref Unsafe.As<byte, Byte3>(ref MemoryMarshal.GetReference(dest));

            int n = source.Length / 4;
            int m = Numerics.Modulo4(n);
            int u = n - m;

            ref uint sLoopEnd = ref Unsafe.Add(ref sBase, u);
            ref uint sEnd = ref Unsafe.Add(ref sBase, n);

            while (Unsafe.IsAddressLessThan(ref sBase, ref sLoopEnd))
            {
                Unsafe.Add(ref dBase, 0) = Unsafe.As<uint, Byte3>(ref Unsafe.Add(ref sBase, 0));
                Unsafe.Add(ref dBase, 1) = Unsafe.As<uint, Byte3>(ref Unsafe.Add(ref sBase, 1));
                Unsafe.Add(ref dBase, 2) = Unsafe.As<uint, Byte3>(ref Unsafe.Add(ref sBase, 2));
                Unsafe.Add(ref dBase, 3) = Unsafe.As<uint, Byte3>(ref Unsafe.Add(ref sBase, 3));

                sBase = ref Unsafe.Add(ref sBase, 4);
                dBase = ref Unsafe.Add(ref dBase, 4);
            }

            while (Unsafe.IsAddressLessThan(ref sBase, ref sEnd))
            {
                Unsafe.Add(ref dBase, 0) = Unsafe.As<uint, Byte3>(ref Unsafe.Add(ref sBase, 0));

                sBase = ref Unsafe.Add(ref sBase, 1);
                dBase = ref Unsafe.Add(ref dBase, 1);
            }
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 3)]
    internal readonly struct Byte3
    {
    }
}
