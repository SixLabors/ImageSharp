// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// The JIT can detect and optimize rotation idioms ROTL (Rotate Left)
// and ROTR (Rotate Right) emitting efficient CPU instructions:
// https://github.com/dotnet/coreclr/pull/1830
namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Defines the contract for methods that allow the shuffling of pixel components.
    /// Used for shuffling on platforms that do not support Hardware Intrinsics.
    /// </summary>
    internal interface IComponentShuffle
    {
        /// <summary>
        /// Gets the shuffle control.
        /// </summary>
        byte Control { get; }

        /// <summary>
        /// Shuffle 8-bit integers within 128-bit lanes in <paramref name="source"/>
        /// using the control and store the results in <paramref name="dest"/>.
        /// </summary>
        /// <param name="source">The source span of bytes.</param>
        /// <param name="dest">The destination span of bytes.</param>
        void RunFallbackShuffle(ReadOnlySpan<byte> source, Span<byte> dest);
    }

    /// <inheritdoc/>
    internal interface IShuffle4 : IComponentShuffle
    {
    }

    internal readonly struct DefaultShuffle4 : IShuffle4
    {
        private readonly byte p3;
        private readonly byte p2;
        private readonly byte p1;
        private readonly byte p0;

        public DefaultShuffle4(byte p3, byte p2, byte p1, byte p0)
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

            for (int i = 0; i < source.Length; i += 4)
            {
                Unsafe.Add(ref dBase, i) = Unsafe.Add(ref sBase, p0 + i);
                Unsafe.Add(ref dBase, i + 1) = Unsafe.Add(ref sBase, p1 + i);
                Unsafe.Add(ref dBase, i + 2) = Unsafe.Add(ref sBase, p2 + i);
                Unsafe.Add(ref dBase, i + 3) = Unsafe.Add(ref sBase, p3 + i);
            }
        }
    }

    internal readonly struct WXYZShuffle4 : IShuffle4
    {
        public byte Control
        {
            [MethodImpl(InliningOptions.ShortMethod)]
            get => SimdUtils.Shuffle.MmShuffle(2, 1, 0, 3);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public void RunFallbackShuffle(ReadOnlySpan<byte> source, Span<byte> dest)
        {
            ref uint sBase = ref Unsafe.As<byte, uint>(ref MemoryMarshal.GetReference(source));
            ref uint dBase = ref Unsafe.As<byte, uint>(ref MemoryMarshal.GetReference(dest));
            int n = source.Length / 4;

            for (int i = 0; i < n; i++)
            {
                uint packed = Unsafe.Add(ref sBase, i);

                // packed          = [W Z Y X]
                // ROTL(8, packed) = [Z Y X W]
                Unsafe.Add(ref dBase, i) = (packed << 8) | (packed >> 24);
            }
        }
    }

    internal readonly struct WZYXShuffle4 : IShuffle4
    {
        public byte Control
        {
            [MethodImpl(InliningOptions.ShortMethod)]
            get => SimdUtils.Shuffle.MmShuffle(0, 1, 2, 3);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public void RunFallbackShuffle(ReadOnlySpan<byte> source, Span<byte> dest)
        {
            ref uint sBase = ref Unsafe.As<byte, uint>(ref MemoryMarshal.GetReference(source));
            ref uint dBase = ref Unsafe.As<byte, uint>(ref MemoryMarshal.GetReference(dest));
            int n = source.Length / 4;

            for (int i = 0; i < n; i++)
            {
                uint packed = Unsafe.Add(ref sBase, i);

                // packed              = [W Z Y X]
                // REVERSE(packedArgb) = [X Y Z W]
                Unsafe.Add(ref dBase, i) = BinaryPrimitives.ReverseEndianness(packed);
            }
        }
    }

    internal readonly struct YZWXShuffle4 : IShuffle4
    {
        public byte Control
        {
            [MethodImpl(InliningOptions.ShortMethod)]
            get => SimdUtils.Shuffle.MmShuffle(0, 3, 2, 1);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public void RunFallbackShuffle(ReadOnlySpan<byte> source, Span<byte> dest)
        {
            ref uint sBase = ref Unsafe.As<byte, uint>(ref MemoryMarshal.GetReference(source));
            ref uint dBase = ref Unsafe.As<byte, uint>(ref MemoryMarshal.GetReference(dest));
            int n = source.Length / 4;

            for (int i = 0; i < n; i++)
            {
                uint packed = Unsafe.Add(ref sBase, i);

                // packed              = [W Z Y X]
                // ROTR(8, packedArgb) = [Y Z W X]
                Unsafe.Add(ref dBase, i) = (packed >> 8) | (packed << 24);
            }
        }
    }

    internal readonly struct ZYXWShuffle4 : IShuffle4
    {
        public byte Control
        {
            [MethodImpl(InliningOptions.ShortMethod)]
            get => SimdUtils.Shuffle.MmShuffle(3, 0, 1, 2);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public void RunFallbackShuffle(ReadOnlySpan<byte> source, Span<byte> dest)
        {
            ref uint sBase = ref Unsafe.As<byte, uint>(ref MemoryMarshal.GetReference(source));
            ref uint dBase = ref Unsafe.As<byte, uint>(ref MemoryMarshal.GetReference(dest));
            int n = source.Length / 4;

            for (int i = 0; i < n; i++)
            {
                uint packed = Unsafe.Add(ref sBase, i);

                // packed              = [W Z Y X]
                // tmp1                = [W 0 Y 0]
                // tmp2                = [0 Z 0 X]
                // tmp3=ROTL(16, tmp2) = [0 X 0 Z]
                // tmp1 + tmp3         = [W X Y Z]
                uint tmp1 = packed & 0xFF00FF00;
                uint tmp2 = packed & 0x00FF00FF;
                uint tmp3 = (tmp2 << 16) | (tmp2 >> 16);

                Unsafe.Add(ref dBase, i) = tmp1 + tmp3;
            }
        }
    }
}
