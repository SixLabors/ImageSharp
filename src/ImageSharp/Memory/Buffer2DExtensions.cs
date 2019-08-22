// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Defines extension methods for <see cref="Buffer2D{T}"/>.
    /// </summary>
    internal static class Buffer2DExtensions
    {
        /// <summary>
        /// Copy <paramref name="columnCount"/> columns of <paramref name="buffer"/> inplace,
        /// from positions starting at <paramref name="sourceIndex"/> to positions at <paramref name="destIndex"/>.
        /// </summary>
        public static unsafe void CopyColumns<T>(
            this Buffer2D<T> buffer,
            int sourceIndex,
            int destIndex,
            int columnCount)
            where T : struct
        {
            DebugGuard.NotNull(buffer, nameof(buffer));
            DebugGuard.MustBeGreaterThanOrEqualTo(sourceIndex, 0, nameof(sourceIndex));
            DebugGuard.MustBeGreaterThanOrEqualTo(destIndex, 0, nameof(sourceIndex));
            CheckColumnRegionsDoNotOverlap(buffer, sourceIndex, destIndex, columnCount);

            int elementSize = Unsafe.SizeOf<T>();
            int width = buffer.Width * elementSize;
            int sOffset = sourceIndex * elementSize;
            int dOffset = destIndex * elementSize;
            long count = columnCount * elementSize;

            Span<byte> span = MemoryMarshal.AsBytes(buffer.Memory.Span);

            fixed (byte* ptr = span)
            {
                byte* basePtr = ptr;
                for (int y = 0; y < buffer.Height; y++)
                {
                    byte* sPtr = basePtr + sOffset;
                    byte* dPtr = basePtr + dOffset;

                    Buffer.MemoryCopy(sPtr, dPtr, count, count);

                    basePtr += width;
                }
            }
        }

        /// <summary>
        /// Returns a <see cref="Rectangle"/> representing the full area of the buffer.
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="buffer">The <see cref="Buffer2D{T}"/></param>
        /// <returns>The <see cref="Rectangle"/></returns>
        public static Rectangle FullRectangle<T>(this Buffer2D<T> buffer)
            where T : struct
        {
            return new Rectangle(0, 0, buffer.Width, buffer.Height);
        }

        /// <summary>
        /// Return a <see cref="BufferArea{T}"/> to the subarea represented by 'rectangle'
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="buffer">The <see cref="Buffer2D{T}"/></param>
        /// <param name="rectangle">The rectangle subarea</param>
        /// <returns>The <see cref="BufferArea{T}"/></returns>
        public static BufferArea<T> GetArea<T>(this Buffer2D<T> buffer, in Rectangle rectangle)
            where T : struct =>
            new BufferArea<T>(buffer, rectangle);

        public static BufferArea<T> GetArea<T>(this Buffer2D<T> buffer, int x, int y, int width, int height)
            where T : struct =>
            new BufferArea<T>(buffer, new Rectangle(x, y, width, height));

        /// <summary>
        /// Return a <see cref="BufferArea{T}"/> to the whole area of 'buffer'
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="buffer">The <see cref="Buffer2D{T}"/></param>
        /// <returns>The <see cref="BufferArea{T}"/></returns>
        public static BufferArea<T> GetArea<T>(this Buffer2D<T> buffer)
            where T : struct =>
            new BufferArea<T>(buffer);

        public static BufferArea<T> GetAreaBetweenRows<T>(this Buffer2D<T> buffer, int minY, int maxY)
            where T : struct =>
            new BufferArea<T>(buffer, new Rectangle(0, minY, buffer.Width, maxY - minY));

        /// <summary>
        /// Gets a span for all the pixels in <paramref name="buffer"/> defined by <paramref name="rows"/>
        /// </summary>
        public static Span<T> GetMultiRowSpan<T>(this Buffer2D<T> buffer, in RowInterval rows)
            where T : struct
        {
            return buffer.Span.Slice(rows.Min * buffer.Width, rows.Height * buffer.Width);
        }

        /// <summary>
        /// Gets a <see cref="Memory{T}"/> to the row 'y' beginning from the pixel at the first pixel on that row.
        /// </summary>
        /// <param name="buffer">The buffer</param>
        /// <param name="y">The y (row) coordinate</param>
        /// <typeparam name="T">The element type</typeparam>
        /// <returns>The <see cref="Span{T}"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Memory<T> GetRowMemory<T>(this Buffer2D<T> buffer, int y)
            where T : struct
        {
            return buffer.MemorySource.Memory.Slice(y * buffer.Width, buffer.Width);
        }

        /// <summary>
        /// Gets a <see cref="Span{T}"/> to the row 'y' beginning from the pixel at 'x'.
        /// </summary>
        /// <param name="buffer">The buffer</param>
        /// <param name="x">The x coordinate (position in the row)</param>
        /// <param name="y">The y (row) coordinate</param>
        /// <typeparam name="T">The element type</typeparam>
        /// <returns>The <see cref="Span{T}"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> GetRowSpan<T>(this Buffer2D<T> buffer, int x, int y)
            where T : struct
        {
            return buffer.GetSpan().Slice((y * buffer.Width) + x, buffer.Width - x);
        }

        /// <summary>
        /// Gets a <see cref="Span{T}"/> to the row 'y' beginning from the pixel at the first pixel on that row.
        /// </summary>
        /// <param name="buffer">The buffer</param>
        /// <param name="y">The y (row) coordinate</param>
        /// <typeparam name="T">The element type</typeparam>
        /// <returns>The <see cref="Span{T}"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> GetRowSpan<T>(this Buffer2D<T> buffer, int y)
            where T : struct
        {
            return buffer.GetSpan().Slice(y * buffer.Width, buffer.Width);
        }

        /// <summary>
        /// Returns the size of the buffer.
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="buffer">The <see cref="Buffer2D{T}"/></param>
        /// <returns>The <see cref="Size{T}"/> of the buffer</returns>
        public static Size Size<T>(this Buffer2D<T> buffer)
            where T : struct
        {
            return new Size(buffer.Width, buffer.Height);
        }

        /// <summary>
        /// Gets a <see cref="Span{T}"/> to the backing buffer of <paramref name="buffer"/>.
        /// </summary>
        internal static Span<T> GetSpan<T>(this Buffer2D<T> buffer)
            where T : struct
        {
            return buffer.MemorySource.GetSpan();
        }

        [Conditional("DEBUG")]
        private static void CheckColumnRegionsDoNotOverlap<T>(
            Buffer2D<T> buffer,
            int sourceIndex,
            int destIndex,
            int columnCount)
            where T : struct
        {
            int minIndex = Math.Min(sourceIndex, destIndex);
            int maxIndex = Math.Max(sourceIndex, destIndex);
            if (maxIndex < minIndex + columnCount || maxIndex > buffer.Width - columnCount)
            {
                throw new InvalidOperationException("Column regions should not overlap!");
            }
        }
    }
}
