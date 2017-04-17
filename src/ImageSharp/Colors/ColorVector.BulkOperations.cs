namespace ImageSharp
{
    using System.Numerics;

    /// <summary>
    /// Unpacked pixel type containing four 16-bit unsigned normalized values typically ranging from 0 to 1.
    /// The color components are stored in red, green, blue, and alpha order.
    /// </summary>
    /// <remarks>
    /// This struct is fully mutable. This is done (against the guidelines) for the sake of performance,
    /// as it avoids the need to create new values for modification operations.
    /// </remarks>
    public partial struct ColorVector
    {
        /// <summary>
        /// <see cref="BulkPixelOperations{TColor}"/> implementation optimized for <see cref="ColorVector"/>.
        /// </summary>
        internal class BulkOperations : BulkPixelOperations<ColorVector>
        {
            /// <inheritdoc />
            internal override unsafe void ToVector4(BufferSpan<ColorVector> sourceColors, BufferSpan<Vector4> destVectors, int count)
            {
                BufferSpan.Copy(sourceColors.AsBytes(), destVectors.AsBytes(), count * sizeof(Vector4));
            }
        }
    }
}