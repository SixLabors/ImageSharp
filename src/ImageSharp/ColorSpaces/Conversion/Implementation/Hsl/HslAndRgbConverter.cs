// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.HslColorSapce
{
    /// <summary>
    /// Color converter between HSL and Rgb
    /// See <see href="http://www.poynton.com/PDFs/coloureq.pdf"/> for formulas.
    /// </summary>
    internal class HslAndRgbConverter : IColorConversion<Hsl, Rgb>, IColorConversion<Rgb, Hsl>
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Rgb Convert(in Hsl input)
        {
            float rangedH = input.H / 360F;
            float r = 0;
            float g = 0;
            float b = 0;
            float s = input.S;
            float l = input.L;

            if (MathF.Abs(l) > Constants.Epsilon)
            {
                if (MathF.Abs(s) < Constants.Epsilon)
                {
                    r = g = b = l;
                }
                else
                {
                    float temp2 = (l < .5F) ? l * (1F + s) : l + s - (l * s);
                    float temp1 = (2F * l) - temp2;

                    r = GetColorComponent(temp1, temp2, rangedH + 0.3333333F);
                    g = GetColorComponent(temp1, temp2, rangedH);
                    b = GetColorComponent(temp1, temp2, rangedH - 0.3333333F);
                }
            }

            return new Rgb(r, g, b);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Hsl Convert(in Rgb input)
        {
            float r = input.R;
            float g = input.G;
            float b = input.B;

            float max = MathF.Max(r, MathF.Max(g, b));
            float min = MathF.Min(r, MathF.Min(g, b));
            float chroma = max - min;
            float h = 0F;
            float s = 0F;
            float l = (max + min) / 2F;

            if (MathF.Abs(chroma) < Constants.Epsilon)
            {
                return new Hsl(0F, s, l);
            }

            if (MathF.Abs(r - max) < Constants.Epsilon)
            {
                h = (g - b) / chroma;
            }
            else if (MathF.Abs(g - max) < Constants.Epsilon)
            {
                h = 2F + ((b - r) / chroma);
            }
            else if (MathF.Abs(b - max) < Constants.Epsilon)
            {
                h = 4F + ((r - g) / chroma);
            }

            h *= 60F;
            if (h < 0F)
            {
                h += 360F;
            }

            if (l <= .5F)
            {
                s = chroma / (max + min);
            }
            else
            {
                s = chroma / (2F - chroma);
            }

            return new Hsl(h, s, l);
        }

        /// <summary>
        /// Gets the color component from the given values.
        /// </summary>
        /// <param name="first">The first value.</param>
        /// <param name="second">The second value.</param>
        /// <param name="third">The third value.</param>
        /// <returns>
        /// The <see cref="float"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float GetColorComponent(float first, float second, float third)
        {
            third = MoveIntoRange(third);
            if (third < 0.1666667F)
            {
                return first + ((second - first) * 6F * third);
            }

            if (third < .5F)
            {
                return second;
            }

            if (third < 0.6666667F)
            {
                return first + ((second - first) * (0.6666667F - third) * 6F);
            }

            return first;
        }

        /// <summary>
        /// Moves the specific value within the acceptable range for
        /// conversion.
        /// <remarks>Used for converting <see cref="Hsl"/> colors to this type.</remarks>
        /// </summary>
        /// <param name="value">The value to shift.</param>
        /// <returns>
        /// The <see cref="float"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float MoveIntoRange(float value)
        {
            if (value < 0F)
            {
                value += 1F;
            }
            else if (value > 1F)
            {
                value -= 1F;
            }

            return value;
        }
    }
}