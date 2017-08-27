using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Represents a rectangular area inside a 2D memory buffer. (Most commonly <see cref="Buffer2D{T}"/>)
    /// This type is kind-of 2D Span.
    /// </summary>
    /// <typeparam name="T">The element type</typeparam>
    internal struct BufferArea2D<T>
        where T : struct
    {
        public IBuffer2D<T> DestinationBuffer { get; }

        public readonly Rectangle Rectangle;

        public BufferArea2D(IBuffer2D<T> destinationBuffer, Rectangle rectangle)
        {
            this.DestinationBuffer = destinationBuffer;
            this.Rectangle = rectangle;
        }

        public BufferArea2D(Buffer2D<T> destinationBuffer)
            : this(destinationBuffer, destinationBuffer.FullRectangle())
        {
        }

        public Size Size => this.Rectangle.Size;
    }
}