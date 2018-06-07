// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Memory
{
    internal static class BufferExtensions
    {
        public static Memory<T> GetMemory<T>(this IBuffer<T> buffer)
            where T : struct
        {
            System.Buffers.MemoryManager<T> bufferManager = buffer as System.Buffers.MemoryManager<T>;

            if (bufferManager == null)
            {
                // TODO: We need a better way to integrate IBuffer<T> with MemoryManager<T>. The prior should probably entirely replace the latter!
                throw new ArgumentException(
                    "BufferExtensions.GetMemory<T>(buffer): buffer should be convertable to System.Buffers.MemoryManager<T>!",
                    nameof(buffer));
            }

            return bufferManager.Memory;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Length<T>(this IBuffer<T> buffer)
            where T : struct => buffer.GetSpan().Length;

        /// <summary>
        /// Gets a <see cref="Span{T}"/> to an offseted position inside the buffer.
        /// </summary>
        /// <param name="buffer">The buffer</param>
        /// <param name="start">The start</param>
        /// <returns>The <see cref="Span{T}"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> Slice<T>(this IBuffer<T> buffer, int start)
            where T : struct
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
        public static Span<T> Slice<T>(this IBuffer<T> buffer, int start, int length)
            where T : struct
        {
            return buffer.GetSpan().Slice(start, length);
        }

        /// <summary>
        /// Clears the contents of this buffer.
        /// </summary>
        /// <param name="buffer">The buffer</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Clear<T>(this IBuffer<T> buffer)
            where T : struct
        {
            buffer.GetSpan().Clear();
        }

        public static ref T DangerousGetPinnableReference<T>(this IBuffer<T> buffer)
            where T : struct =>
            ref MemoryMarshal.GetReference(buffer.GetSpan());

        public static void Read(this Stream stream, IManagedByteBuffer buffer)
        {
            stream.Read(buffer.Array, 0, buffer.Length());
        }

        public static void Write(this Stream stream, IManagedByteBuffer buffer)
        {
            stream.Write(buffer.Array, 0, buffer.Length());
        }
    }
}