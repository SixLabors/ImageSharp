// <copyright file="Buffer2DExtensions{T}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Memory
{
    using System;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Defines extension methods for <see cref="IBuffer2D{T}"/>.
    /// </summary>
    internal static class Buffer2DExtensions
    {
        /// <summary>
        /// Gets a <see cref="Span{T}"/> to the row 'y' beginning from the pixel at 'x'.
        /// </summary>
        /// <param name="buffer">The buffer</param>
        /// <param name="x">The x coordinate (position in the row)</param>
        /// <param name="y">The y (row) coordinate</param>
        /// <typeparam name="T">The element type</typeparam>
        /// <returns>The <see cref="Span{T}"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> GetRowSpan<T>(this IBuffer2D<T> buffer, int x, int y)
            where T : struct
        {
            return buffer.Span.Slice((y * buffer.Width) + x, buffer.Width - x);
        }

        /// <summary>
        /// Gets a <see cref="Span{T}"/> to the row 'y' beginning from the pixel at the first pixel on that row.
        /// </summary>
        /// <param name="buffer">The buffer</param>
        /// <param name="y">The y (row) coordinate</param>
        /// <typeparam name="T">The element type</typeparam>
        /// <returns>The <see cref="Span{T}"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> GetRowSpan<T>(this IBuffer2D<T> buffer, int y)
            where T : struct
        {
            return buffer.Span.Slice(y * buffer.Width, buffer.Width);
        }
    }
}