using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Exposes an array through the <see cref="IBuffer{T}"/> interface.
    /// </summary>
    internal class BasicArrayBuffer<T> : IBuffer<T>
        where T : struct
    {
        public BasicArrayBuffer(T[] array, int length)
        {
            DebugGuard.MustBeLessThanOrEqualTo(length, array.Length, nameof(length));
            this.Array = array;
            this.Length = length;
        }

        public BasicArrayBuffer(T[] array)
            : this(array, array.Length)
        {
        }

        public T[] Array { get; }

        public int Length { get; }

        public Span<T> Span => new Span<T>(this.Array, 0, this.Length);

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

        public void Dispose()
        {
        }
    }
}