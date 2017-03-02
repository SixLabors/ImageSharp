namespace ImageSharp
{
    using System.Numerics;

    public unsafe class BulkPixelOperations<TColor>
        where TColor : struct, IPixel<TColor>
    {
        public static BulkPixelOperations<TColor> Instance { get; } = default(TColor).BulkOperations;

        internal virtual void PackFromVector4(
            ArrayPointer<Vector4> sourceVectors,
            ArrayPointer<TColor> destColors,
            int count)
        {
        }

        internal virtual void PackToVector4(
            ArrayPointer<TColor> sourceColors,
            ArrayPointer<Vector4> destVectors,
            int count)
        {
        }

        internal virtual void PackToXyzBytes(ArrayPointer<TColor> sourceColors, ArrayPointer<byte> destBytes, int count)
        {
        }

        internal virtual void PackFromXyzBytes(ArrayPointer<byte> sourceBytes, ArrayPointer<TColor> destColors, int count)
        {
        }

        internal virtual void PackToXyzwBytes(ArrayPointer<TColor> sourceColors, ArrayPointer<byte> destBytes, int count)
        {
        }

        internal virtual void PackFromXyzwBytes(ArrayPointer<byte> sourceBytes, ArrayPointer<TColor> destColors, int count)
        {
        }
        
        internal virtual void PackToZyxBytes(ArrayPointer<TColor> sourceColors, ArrayPointer<byte> destBytes, int count)
        {
        }

        internal virtual void PackFromZyxBytes(ArrayPointer<byte> sourceBytes, ArrayPointer<TColor> destColors, int count)
        {
        }

        internal virtual void PackToZyxwBytes(ArrayPointer<TColor> sourceColors, ArrayPointer<byte> destBytes, int count)
        {
        }

        internal virtual void PackFromZyxwBytes(ArrayPointer<byte> sourceBytes, ArrayPointer<TColor> destColors, int count)
        {
        }
    }
}