// <copyright file="BufferPointer.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System.Numerics;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Utility methods for <see cref="BufferPointer{T}"/>
    /// </summary>
    internal static class BufferPointer
    {
        /// <summary>
        /// It's worth to use Marshal.Copy() over this size.
        /// </summary>
        private const uint ByteCountThreshold = 1024u;

        /// <summary>
        /// Copy 'count' number of elements of the same type from 'source' to 'dest'
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="source">The input <see cref="BufferPointer{T}"/></param>
        /// <param name="destination">The destination <see cref="BufferPointer{T}"/>.</param>
        /// <param name="count">The number of elements to copy</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void Copy<T>(BufferPointer<T> source, BufferPointer<T> destination, int count)
            where T : struct
        {
            uint byteCount = USizeOf<T>(count);

            if (byteCount > ByteCountThreshold)
            {
                if (TryMarshalCopy(source, destination, count))
                {
                    return;
                }
            }

            Unsafe.CopyBlock((void*)destination.PointerAtOffset, (void*)source.PointerAtOffset, byteCount);
        }

        /// <summary>
        /// Copy 'countInSource' elements of <typeparamref name="T"/> from 'source' into the raw byte buffer 'destination'.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="source">The source buffer of <typeparamref name="T"/> elements to copy from.</param>
        /// <param name="destination">The destination buffer.</param>
        /// <param name="countInSource">The number of elements to copy from 'source'</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void Copy<T>(BufferPointer<T> source, BufferPointer<byte> destination, int countInSource)
            where T : struct
        {
            uint byteCount = USizeOf<T>(countInSource);

            if (byteCount > ByteCountThreshold)
            {
                if (TryMarshalCopy(source, destination, countInSource))
                {
                    return;
                }
            }

            Unsafe.CopyBlock((void*)destination.PointerAtOffset, (void*)source.PointerAtOffset, byteCount);
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
        private static bool TryMarshalCopy<TSource, TDest>(BufferPointer<TSource> source, BufferPointer<TDest> destination, int count)
            where TSource : struct
            where TDest : struct
        {
            // Pattern Based On:
            // https://github.com/dotnet/corefx/blob/master/src/System.Numerics.Vectors/src/System/Numerics/Vector.cs#L12
            //
            // Note: The following patterns are used throughout the code here and are described here
            //
            // PATTERN:
            //    if (typeof(T) == typeof(Int32)) { ... }
            //    else if (typeof(T) == typeof(Single)) { ... }
            // EXPLANATION:
            //    At runtime, each instantiation of BufferPointer<T> will be type-specific, and each of these typeof blocks will be eliminated,
            //    as typeof(T) is a (JIT) compile-time constant for each instantiation. This design was chosen to eliminate any overhead from
            //    delegates and other patterns.
            if (typeof(TSource) == typeof(long))
            {
                long[] srcArray = Unsafe.As<long[]>(source.Array);
                Marshal.Copy(srcArray, source.Offset, destination.PointerAtOffset, count);
                return true;
            }
            else if (typeof(TSource) == typeof(int))
            {
                int[] srcArray = Unsafe.As<int[]>(source.Array);
                Marshal.Copy(srcArray, source.Offset, destination.PointerAtOffset, count);
                return true;
            }
            else if (typeof(TSource) == typeof(uint))
            {
                int[] srcArray = Unsafe.As<int[]>(source.Array);
                Marshal.Copy(srcArray, source.Offset, destination.PointerAtOffset, count);
                return true;
            }
            else if (typeof(TSource) == typeof(short))
            {
                short[] srcArray = Unsafe.As<short[]>(source.Array);
                Marshal.Copy(srcArray, source.Offset, destination.PointerAtOffset, count);
                return true;
            }
            else if (typeof(TSource) == typeof(ushort))
            {
                short[] srcArray = Unsafe.As<short[]>(source.Array);
                Marshal.Copy(srcArray, source.Offset, destination.PointerAtOffset, count);
                return true;
            }
            else if (typeof(TSource) == typeof(byte))
            {
                byte[] srcArray = Unsafe.As<byte[]>(source.Array);
                Marshal.Copy(srcArray, source.Offset, destination.PointerAtOffset, count);
                return true;
            }

            return false;
        }
    }
}