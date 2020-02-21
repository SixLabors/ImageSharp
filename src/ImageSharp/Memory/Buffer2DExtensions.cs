// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Defines extension methods for <see cref="Buffer2D{T}"/>.
    /// </summary>
    public static class Buffer2DExtensions
    {
        /// <summary>
        /// Gets the backing <see cref="IMemoryGroup{T}"/>.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <typeparam name="T">The element type.</typeparam>
        /// <returns>The MemoryGroup.</returns>
        public static IMemoryGroup<T> GetMemoryGroup<T>(this Buffer2D<T> buffer)
            where T : struct
        {
            Guard.NotNull(buffer, nameof(buffer));
            return buffer.FastMemoryGroup.View;
        }

        /// <summary>
        /// Gets a <see cref="Span{T}"/> to the backing data of <paramref name="buffer"/>
        /// if the backing group consists of one single contiguous memory buffer.
        /// Throws <see cref="InvalidOperationException"/> otherwise.
        /// </summary>
        /// <param name="buffer">The <see cref="Buffer2D{T}"/>.</param>
        /// <typeparam name="T">The value type.</typeparam>
        /// <returns>The <see cref="Span{T}"/> referencing the memory area.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the backing group is discontiguous.
        /// </exception>
        internal static Span<T> GetSingleSpan<T>(this Buffer2D<T> buffer)
            where T : struct
        {
            Guard.NotNull(buffer, nameof(buffer));
            if (buffer.FastMemoryGroup.Count > 1)
            {
                throw new InvalidOperationException("GetSingleSpan is only valid for a single-buffer group!");
            }

            return buffer.FastMemoryGroup.Single().Span;
        }

        /// <summary>
        /// Gets a <see cref="Memory{T}"/> to the backing data of <paramref name="buffer"/>
        /// if the backing group consists of one single contiguous memory buffer.
        /// Throws <see cref="InvalidOperationException"/> otherwise.
        /// </summary>
        /// <param name="buffer">The <see cref="Buffer2D{T}"/>.</param>
        /// <typeparam name="T">The value type.</typeparam>
        /// <returns>The <see cref="Memory{T}"/>.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the backing group is discontiguous.
        /// </exception>
        internal static Memory<T> GetSingleMemory<T>(this Buffer2D<T> buffer)
            where T : struct
        {
            Guard.NotNull(buffer, nameof(buffer));
            if (buffer.FastMemoryGroup.Count > 1)
            {
                throw new InvalidOperationException("GetSingleMemory is only valid for a single-buffer group!");
            }

            return buffer.FastMemoryGroup.Single();
        }

        /// <summary>
        /// TODO: Does not work with multi-buffer groups, should be specific to Resize.
        /// Copy <paramref name="columnCount"/> columns of <paramref name="buffer"/> inplace,
        /// from positions starting at <paramref name="sourceIndex"/> to positions at <paramref name="destIndex"/>.
        /// </summary>
        internal static unsafe void CopyColumns<T>(
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

            Span<byte> span = MemoryMarshal.AsBytes(buffer.GetSingleMemory().Span);

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
        internal static Rectangle FullRectangle<T>(this Buffer2D<T> buffer)
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
        internal static BufferArea<T> GetArea<T>(this Buffer2D<T> buffer, in Rectangle rectangle)
            where T : struct =>
            new BufferArea<T>(buffer, rectangle);

        internal static BufferArea<T> GetArea<T>(this Buffer2D<T> buffer, int x, int y, int width, int height)
            where T : struct =>
            new BufferArea<T>(buffer, new Rectangle(x, y, width, height));

        /// <summary>
        /// Return a <see cref="BufferArea{T}"/> to the whole area of 'buffer'
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="buffer">The <see cref="Buffer2D{T}"/></param>
        /// <returns>The <see cref="BufferArea{T}"/></returns>
        internal static BufferArea<T> GetArea<T>(this Buffer2D<T> buffer)
            where T : struct =>
            new BufferArea<T>(buffer);

        /// <summary>
        /// Returns the size of the buffer.
        /// </summary>
        /// <typeparam name="T">The element type</typeparam>
        /// <param name="buffer">The <see cref="Buffer2D{T}"/></param>
        /// <returns>The <see cref="Size{T}"/> of the buffer</returns>
        internal static Size Size<T>(this Buffer2D<T> buffer)
            where T : struct
        {
            return new Size(buffer.Width, buffer.Height);
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
