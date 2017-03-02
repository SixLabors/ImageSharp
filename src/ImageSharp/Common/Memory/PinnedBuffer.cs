namespace ImageSharp
{
    using System;
    using System.Buffers;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Manages a pinned buffer of 'T' as a Disposable resource.
    /// The backing array is either pooled or comes from the outside.
    /// TODO: Should replace the pinning/dispose logic in several classes like <see cref="PixelAccessor{TColor}"/> or <see cref="PixelArea{TColor}"/>!
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    internal class PinnedBuffer<T> : IDisposable
        where T : struct
    {
        private GCHandle handle;

        private bool isBufferRented;

        private bool isDisposed;

        /// <summary>
        /// TODO: Consider reusing functionality of <see cref="PixelPool{TColor}"/>
        /// </summary>
        private static readonly ArrayPool<T> ArrayPool = ArrayPool<T>.Create();

        /// <summary>
        /// Initializes a new instance of the <see cref="PinnedBuffer{T}"/> class.
        /// </summary>
        /// <param name="count">The desired count of elements. (Minimum size for <see cref="Array"/>)</param>
        public PinnedBuffer(int count)
        {
            this.Count = count;
            this.Array = ArrayPool.Rent(count);
            this.isBufferRented = true;
            this.Pin();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PinnedBuffer{T}"/> class.
        /// </summary>
        /// <param name="array">The array to pin.</param>
        public PinnedBuffer(T[] array)
        {
            this.Count = array.Length;
            this.Array = array;
            this.Pin();
        }

        /// <summary>
        /// The count of "relevant" elements. Usually be smaller than 'Array.Length' when <see cref="Array"/> is pooled.
        /// </summary>
        public int Count { get; private set; }

        /// <summary>
        /// The (pinned) array of elements.
        /// </summary>
        public T[] Array { get; private set; }

        /// <summary>
        /// Pointer to the pinned <see cref="Array"/>.
        /// </summary>
        public IntPtr Pointer { get; private set; }

        /// <summary>
        /// Disposes the <see cref="PinnedBuffer{T}"/> instance by unpinning the array, and returning the pooled buffer when necessary.
        /// </summary>
        public void Dispose()
        {
            if (this.isDisposed)
            {
                return;
            }
            this.isDisposed = true;
            this.UnPin();

            if (this.isBufferRented)
            {
                ArrayPool.Return(this.Array, true);
            }

            this.Array = null;
            this.Count = 0;

            GC.SuppressFinalize(this);
        }

        private void Pin()
        {
            this.handle = GCHandle.Alloc(this.Array, GCHandleType.Pinned);
            this.Pointer = this.handle.AddrOfPinnedObject();
        }

        private void UnPin()
        {
            if (this.Pointer == IntPtr.Zero || !this.handle.IsAllocated)
            {
                return;
            }
            this.handle.Free();
            this.Pointer = IntPtr.Zero;
        }

        ~PinnedBuffer()
        {
            this.UnPin();
        }
    }
}