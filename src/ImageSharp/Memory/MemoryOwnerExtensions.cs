// Copyright (c) Six Labors.
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
        /// <summary>
        /// Gets a <see cref="Span{T}"/> from an <see cref="IMemoryOwner{T}"/> instance.
        /// </summary>
        /// <param name="buffer">The buffer</param>
        /// <returns>The <see cref="Span{T}"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> GetSpan<T>(this IMemoryOwner<T> buffer)
        {
            return buffer.Memory.Span;
        }

        /// <summary>
        /// Gets the length of an <see cref="IMemoryOwner{T}"/> internal buffer.
        /// </summary>
        /// <param name="buffer">The buffer</param>
        /// <returns>The length of the buffer</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Length<T>(this IMemoryOwner<T> buffer)
        {
            return buffer.Memory.Length;
        }

        /// <summary>
        /// Gets a <see cref="Span{T}"/> to an offsetted position inside the buffer.
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

        /// <summary>
        /// Gets a reference to the first item in the internal buffer for an <see cref="IMemoryOwner{T}"/> instance.
        /// </summary>
        /// <param name="buffer">The buffer</param>
        /// <returns>A reference to the first item within the memory wrapped by <paramref name="buffer"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T GetReference<T>(this IMemoryOwner<T> buffer)
            where T : struct
        {
            return ref MemoryMarshal.GetReference(buffer.GetSpan());
        }
    }
}
