// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion
{
    /// <summary>
    /// Color converter between <see cref="Cmyk"/> and <see cref="Rgb"/>.
    /// </summary>
    internal static class CmykAndRgbConverter
    {
        /// <summary>
        /// Performs the conversion from the <see cref="Cmyk"/> input to an instance of <see cref="Rgb"/> type.
        /// </summary>
        /// <param name="input">The input color instance.</param>
        /// <returns>The converted result</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static Rgb Convert(in Cmyk input)
        {
            Vector3 rgb = (Vector3.One - new Vector3(input.C, input.M, input.Y)) * (Vector3.One - new Vector3(input.K));
            return new Rgb(rgb);
        }

        /// <summary>
        /// Performs the conversion from the <see cref="Rgb"/> input to an instance of <see cref="Cmyk"/> type.
        /// </summary>
        /// <param name="input">The input color instance.</param>
        /// <returns>The converted result.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static Cmyk Convert(in Rgb input)
        {
            // To CMY
            Vector3 cmy = Vector3.One - input.ToVector3();

            // To CMYK
            Vector3 k = new(MathF.Min(cmy.X, MathF.Min(cmy.Y, cmy.Z)));

            if (MathF.Abs(k.X - 1F) < Constants.Epsilon)
            {
                return new Cmyk(0, 0, 0, 1F);
            }

            cmy = (cmy - k) / (Vector3.One - k);

            return new Cmyk(cmy.X, cmy.Y, cmy.Z, k.X);
        }
    }
}
