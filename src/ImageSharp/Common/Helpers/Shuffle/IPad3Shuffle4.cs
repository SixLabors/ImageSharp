// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp
{
    /// <inheritdoc/>
    internal interface IPad3Shuffle4 : IComponentShuffle
    {
    }

    internal readonly struct DefaultPad3Shuffle4 : IPad3Shuffle4
    {
        private readonly byte p3;
        private readonly byte p2;
        private readonly byte p1;
        private readonly byte p0;

        public DefaultPad3Shuffle4(byte p3, byte p2, byte p1, byte p0)
        {
            DebugGuard.MustBeBetweenOrEqualTo<byte>(p3, 0, 3, nameof(p3));
            DebugGuard.MustBeBetweenOrEqualTo<byte>(p2, 0, 3, nameof(p2));
            DebugGuard.MustBeBetweenOrEqualTo<byte>(p1, 0, 3, nameof(p1));
            DebugGuard.MustBeBetweenOrEqualTo<byte>(p0, 0, 3, nameof(p0));

            this.p3 = p3;
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

            int p3 = this.p3;
            int p2 = this.p2;
            int p1 = this.p1;
            int p0 = this.p0;

            Span<byte> temp = stackalloc byte[4];
            ref byte t = ref MemoryMarshal.GetReference(temp);
            ref uint tu = ref Unsafe.As<byte, uint>(ref t);

            for (int i = 0, j = 0; i < source.Length; i += 3, j += 4)
            {
                ref var s = ref Unsafe.Add(ref sBase, i);
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
        public byte Control
        {
            [MethodImpl(InliningOptions.ShortMethod)]
            get => SimdUtils.Shuffle.MmShuffle(3, 2, 1, 0);
        }

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
}
