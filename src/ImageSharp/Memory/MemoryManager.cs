using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Memory managers are used to allocate memory for image processing operations.
    /// </summary>
    public abstract class MemoryManager
    {
        /// <summary>
        /// Allocates a <see cref="Buffer{T}"/> of size <paramref name="size"/>, optionally
        /// clearing the buffer before it gets returned.
        /// </summary>
        /// <typeparam name="T">Type of the data stored in the buffer</typeparam>
        /// <param name="size">Size of the buffer to allocate</param>
        /// <param name="clear">True to clear the backing memory of the buffer</param>
        /// <returns>A buffer of values of type <typeparamref name="T"/>.</returns>
        internal abstract Buffer<T> Allocate<T>(int size, bool clear)
            where T : struct;

        /// <summary>
        /// Releases the memory allocated for <paramref name="buffer"/>. After this, the buffer
        /// is no longer usable.
        /// </summary>
        /// <typeparam name="T">Type of the data stored in the buffer</typeparam>
        /// <param name="buffer">The buffer to release</param>
        internal abstract void Release<T>(Buffer<T> buffer)
            where T : struct;
    }
}
