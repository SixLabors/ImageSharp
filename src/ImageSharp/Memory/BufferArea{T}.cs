using System;
using System.Runtime.CompilerServices;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Represents a rectangular area inside a 2D memory buffer (<see cref="Buffer2D{T}"/>).
    /// This type is kind-of 2D Span, but it can live on heap.
    /// </summary>
    /// <typeparam name="T">The element type</typeparam>
    internal struct BufferArea<T>
        where T : struct
    {
        /// <summary>
        /// The rectangle specifying the boundaries of the area in <see cref="DestinationBuffer"/>.
        /// </summary>
        public readonly Rectangle Rectangle;

        public BufferArea(IBuffer2D<T> destinationBuffer, Rectangle rectangle)
        {
            Guard.MustBeGreaterThanOrEqualTo(rectangle.X, 0, nameof(rectangle));
            Guard.MustBeGreaterThanOrEqualTo(rectangle.Y, 0, nameof(rectangle));
            Guard.MustBeLessThanOrEqualTo(rectangle.Width, destinationBuffer.Width, nameof(rectangle));
            Guard.MustBeLessThanOrEqualTo(rectangle.Height, destinationBuffer.Height, nameof(rectangle));

            this.DestinationBuffer = destinationBuffer;
            this.Rectangle = rectangle;
        }

        public BufferArea(IBuffer2D<T> destinationBuffer)
            : this(destinationBuffer, destinationBuffer.FullRectangle())
        {
        }

        /// <summary>
        /// Gets the <see cref="IBuffer2D{T}"/> being pointed by this instance.
        /// </summary>
        public IBuffer2D<T> DestinationBuffer { get; }

        /// <summary>
        /// Gets the size of the area.
        /// </summary>
        public Size Size => this.Rectangle.Size;

        /// <summary>
        /// Gets the pixel stride which is equal to the width of <see cref="DestinationBuffer"/>.
        /// </summary>
        public int Stride => this.DestinationBuffer.Width;

        /// <summary>
        /// Gets or sets a value at the given index.
        /// </summary>
        /// <param name="x">The position inside a row</param>
        /// <param name="y">The row index</param>
        /// <returns>The reference to the value</returns>
        public ref T this[int x, int y] => ref this.DestinationBuffer.Span[this.GetIndexOf(x, y)];

        /// <summary>
        /// Gets a reference to the [0,0] element.
        /// </summary>
        /// <returns>The reference to the [0,0] element</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T GetReferenceToOrigo() =>
            ref this.DestinationBuffer.Span[(this.Rectangle.Y * this.DestinationBuffer.Width) + this.Rectangle.X];

        /// <summary>
        /// Gets a span to row 'y' inside this area.
        /// </summary>
        /// <param name="y">The row index</param>
        /// <returns>The span</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> GetRowSpan(int y)
        {
            int yy = this.GetRowIndex(y);
            int xx = this.Rectangle.X;
            int width = this.Rectangle.Width;

            return this.DestinationBuffer.Span.Slice(yy + xx, width);
        }

        /// <summary>
        /// Returns a sub-area as <see cref="BufferArea{T}"/>. (Similar to <see cref="Span{T}.Slice(int, int)"/>.)
        /// </summary>
        /// <param name="x">The x index at the subarea origo</param>
        /// <param name="y">The y index at the subarea origo</param>
        /// <param name="width">The desired width of the subarea</param>
        /// <param name="height">The desired height of the subarea</param>
        /// <returns>The subarea</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BufferArea<T> GetSubArea(int x, int y, int width, int height)
        {
            var rectangle = new Rectangle(x, y, width, height);
            return this.GetSubArea(rectangle);
        }

        /// <summary>
        /// Returns a sub-area as <see cref="BufferArea{T}"/>. (Similar to <see cref="Span{T}.Slice(int, int)"/>.)
        /// </summary>
        /// <param name="rectangle">The <see cref="Rectangle"/> specifying the boundaries of the subarea</param>
        /// <returns>The subarea</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BufferArea<T> GetSubArea(Rectangle rectangle)
        {
            DebugGuard.MustBeLessThanOrEqualTo(rectangle.Width, this.Rectangle.Width, nameof(rectangle));
            DebugGuard.MustBeLessThanOrEqualTo(rectangle.Height, this.Rectangle.Height, nameof(rectangle));

            int x = this.Rectangle.X + rectangle.X;
            int y = this.Rectangle.Y + rectangle.Y;
            rectangle = new Rectangle(x, y, rectangle.Width, rectangle.Height);
            return new BufferArea<T>(this.DestinationBuffer, rectangle);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetIndexOf(int x, int y)
        {
            int yy = this.GetRowIndex(y);
            int xx = this.Rectangle.X + x;
            return yy + xx;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetRowIndex(int y)
        {
            return (y + this.Rectangle.Y) * this.DestinationBuffer.Width;
        }
    }
}