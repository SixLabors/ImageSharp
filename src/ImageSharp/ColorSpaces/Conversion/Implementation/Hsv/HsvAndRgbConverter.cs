// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.HsvColorSapce
{
    /// <summary>
    /// Color converter between HSV and Rgb
    /// See <see href="http://www.poynton.com/PDFs/coloureq.pdf"/> for formulas.
    /// </summary>
    internal class HsvAndRgbConverter : IColorConversion<Hsv, Rgb>, IColorConversion<Rgb, Hsv>
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Rgb Convert(in Hsv input)
        {
            float s = input.S;
            float v = input.V;

            if (MathF.Abs(s) < Constants.Epsilon)
            {
                return new Rgb(v, v, v);
            }

            float h = (MathF.Abs(input.H - 360) < Constants.Epsilon) ? 0 : input.H / 60;
            int i = (int)Math.Truncate(h);
            float f = h - i;

            float p = v * (1F - s);
            float q = v * (1F - (s * f));
            float t = v * (1F - (s * (1F - f)));

            float r, g, b;
            switch (i)
            {
                case 0:
                    r = v;
                    g = t;
                    b = p;
                    break;

                case 1:
                    r = q;
                    g = v;
                    b = p;
                    break;

                case 2:
                    r = p;
                    g = v;
                    b = t;
                    break;

                case 3:
                    r = p;
                    g = q;
                    b = v;
                    break;

                case 4:
                    r = t;
                    g = p;
                    b = v;
                    break;

                default:
                    r = v;
                    g = p;
                    b = q;
                    break;
            }

            return new Rgb(r, g, b);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Hsv Convert(in Rgb input)
        {
            float r = input.R;
            float g = input.G;
            float b = input.B;

            float max = MathF.Max(r, MathF.Max(g, b));
            float min = MathF.Min(r, MathF.Min(g, b));
            float chroma = max - min;
            float h = 0;
            float s = 0;
            float v = max;

            if (MathF.Abs(chroma) < Constants.Epsilon)
            {
                return new Hsv(0, s, v);
            }

            if (MathF.Abs(r - max) < Constants.Epsilon)
            {
                h = (g - b) / chroma;
            }
            else if (MathF.Abs(g - max) < Constants.Epsilon)
            {
                h = 2 + ((b - r) / chroma);
            }
            else if (MathF.Abs(b - max) < Constants.Epsilon)
            {
                h = 4 + ((r - g) / chroma);
            }

            h *= 60;
            if (h < 0.0)
            {
                h += 360;
            }

            s = chroma / v;

            return new Hsv(h, s, v);
        }
    }
}