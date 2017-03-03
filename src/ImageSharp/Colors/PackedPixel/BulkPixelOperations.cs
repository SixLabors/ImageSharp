namespace ImageSharp
{
    using System;
    using System.Numerics;
    using System.Runtime.CompilerServices;

    public unsafe class BulkPixelOperations<TColor>
        where TColor : struct, IPixel<TColor>
    {
        public static BulkPixelOperations<TColor> Instance { get; } = default(TColor).BulkOperations;
        
        private static readonly int ColorSize = Unsafe.SizeOf<TColor>();

        internal virtual void PackFromVector4(
            ArrayPointer<Vector4> sourceVectors,
            ArrayPointer<TColor> destColors,
            int count)
        {
            Vector4* sp = (Vector4*)sourceVectors.PointerAtOffset;
            byte* dp = (byte*)destColors;

            for (int i = 0; i < count; i++)
            {
                Vector4 v = Unsafe.Read<Vector4>(sp);
                TColor c = default(TColor);
                c.PackFromVector4(v);
                Unsafe.Write(dp, c);

                sp++;
                dp += ColorSize;
            }
        }

        internal virtual void PackToVector4(
            ArrayPointer<TColor> sourceColors,
            ArrayPointer<Vector4> destVectors,
            int count)
        {
            byte* sp = (byte*)sourceColors;
            Vector4* dp = (Vector4*)destVectors.PointerAtOffset;

            for (int i = 0; i < count; i++)
            {
                TColor c = Unsafe.Read<TColor>(sp);
                *dp = c.ToVector4();
                sp += ColorSize;
                dp++;
            }
        }

        internal virtual void PackFromXyzBytes(
            ArrayPointer<byte> sourceBytes, 
            ArrayPointer<TColor> destColors, 
            int count)
        {
            byte* sp = (byte*)sourceBytes;
            byte* dp = (byte*)destColors.PointerAtOffset;

            for (int i = 0; i < count; i++)
            {
                TColor c = default(TColor);
                c.PackFromBytes(sp[0], sp[1], sp[2], 255);
                Unsafe.Write(dp, c);
                sp += 3;
                dp += ColorSize;
            }
        }

        internal virtual void PackToXyzBytes(
            ArrayPointer<TColor> sourceColors, 
            ArrayPointer<byte> destBytes, int count)
        {
            byte* sp = (byte*)sourceColors;

            byte[] dest = destBytes.Array;

            for (int i = destBytes.Offset; i < destBytes.Offset + count*3; i+=3)
            {
                TColor c = Unsafe.Read<TColor>(sp);
                c.ToXyzBytes(dest, i);
                sp += ColorSize;
            }
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