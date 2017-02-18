namespace ImageSharp
{
    using System;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Provides access to elements in an array from a given position. 
    /// This struct shares many similarities with corefx System.Span&lt;T&gt; but there are differences in functionalities and semantics:
    /// - It's not possible to use it with stack objects or pointers to unmanaged memory, only with managed arrays
    /// - There is no bounds checking for performance reasons. Therefore we don't need to store length. (However this could be added as DEBUG-only feature.)
    /// - Currently the arrays provided to ArrayPointer need to be pinned. This behaviour could be changed using C#7 features.
    /// </summary>
    internal unsafe struct ArrayPointer<T>
        where T : struct
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayPointer(T[] array, void* pointerToArray, int offset)
        {
            // TODO: Use Guard.NotNull() here after optimizing it with ThrowHelper!
            if (array == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(array));
            }
            this.Array = array;
            this.Offset = offset;
            this.PointerAtOffset = (IntPtr)pointerToArray + Unsafe.SizeOf<T>()*offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayPointer(T[] array, void* pointerToArray)
        {
            // TODO: Use Guard.NotNull() here after optimizing it with ThrowHelper!
            if (array == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(array));
            }
            this.Array = array;
            this.Offset = 0;
            this.PointerAtOffset = (IntPtr)pointerToArray;
        }

        public T[] Array { get; private set; }

        public int Offset { get; private set; }

        public IntPtr PointerAtOffset { get; private set; }

        public ArrayPointer<T> Slice(int offset)
        {
            ArrayPointer<T> result = default(ArrayPointer<T>);
            result.Array = this.Array;
            result.Offset = this.Offset + offset;
            result.PointerAtOffset = this.PointerAtOffset + Unsafe.SizeOf<T>() * offset;
            return result;
        }
    }
}