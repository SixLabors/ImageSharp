namespace ImageSharp
{
    using System.Numerics;

    public partial struct Color
    {
        internal class BulkOperations : BulkPixelOperations<Color>
        {
            private static readonly int VectorSize = Vector<uint>.Count;

            internal static void PackToVector4Aligned(
                ArrayPointer<Color> source,
                ArrayPointer<Vector4> destination,
                int count)
            {
                DebugGuard.IsTrue(
                    count % VectorSize == 0,
                    nameof(count),
                    "Argument 'count' should divisible by Vector<uint>.Count!");


            }
        }
    }
}