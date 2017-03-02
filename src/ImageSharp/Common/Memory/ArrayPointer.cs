namespace ImageSharp
{
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Utility methods to <see cref="ArrayPointer{T}"/>
    /// </summary>
    internal static class ArrayPointer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void Copy<T>(ArrayPointer<T> source, ArrayPointer<T> destination, int count)
            where T : struct
        {
            Unsafe.CopyBlock((void*)source.PointerAtOffset, (void*)destination.PointerAtOffset, USizeOf<T>(count));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void Copy<T>(ArrayPointer<T> source, ArrayPointer<byte> destination, int countInSource)
            where T : struct
        {
            Unsafe.CopyBlock((void*)source.PointerAtOffset, (void*)destination.PointerAtOffset, USizeOf<T>(countInSource));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void Copy<T>(ArrayPointer<byte> source, ArrayPointer<T> destination, int countInDest)
            where T : struct
        {
            Unsafe.CopyBlock((void*)source.PointerAtOffset, (void*)destination.PointerAtOffset, USizeOf<T>(countInDest));
        }

        /// <summary>
        /// Gets the size of `count` elements in bytes.
        /// </summary>
        /// <param name="count">The count of the elements</param>
        /// <returns>The size in bytes as int</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SizeOf<T>(int count)
            where T : struct => Unsafe.SizeOf<T>() * count;

        /// <summary>
        /// Gets the size of `count` elements in bytes as UInt32
        /// </summary>
        /// <param name="count">The count of the elements</param>
        /// <returns>The size in bytes as UInt32</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint USizeOf<T>(int count)
            where T : struct
            => (uint)SizeOf<T>(count);
    }
}