// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Utility methods for <see cref="Span{T}"/>
    /// </summary>
    internal static class SpanHelper
    {
        /// <summary>
        /// Fetches a <see cref="Vector{T}"/> from the beginning of the span.
        /// </summary>
        /// <typeparam name="T">The value type</typeparam>
        /// <param name="span">The span to fetch the vector from</param>
        /// <returns>A <see cref="Vector{T}"/> reference to the beginning of the span</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref Vector<T> FetchVector<T>(this Span<T> span)
            where T : struct
        {
            return ref Unsafe.As<T, Vector<T>>(ref MemoryMarshal.GetReference(span));
        }

        /// <summary>
        /// Copy 'count' number of elements of the same type from 'source' to 'dest'
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="source">The <see cref="Span{T}"/> to copy elements from.</param>
        /// <param name="destination">The destination <see cref="Span{T}"/>.</param>
        /// <param name="count">The number of elements to copy</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void Copy<T>(ReadOnlySpan<T> source, Span<T> destination, int count)
            where T : struct
        {
            source.Slice(0, count).CopyTo(destination);
        }

        /// <summary>
        /// Copy all elements of 'source' into 'destination'.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="source">The <see cref="Span{T}"/> to copy elements from.</param>
        /// <param name="destination">The destination <see cref="Span{T}"/>.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Copy<T>(ReadOnlySpan<T> source, Span<T> destination)
            where T : struct
        {
            Copy(source, destination, Math.Min(source.Length, destination.Length));
        }

        /// <summary>
        /// Gets the size of `count` elements in bytes.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="count">The count of the elements</param>
        /// <returns>The size in bytes as int</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SizeOf<T>(int count)
            where T : struct => Unsafe.SizeOf<T>() * count;

        /// <summary>
        /// Gets the size of `count` elements in bytes as UInt32
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="count">The count of the elements</param>
        /// <returns>The size in bytes as UInt32</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint USizeOf<T>(int count)
            where T : struct
            => (uint)SizeOf<T>(count);
    }
}