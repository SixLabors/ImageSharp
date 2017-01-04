// <copyright file="MutableSpan.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats.Jpg
{
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Like corefxlab Span, but with an AddOffset() method for efficiency.
    /// TODO: When Span will be official, consider replacing this class!
    /// </summary>
    /// <see>
    ///     <cref>https://github.com/dotnet/corefxlab/blob/master/src/System.Slices/System/Span.cs</cref>
    /// </see>
    /// <typeparam name="T">The type of the data in the span</typeparam>
    internal struct MutableSpan<T>
    {
        /// <summary>
        /// Data
        /// </summary>
        public T[] Data;

        /// <summary>
        /// Offset
        /// </summary>
        public int Offset;

        /// <summary>
        /// Initializes a new instance of the <see cref="MutableSpan{T}"/> struct.
        /// </summary>
        /// <param name="size">The size of the span</param>
        /// <param name="offset">The offset (defaults to 0)</param>
        public MutableSpan(int size, int offset = 0)
        {
            this.Data = new T[size];
            this.Offset = offset;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MutableSpan{T}"/> struct.
        /// </summary>
        /// <param name="data">The data</param>
        /// <param name="offset">The offset (defaults to 0)</param>
        public MutableSpan(T[] data, int offset = 0)
        {
            this.Data = data;
            this.Offset = offset;
        }

        /// <summary>
        /// Gets the total count of data
        /// </summary>
        public int TotalCount => this.Data.Length - this.Offset;

        /// <summary>
        /// Index into the data
        /// </summary>
        /// <param name="idx">The data</param>
        /// <returns>The value at the specified index</returns>
        public T this[int idx]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return this.Data[idx + this.Offset];
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                this.Data[idx + this.Offset] = value;
            }
        }

        public static implicit operator MutableSpan<T>(T[] data) => new MutableSpan<T>(data, 0);

        /// <summary>
        /// Slice the data
        /// </summary>
        /// <param name="offset">The offset</param>
        /// <returns>The new <see cref="MutableSpan{T}"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MutableSpan<T> Slice(int offset)
        {
            return new MutableSpan<T>(this.Data, this.Offset + offset);
        }

        /// <summary>
        /// Add to the offset
        /// </summary>
        /// <param name="offset">The additional offset</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddOffset(int offset)
        {
            this.Offset += offset;
        }
    }
}