// <copyright file="BufferSpan.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Utility methods for <see cref="BufferSpan{T}"/>
    /// </summary>
    internal static class BufferSpan
    {
        /// <summary>
        /// It's worth to use Marshal.Copy() or Buffer.BlockCopy() over this size.
        /// </summary>
        private const int ByteCountThreshold = 1024;

        /// <summary>
        /// Copy 'count' number of elements of the same type from 'source' to 'dest'
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="source">The input <see cref="BufferSpan{T}"/></param>
        /// <param name="destination">The destination <see cref="BufferSpan{T}"/>.</param>
        /// <param name="count">The number of elements to copy</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Copy<T>(BufferSpan<T> source, BufferSpan<T> destination, int count)
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
        public static void Copy<T>(BufferSpan<T> source, BufferSpan<byte> destination, int countInSource)
            where T : struct
        {
            CopyImpl(source, destination, countInSource);
        }

        /// <summary>
        /// Copy 'countInDest' number of <typeparamref name="TDest"/> elements into 'dest' from a raw byte buffer defined by 'source'.
        /// </summary>
        /// <typeparam name="TDest">The element type.</typeparam>
        /// <param name="source">The raw source buffer to copy from"/></param>
        /// <param name="destination">The destination buffer"/></param>
        /// <param name="countInDest">The number of <typeparamref name="TDest"/> elements to copy.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void Copy<TDest>(BufferSpan<byte> source, BufferSpan<TDest> destination, int countInDest)
            where TDest : struct
        {
            // TODO: Refactor this method when Unsafe.CopyBlock(ref T, ref T) gets available!
            int byteCount = SizeOf<TDest>(countInDest);

            if (PerTypeValues<TDest>.IsPrimitiveType)
            {
                Buffer.BlockCopy(source.Array, source.ByteOffset, destination.Array, destination.ByteOffset, byteCount);
                return;
            }

            ref byte destRef = ref Unsafe.As<TDest, byte>(ref destination.DangerousGetPinnableReference());

            fixed (void* pinnedDest = &destRef)
            {
#if !NETSTANDARD1_1
                ref byte srcRef = ref source.DangerousGetPinnableReference();
                fixed (void* pinnedSrc = &srcRef)
                {
                    Buffer.MemoryCopy(pinnedSrc, pinnedDest, byteCount, byteCount);
                    return;
                }
#else
                if (byteCount > ByteCountThreshold)
                {
                    IntPtr ptr = (IntPtr)pinnedDest;
                    Marshal.Copy(source.Array, source.Start, ptr, byteCount);
                }

                ref byte srcRef = ref source.DangerousGetPinnableReference();

                fixed (void* pinnedSrc = &srcRef)
                {
                    Unsafe.CopyBlock(pinnedSrc, pinnedDest, (uint)byteCount);
                }
#endif
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
        private static unsafe void CopyImpl<T, TDest>(BufferSpan<T> source, BufferSpan<TDest> destination, int countInSource)
            where T : struct
            where TDest : struct
        {
            // TODO: Use Unsafe.CopyBlock(ref T, ref T) for small buffers when it gets available!
            int byteCount = SizeOf<T>(countInSource);

            if (PerTypeValues<T>.IsPrimitiveType && PerTypeValues<TDest>.IsPrimitiveType)
            {
                Buffer.BlockCopy(source.Array, source.ByteOffset, destination.Array, destination.ByteOffset, byteCount);
                return;
            }

            ref byte destRef = ref Unsafe.As<TDest, byte>(ref destination.DangerousGetPinnableReference());

            fixed (void* pinnedDest = &destRef)
            {
#if !NETSTANDARD1_1
                ref byte srcRef = ref Unsafe.As<T, byte>(ref source.DangerousGetPinnableReference());
                fixed (void* pinnedSrc = &srcRef)
                {
                    Buffer.MemoryCopy(pinnedSrc, pinnedDest, byteCount, byteCount);
                    return;
                }
#else
                if (byteCount > ByteCountThreshold)
                {
                    IntPtr ptr = (IntPtr)pinnedDest;
                    if (Unsafe.SizeOf<T>() == sizeof(long))
                    {
                        Marshal.Copy(Unsafe.As<long[]>(source.Array), source.Start, ptr, countInSource);
                        return;
                    }
                    else if (Unsafe.SizeOf<T>() == sizeof(int))
                    {
                        Marshal.Copy(Unsafe.As<int[]>(source.Array), source.Start, ptr, countInSource);
                        return;
                    }
                    else if (Unsafe.SizeOf<T>() == sizeof(short))
                    {
                        Marshal.Copy(Unsafe.As<short[]>(source.Array), source.Start, ptr, countInSource);
                        return;
                    }
                    else if (Unsafe.SizeOf<T>() == sizeof(byte))
                    {
                        Marshal.Copy(Unsafe.As<byte[]>(source.Array), source.Start, ptr, countInSource);
                        return;
                    }
                }

                ref byte srcRef = ref Unsafe.As<T, byte>(ref source.DangerousGetPinnableReference());

                fixed (void* pinnedSrc = &srcRef)
                {
                    Unsafe.CopyBlock(pinnedSrc, pinnedDest, (uint)byteCount);
                }
#endif
            }
        }

        /// <summary>
        /// Per-type static value cache for type 'T'
        /// </summary>
        /// <typeparam name="T">The type</typeparam>
        internal class PerTypeValues<T>
        {
            /// <summary>
            /// Gets a value indicating whether 'T' is a primitive type.
            /// </summary>
            public static readonly bool IsPrimitiveType =
                typeof(T) == typeof(byte) ||
                typeof(T) == typeof(char) ||
                typeof(T) == typeof(short) ||
                typeof(T) == typeof(ushort) ||
                typeof(T) == typeof(int) ||
                typeof(T) == typeof(uint) ||
                typeof(T) == typeof(float) ||
                typeof(T) == typeof(double) ||
                typeof(T) == typeof(long) ||
                typeof(T) == typeof(ulong) ||
                typeof(T) == typeof(IntPtr) ||
                typeof(T) == typeof(UIntPtr);
        }
    }
}