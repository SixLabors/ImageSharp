namespace GenericImage
{
    using System;
    using System.Runtime.InteropServices;

    using GenericImage.PackedVectors;

    /// <summary>
    /// Provides per-pixel access to an images pixels.
    /// </summary>
    public sealed unsafe class PixelAccessorRgba64 : IPixelAccessor
    {
        /// <summary>
        /// The position of the first pixel in the bitmap.
        /// </summary>
        private ulong* pixelsBase;

        /// <summary>
        /// Provides a way to access the pixels from unmanaged memory.
        /// </summary>
        private GCHandle pixelsHandle;

        /// <summary>
        /// A value indicating whether this instance of the given entity has been disposed.
        /// </summary>
        /// <value><see langword="true"/> if this instance has been disposed; otherwise, <see langword="false"/>.</value>
        /// <remarks>
        /// If the entity is disposed, it must not be disposed a second
        /// time. The isDisposed field is set the first time the entity
        /// is disposed. If the isDisposed field is true, then the Dispose()
        /// method will not dispose again. This help not to prolong the entity's
        /// life in the Garbage Collector.
        /// </remarks>
        private bool isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="PixelAccessorRgba64"/> class.
        /// </summary>
        /// <param name="image">
        /// The image to provide pixel access for.
        /// </param>
        public PixelAccessorRgba64(IImageBase<ulong> image)
        {
            //Guard.NotNull(image, nameof(image));
            //Guard.MustBeGreaterThan(image.Width, 0, "image width");
            //Guard.MustBeGreaterThan(image.Height, 0, "image height");

            int size = image.Pixels.Length;
            this.Width = image.Width;
            this.Height = image.Height;

            // Assign the pointer.
            // If buffer is allocated on Large Object Heap i.e > 85Kb, then we are going to pin it instead of making a copy.
            if (size > 87040)
            {
                this.pixelsHandle = GCHandle.Alloc(image.Pixels, GCHandleType.Pinned);
                this.pixelsBase = (ulong*)this.pixelsHandle.AddrOfPinnedObject().ToPointer();
            }
            else
            {
                fixed (ulong* pbuffer = image.Pixels)
                {
                    this.pixelsBase = pbuffer;
                }
            }
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="PixelAccessorRgba64"/> class. 
        /// </summary>
        ~PixelAccessorRgba64()
        {
            this.Dispose();
        }

        /// <summary>
        /// Gets the width of the image.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Gets the height of the image.
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Gets or sets the color of a pixel at the specified position.
        /// </summary>
        /// <param name="x">
        /// The x-coordinate of the pixel. Must be greater
        /// than zero and smaller than the width of the pixel.
        /// </param>
        /// <param name="y">
        /// The y-coordinate of the pixel. Must be greater
        /// than zero and smaller than the width of the pixel.
        /// </param>
        /// <returns>The <see cref="IPackedVector"/> at the specified position.</returns>
        public IPackedVector this[int x, int y]
        {
            get
            {
#if DEBUG
                if ((x < 0) || (x >= this.Width))
                {
                    throw new ArgumentOutOfRangeException(nameof(x), "Value cannot be less than zero or greater than the bitmap width.");
                }

                if ((y < 0) || (y >= this.Height))
                {
                    throw new ArgumentOutOfRangeException(nameof(y), "Value cannot be less than zero or greater than the bitmap height.");
                }
#endif
                return *((Rgba64*)(this.pixelsBase + (((y * this.Width) + x) * 4)));
            }

            set
            {
#if DEBUG
                if ((x < 0) || (x >= this.Width))
                {
                    throw new ArgumentOutOfRangeException(nameof(x), "Value cannot be less than zero or greater than the bitmap width.");
                }

                if ((y < 0) || (y >= this.Height))
                {
                    throw new ArgumentOutOfRangeException(nameof(y), "Value cannot be less than zero or greater than the bitmap height.");
                }
#endif
                *(Rgba64*)(this.pixelsBase + (((y * this.Width) + x) * 4)) = (Rgba64)value;
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (this.isDisposed)
            {
                return;
            }

            if (this.pixelsHandle.IsAllocated)
            {
                this.pixelsHandle.Free();
            }

            this.pixelsBase = null;

            // Note disposing is done.
            this.isDisposed = true;

            // This object will be cleaned up by the Dispose method.
            // Therefore, you should call GC.SuppressFinalize to
            // take this object off the finalization queue 
            // and prevent finalization code for this object
            // from executing a second time.
            GC.SuppressFinalize(this);
        }
    }
}
