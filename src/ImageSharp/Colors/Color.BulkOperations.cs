namespace ImageSharp
{
    using System;
    using System.Numerics;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Unpacked pixel type containing four 16-bit unsigned normalized values ranging from 0 to 1.
    /// The color components are stored in red, green, blue, and alpha order.
    /// </summary>
    /// <remarks>
    /// This struct is fully mutable. This is done (against the guidelines) for the sake of performance,
    /// as it avoids the need to create new values for modification operations.
    /// </remarks>
    public partial struct Color
    {
        /// <summary>
        /// <see cref="BulkPixelOperations{TColor}"/> implementation optimized for <see cref="Color"/>.
        /// </summary>
        internal class BulkOperations : BulkPixelOperations<Color>
        {
            /// <inheritdoc />
            internal override void ToVector4(BufferSpan<Color> sourceColors, BufferSpan<Vector4> destVectors, int count)
            {
                ref Vector4 sourceRef = ref Unsafe.As<Color, Vector4>(ref sourceColors.DangerousGetPinnableReference());
                ref Vector4 destRef = ref destVectors.DangerousGetPinnableReference();

                for (int i = 0; i < count; i++)
                {
                    ref Vector4 sp = ref Unsafe.Add(ref sourceRef, i);
                    ref Vector4 dp = ref Unsafe.Add(ref destRef, i);
                    dp = sp;
                }
            }
        }
    }
}