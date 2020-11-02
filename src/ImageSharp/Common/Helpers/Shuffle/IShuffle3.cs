// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp
{
    /// <inheritdoc/>
    internal interface IShuffle3 : IComponentShuffle
    {
    }

    internal readonly struct DefaultShuffle3 : IShuffle3
    {
        private readonly byte p2;
        private readonly byte p1;
        private readonly byte p0;

        public DefaultShuffle3(byte p2, byte p1, byte p0)
        {
            Guard.MustBeBetweenOrEqualTo<byte>(p2, 0, 2, nameof(p2));
            Guard.MustBeBetweenOrEqualTo<byte>(p1, 0, 2, nameof(p1));
            Guard.MustBeBetweenOrEqualTo<byte>(p0, 0, 2, nameof(p0));

            this.p2 = p2;
            this.p1 = p1;
            this.p0 = p0;
            this.Control = SimdUtils.Shuffle.MmShuffle(3, p2, p1, p0);
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

            for (int i = 0; i < source.Length; i += 3)
            {
                Unsafe.Add(ref dBase, i) = Unsafe.Add(ref sBase, p0 + i);
                Unsafe.Add(ref dBase, i + 1) = Unsafe.Add(ref sBase, p1 + i);
                Unsafe.Add(ref dBase, i + 2) = Unsafe.Add(ref sBase, p2 + i);
            }
        }
    }

    internal readonly struct ZYXShuffle3 : IShuffle3
    {
        private static readonly byte ZYX = SimdUtils.Shuffle.MmShuffle(3, 0, 1, 2);

        public byte Control => ZYX;

        [MethodImpl(InliningOptions.ShortMethod)]
        public void RunFallbackShuffle(ReadOnlySpan<byte> source, Span<byte> dest)
        {
            ref Byte3 sBase = ref Unsafe.As<byte, Byte3>(ref MemoryMarshal.GetReference(source));
            ref Byte3 dBase = ref Unsafe.As<byte, Byte3>(ref MemoryMarshal.GetReference(dest));
            int n = source.Length / 3;

            for (int i = 0; i < n; i++)
            {
                uint packed = Unsafe.As<Byte3, uint>(ref Unsafe.Add(ref sBase, i));

                // packed              = [W Z Y X]
                // tmp1                = [W 0 Y 0]
                // tmp2                = [0 Z 0 X]
                // tmp3=ROTL(16, tmp2) = [0 X 0 Z]
                // tmp1 + tmp3         = [W X Y Z]
                uint tmp1 = packed & 0xFF00FF00;
                uint tmp2 = packed & 0x00FF00FF;
                uint tmp3 = (tmp2 << 16) | (tmp2 >> 16);
                packed = tmp1 + tmp3;

                Unsafe.Add(ref dBase, i) = Unsafe.As<uint, Byte3>(ref packed);
            }
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 3)]
    internal readonly struct Byte3
    {
    }
}
