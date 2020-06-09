// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Tests.Memory.Allocators
{
    internal static class BufferExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> GetSpan<T>(this IMemoryOwner<T> buffer)
            => buffer.Memory.Span;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Length<T>(this IMemoryOwner<T> buffer)
            => buffer.GetSpan().Length;

        public static ref T GetReference<T>(this IMemoryOwner<T> buffer)
            where T : struct =>
            ref MemoryMarshal.GetReference(buffer.GetSpan());
    }
}
