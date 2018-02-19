using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Temporal workaround providing a "Buffer" based on a generic array without the 'Unsafe.As()' hackery.
    /// </summary>
    internal class FakeBuffer<T> : IBuffer<T>
        where T : struct
    {
        public FakeBuffer(T[] array)
        {
            this.Array = array;
        }

        public T[] Array { get; }

        public Span<T> Span => this.Array;

        public int Length => this.Array.Length;

        /// <summary>
        /// Returns a reference to specified element of the buffer.
        /// </summary>
        /// <param name="index">The index</param>
        /// <returns>The reference to the specified element</returns>
        public ref T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                DebugGuard.MustBeLessThan(index, this.Length, nameof(index));

                Span<T> span = this.Span;
                return ref span[index];
            }
        }

        /// <summary>
        /// Converts <see cref="Buffer{T}"/> to an <see cref="ReadOnlySpan{T}"/>.
        /// </summary>
        /// <param name="buffer">The <see cref="Buffer{T}"/> to convert.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ReadOnlySpan<T>(FakeBuffer<T> buffer)
        {
            return new ReadOnlySpan<T>(buffer.Array, 0, buffer.Length);
        }

        /// <summary>
        /// Converts <see cref="Buffer{T}"/> to an <see cref="Span{T}"/>.
        /// </summary>
        /// <param name="buffer">The <see cref="Buffer{T}"/> to convert.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Span<T>(FakeBuffer<T> buffer)
        {
            return new Span<T>(buffer.Array, 0, buffer.Length);
        }

        public void Dispose()
        {
        }
    }
}