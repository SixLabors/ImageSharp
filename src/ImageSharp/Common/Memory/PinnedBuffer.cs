namespace ImageSharp
{
    using System;
    using System.Buffers;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Manages a pinned buffer of value type data 'T' as a Disposable resource.
    /// The backing array is either pooled or comes from the outside.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    internal class PinnedBuffer<T> : IDisposable
        where T : struct
    {
        /// <summary>
        /// A handle that allows to access the managed <see cref="Array"/> as an unmanaged memory by pinning.
        /// </summary>
        private GCHandle handle;

        /// <summary>
        /// A value indicating wether this <see cref="PinnedBuffer{T}"/> instance should return the array to the pool.
        /// </summary>
        private bool isPoolingOwner;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="PinnedBuffer{T}"/> class.
        /// </summary>
        /// <param name="count">The desired count of elements. (Minimum size for <see cref="Array"/>)</param>
        public PinnedBuffer(int count)
        {
            this.Count = count;
            this.Array = PixelDataPool<T>.Rent(count);
            this.isPoolingOwner = true;
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
        /// Initializes a new instance of the <see cref="PinnedBuffer{T}"/> class.
        /// </summary>
        /// <param name="count">The count of "relevant" elements in 'array'.</param>
        /// <param name="array">The array to pin.</param>
        public PinnedBuffer(int count, T[] array)
        {
            if (array.Length < count)
            {
                throw new ArgumentException("Can't initialize a PinnedBuffer with array.Length < count", nameof(array));
            }
            this.Count = count;
            this.Array = array;
            this.Pin();
        }

        ~PinnedBuffer()
        {
            this.UnPin();
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="PinnedBuffer{T}"/> instance is disposed, or has lost ownership of <see cref="Array"/>.
        /// </summary>
        public bool IsDisposedOrLostArrayOwnership { get; private set; }


        /// <summary>
        /// Gets the count of "relevant" elements. Usually be smaller than 'Array.Length' when <see cref="Array"/> is pooled.
        /// </summary>
        public int Count { get; private set; }

        /// <summary>
        /// The (pinned) array of elements.
        /// </summary>
        public T[] Array { get; private set; }

        /// <summary>
        /// Gets a pointer to the pinned <see cref="Array"/>.
        /// </summary>
        public IntPtr Pointer { get; private set; }

        /// <summary>
        /// Disposes the <see cref="PinnedBuffer{T}"/> instance by unpinning the array, and returning the pooled buffer when necessary.
        /// </summary>
        public void Dispose()
        {
            if (this.IsDisposedOrLostArrayOwnership)
            {
                return;
            }
            this.IsDisposedOrLostArrayOwnership = true;
            this.UnPin();

            if (this.isPoolingOwner)
            {
                PixelDataPool<T>.Return(this.Array);
            }

            this.Array = null;
            this.Count = 0;

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Unpins <see cref="Array"/> and makes the object "quasi-disposed" so the array is no longer owned by this object.
        /// If <see cref="Array"/> is rented, it's the callers responsibility to return it to it's pool. (Most likely <see cref="PixelDataPool{T}"/>)
        /// </summary>
        /// <returns>The unpinned <see cref="Array"/></returns>
        public T[] UnPinAndTakeArrayOwnership()
        {
            if (this.IsDisposedOrLostArrayOwnership)
            {
                throw new InvalidOperationException("UnPinAndTakeArrayOwnership() is invalid: either PinnedBuffer<T> is disposed or UnPinAndTakeArrayOwnership() has been called multiple times!");
            }

            this.IsDisposedOrLostArrayOwnership = true;
            this.UnPin();
            T[] array = this.Array;
            this.Array = null;
            return array;
        }

        /// <summary>
        /// Pins <see cref="Array"/>.
        /// </summary>
        private void Pin()
        {
            this.handle = GCHandle.Alloc(this.Array, GCHandleType.Pinned);
            this.Pointer = this.handle.AddrOfPinnedObject();
        }

        /// <summary>
        /// Unpins <see cref="Array"/>.
        /// </summary>
        private void UnPin()
        {
            if (this.Pointer == IntPtr.Zero || !this.handle.IsAllocated)
            {
                return;
            }
            this.handle.Free();
            this.Pointer = IntPtr.Zero;
        }
    }
}