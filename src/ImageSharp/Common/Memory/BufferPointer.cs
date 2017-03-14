// <copyright file="BufferPointer.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Numerics;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Utility methods for <see cref="BufferPointer{T}"/>
    /// </summary>
    internal static class BufferPointer
    {
        /// <summary>
        /// It's worth to use Marshal.Copy() or Buffer.BlockCopy() over this size.
        /// </summary>
        private const int ByteCountThreshold = 1024;

        /// <summary>
        /// Copy 'count' number of elements of the same type from 'source' to 'dest'
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="source">The input <see cref="BufferPointer{T}"/></param>
        /// <param name="destination">The destination <see cref="BufferPointer{T}"/>.</param>
        /// <param name="count">The number of elements to copy</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Copy<T>(BufferPointer<T> source, BufferPointer<T> destination, int count)
            where T : struct
        {
            CopyImpl(source, destination, count);
        }

        /// <summary>
        /// Copy 'countInSource' elements of <typeparamref name="T"/> from 'source' into the raw byte buffer 'destination'.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="source">The source buffer of <typeparamref name="T"/> elements to copy from.</param>
        /// <param name="destination">The destination buffer.</param>
        /// <param name="countInSource">The number of elements to copy from 'source'</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Copy<T>(BufferPointer<T> source, BufferPointer<byte> destination, int countInSource)
            where T : struct
        {
            CopyImpl(source, destination, countInSource);
        }

        /// <summary>
        /// Copy 'countInDest' number of <typeparamref name="T"/> elements into 'dest' from a raw byte buffer defined by 'source'.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="source">The raw source buffer to copy from"/></param>
        /// <param name="destination">The destination buffer"/></param>
        /// <param name="countInDest">The number of <typeparamref name="T"/> elements to copy.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void Copy<T>(BufferPointer<byte> source, BufferPointer<T> destination, int countInDest)
            where T : struct
        {
            int byteCount = SizeOf<T>(countInDest);

            if (byteCount > (int)ByteCountThreshold)
            {
                Marshal.Copy(source.Array, source.Offset, destination.PointerAtOffset, byteCount);
            }
            else
            {
                Unsafe.CopyBlock((void*)destination.PointerAtOffset, (void*)source.PointerAtOffset, (uint)byteCount);
            }
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void CopyImpl<T, TDest>(BufferPointer<T> source, BufferPointer<TDest> destination, int count)
            where T : struct
            where TDest : struct
        {
            int byteCount = SizeOf<T>(count);

            if (byteCount > ByteCountThreshold)
            {
                if (Unsafe.SizeOf<T>() == sizeof(long))
                {
                    Marshal.Copy(Unsafe.As<long[]>(source.Array), source.Offset, destination.PointerAtOffset, count);
                    return;
                }
                else if (Unsafe.SizeOf<T>() == sizeof(int))
                {
                    Marshal.Copy(Unsafe.As<int[]>(source.Array), source.Offset, destination.PointerAtOffset, count);
                    return;
                }
                else if (Unsafe.SizeOf<T>() == sizeof(short))
                {
                    Marshal.Copy(Unsafe.As<short[]>(source.Array), source.Offset, destination.PointerAtOffset, count);
                    return;
                }
                else if (Unsafe.SizeOf<T>() == sizeof(byte))
                {
                    Marshal.Copy(Unsafe.As<byte[]>(source.Array), source.Offset, destination.PointerAtOffset, count);
                    return;
                }
            }

            Unsafe.CopyBlock((void*)destination.PointerAtOffset, (void*)source.PointerAtOffset, (uint)byteCount);
        }
    }
}