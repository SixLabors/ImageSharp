namespace nQuant
{
    using System.Runtime.InteropServices;

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
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Pixel"/> struct.
        /// </summary>
        /// <param name="argb">
        /// The combined color components.
        /// </param>
        public Pixel(int argb)
            : this()
        {
            this.Argb = argb;
        }

        public long Amplitude()
        {
            return (Alpha * Alpha) + (Red * Red) + (Green * Green) + (Blue * Blue);
        }

        [FieldOffsetAttribute(3)]
        public byte Alpha;
        [FieldOffsetAttribute(2)]
        public byte Red;
        [FieldOffsetAttribute(1)]
        public byte Green;
        [FieldOffsetAttribute(0)]
        public byte Blue;
        [FieldOffset(0)]
        public int Argb;

        public override string ToString()
        {
            return string.Format("Alpha:{0} Red:{1} Green:{2} Blue:{3}", Alpha, Red, Green, Blue);
        }
    }
}