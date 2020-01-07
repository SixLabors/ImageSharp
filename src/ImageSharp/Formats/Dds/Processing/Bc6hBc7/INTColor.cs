using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace SixLabors.ImageSharp.Formats.Dds.Processing.Bc6hBc7
{
    internal struct INTEndPntPair
    {
        public INTColor A;
        public INTColor B;

        public INTEndPntPair(INTColor a, INTColor b)
        {
            A = a;
            B = b;
        }
    };

    internal class INTColor
    {
        public int r, g, b;
        public int pad;

        public INTColor()
        {

        }
        public INTColor(int nr, int ng, int nb)
        {
            r = nr;
            g = ng;
            b = nb;
            pad = 0;
        }

        public static INTColor operator +(INTColor a, INTColor c)
        {
            a.r += c.r;
            a.g += c.g;
            a.b += c.b;
            return a;
        }

        public static INTColor operator &(INTColor a, INTColor c)
        {
            a.r &= c.r;
            a.g &= c.g;
            a.b &= c.b;
            return a;
        }

        public INTColor SignExtend(LDRColorA Prec)
        {
            r = SIGN_EXTEND(r, Prec.r);
            g = SIGN_EXTEND(g, Prec.g);
            b = SIGN_EXTEND(b, Prec.b);
            return this;
        }

        private static int SIGN_EXTEND(int x, int nb)
        {
            return ((x & 1 << nb - 1) != 0 ? ~0 ^ (1 << nb) - 1 : 0) | x;
        }

        public void ToF16(ushort[] aF16, bool bSigned)
        {
            aF16[0] = INT2F16(r, bSigned);
            aF16[1] = INT2F16(g, bSigned);
            aF16[2] = INT2F16(b, bSigned);
        }

        private static ushort INT2F16(int input, bool bSigned)
        {
            ushort res;
            if (bSigned)
            {
                int s = 0;
                if (input < 0)
                {
                    s = Constants.F16S_MASK;
                    input = -input;
                }
                res = (ushort)(s | input);
            }
            else
            {
                Debug.Assert(input >= 0 && input <= Constants.F16MAX);
                res = (ushort)input;
            }

            return res;
        }
    }
}
