using System.Diagnostics;
using System.Runtime.InteropServices;
namespace ImageProcessor.Imaging.Quantizers.WuQuantizer
{
    [StructLayout(LayoutKind.Explicit)]
    public struct Pixel
    {
        public Pixel(byte alpha, byte red, byte green, byte blue)
            : this()
        {
            Alpha = alpha;
            Red = red;
            Green = green;
            Blue = blue;

            Debug.Assert(Argb == (alpha << 24 | red << 16 | green << 8 | blue));
        }

        public Pixel(int argb)
            : this()
        {
            Argb = argb;
            Debug.Assert(Alpha == ((uint)argb >> 24));
            Debug.Assert(Red == ((uint)(argb >> 16) & 255));
            Debug.Assert(Green == ((uint)(argb >> 8) & 255));
            Debug.Assert(Blue == ((uint)argb & 255));
        }

        [FieldOffset(3)]
        public byte Alpha;
        [FieldOffset(2)]
        public byte Red;
        [FieldOffset(1)]
        public byte Green;
        [FieldOffset(0)]
        public byte Blue;
        [FieldOffset(0)]
        public int Argb;

        public override string ToString()
        {
            return string.Format("Alpha:{0} Red:{1} Green:{2} Blue:{3}", Alpha, Red, Green, Blue);
        }
    }
}