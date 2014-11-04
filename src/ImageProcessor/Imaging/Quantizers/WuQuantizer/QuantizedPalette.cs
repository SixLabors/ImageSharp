using System.Collections.Generic;
using System.Drawing;

namespace nQuant
{
    public class QuantizedPalette
    {
        public QuantizedPalette(int size)
        {
            Colors = new List<Color>();
            PixelIndex = new byte[size];
        }
        public IList<Color> Colors { get; private set; }

        public byte[] PixelIndex { get; private set; }
    }
}