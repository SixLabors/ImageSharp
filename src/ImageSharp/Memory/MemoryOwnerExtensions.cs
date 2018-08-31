// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Extension methods for <see cref="IMemoryOwner{T}"/>
    /// </summary>
    internal static class MemoryOwnerExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> GetSpan<T>(this IMemoryOwner<T> buffer)
            => buffer.Memory.Span;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Length<T>(this IMemoryOwner<T> buffer)
            => buffer.GetSpan().Length;

        /// <summary>
        /// Gets a <see cref="Span{T}"/> to an offseted position inside the buffer.
        /// </summary>
        /// <param name="buffer">The buffer</param>
        /// <param name="start">The start</param>
        /// <returns>The <see cref="Span{T}"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> Slice<T>(this IMemoryOwner<T> buffer, int start)
        {
            return buffer.GetSpan().Slice(start);
        }

        /// <summary>
        /// Gets a <see cref="Span{T}"/> to an offsetted position inside the buffer.
        /// </summary>
        /// <param name="buffer">The buffer</param>
        /// <param name="start">The start</param>
        /// <param name="length">The length of the slice</param>
        /// <returns>The <see cref="Span{T}"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> Slice<T>(this IMemoryOwner<T> buffer, int start, int length)
        {
            return buffer.GetSpan().Slice(start, length);
        }

        /// <summary>
        /// Clears the contents of this buffer.
        /// </summary>
        /// <param name="buffer">The buffer</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Clear<T>(this IMemoryOwner<T> buffer)
        {
            buffer.GetSpan().Clear();
        }

        public static ref T GetReference<T>(this IMemoryOwner<T> buffer)
            where T : struct =>
            ref MemoryMarshal.GetReference(buffer.GetSpan());
    }
}