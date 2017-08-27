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
        public readonly Rectangle Rectangle;

        public BufferArea(IBuffer2D<T> destinationBuffer, Rectangle rectangle)
        {
            Guard.MustBeGreaterThanOrEqualTo(rectangle.X, 0, nameof(rectangle));
            Guard.MustBeGreaterThanOrEqualTo(rectangle.Y, 0, nameof(rectangle));
            Guard.MustBeLessThan(rectangle.Width, destinationBuffer.Width, nameof(rectangle));
            Guard.MustBeLessThan(rectangle.Height, destinationBuffer.Height, nameof(rectangle));

            this.DestinationBuffer = destinationBuffer;
            this.Rectangle = rectangle;
        }

        public BufferArea(IBuffer2D<T> destinationBuffer)
            : this(destinationBuffer, destinationBuffer.FullRectangle())
        {
        }

        public IBuffer2D<T> DestinationBuffer { get; }

        public Size Size => this.Rectangle.Size;

        public ref T this[int x, int y] => ref this.DestinationBuffer.Span[this.GetIndexOf(x, y)];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> GetRowSpan(int y)
        {
            int yy = this.GetRowIndex(y);
            int xx = this.Rectangle.X;
            int width = this.Rectangle.Width;

            return this.DestinationBuffer.Span.Slice(yy + xx, width);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BufferArea<T> GetSubArea(int x, int y, int width, int height)
        {
            var rectangle = new Rectangle(x, y, width, height);
            return this.GetSubArea(rectangle);
        }

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