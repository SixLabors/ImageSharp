using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageProcessor.Imaging.Colors
{
    using System.Drawing;

    using ImageProcessor.Common.Extensions;

    internal static class ColorExtensions
    {
        public static Color Add(this Color color, params Color[] colors)
        {
            int red = color.A > 0 ? color.R : 0;
            int green = color.A > 0 ? color.G : 0;
            int blue = color.A > 0 ? color.B : 0;
            int alpha = color.A;

            int counter = 0;
            foreach (Color addColor in colors)
            {
                if (addColor.A > 0)
                {
                    counter += 1;
                    red += addColor.R;
                    green += addColor.G;
                    blue += addColor.B;
                    alpha += addColor.A;
                }
            }

            counter = Math.Max(1, counter);

            return Color.FromArgb((alpha / counter).ToByte(), (red / counter).ToByte(), (green / counter).ToByte(), (blue / counter).ToByte());
        }

        public static CmykColor AddAsCmykColor(this Color color, params Color[] colors)
        {
            CmykColor cmyk = color;
            float c = color.A > 0 ? cmyk.C : 0;
            float m = color.A > 0 ? cmyk.M : 0;
            float y = color.A > 0 ? cmyk.Y : 0;
            float k = color.A > 0 ? cmyk.K : 0;

            foreach (Color addColor in colors)
            {
                if (addColor.A > 0)
                {
                    CmykColor cmykAdd = addColor;
                    c += cmykAdd.C;
                    m += cmykAdd.M;
                    y += cmykAdd.Y;
                    k += cmykAdd.K;
                }
            }

            //c = Math.Max(0.0f, Math.Min(100f, c));
            //m = Math.Max(0.0f, Math.Min(100f, m));
            //y = Math.Max(0.0f, Math.Min(100f, y));
            //k = Math.Max(0.0f, Math.Min(100f, k));

            return CmykColor.FromCmykColor(c, m, y, k);
        }
    }
}
