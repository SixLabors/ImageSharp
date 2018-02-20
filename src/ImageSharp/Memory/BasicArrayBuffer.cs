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
        public BasicArrayBuffer(T[] array)
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

        public void Dispose()
        {
        }
    }
}