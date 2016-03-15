namespace ImageProcessorCore.Formats.Jpg
{
    using System;
    using System.IO;

    internal static class Colors
    {
        public static void RGBToYCbCr(byte r, byte g, byte b, out byte yy, out byte cb, out byte cr)
        {
            // The JFIF specification says:
            //  Y' =  0.2990*R + 0.5870*G + 0.1140*B
            //  Cb = -0.1687*R - 0.3313*G + 0.5000*B + 128
            //  Cr =  0.5000*R - 0.4187*G - 0.0813*B + 128
            // http://www.w3.org/Graphics/JPEG/jfif3.pdf says Y but means Y'.

            int iyy = (19595*r + 38470*g + 7471*b + (1<<15)) >> 16;
            int icb = (-11056*r - 21712*g + 32768*b + (257<<15)) >> 16;
            int icr = (32768*r - 27440*g - 5328*b + (257<<15)) >> 16;

            if (iyy < 0) yy = 0; else if (iyy > 255) yy = 255; else yy = (byte)iyy;
            if (icb < 0) cb = 0; else if (icb > 255) cb = 255; else cb = (byte)icb;
            if (icr < 0) cr = 0; else if (icr > 255) cr = 255; else cr = (byte)icr;
        }

        public static void YCbCrToRGB(byte yy, byte cb, byte cr, out byte r, out byte g, out byte b)
        {
            // The JFIF specification says:
            //  R = Y' + 1.40200*(Cr-128)
            //  G = Y' - 0.34414*(Cb-128) - 0.71414*(Cr-128)
            //  B = Y' + 1.77200*(Cb-128)
            // http://www.w3.org/Graphics/JPEG/jfif3.pdf says Y but means Y'.

            int yy1 = yy * 0x10100; // Convert 0x12 to 0x121200.
            int cb1 = cb - 128;
            int cr1 = cr - 128;
            int ir = (yy1 + 91881*cr1) >> 16;
            int ig = (yy1 - 22554*cb1 - 46802*cr1) >> 16;
            int ib = (yy1 + 116130*cb1) >> 16;

            if (ir < 0) r = 0; else if (ir > 255) r = 255; else r = (byte)ir;
            if (ig < 0) g = 0; else if (ig > 255) g = 255; else g = (byte)ig;
            if (ib < 0) b = 0; else if (ib > 255) b = 255; else b = (byte)ib;
        }
    }
}
