// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion
{
    /// <summary>
    /// Color converter between <see cref="YCbCr"/> and <see cref="Rgb"/>
    /// See <see href="https://en.wikipedia.org/wiki/YCbCr#JPEG_conversion"/> for formulas.
    /// </summary>
    internal sealed class YCbCrAndRgbConverter
    {
        private static readonly Vector3 MaxBytes = new Vector3(255F);

        /// <summary>
        /// Performs the conversion from the <see cref="YCbCr"/> input to an instance of <see cref="Rgb"/> type.
        /// </summary>
        /// <param name="input">The input color instance.</param>
        /// <returns>The converted result.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Rgb Convert(in YCbCr input)
        {
            float y = input.Y;
            float cb = input.Cb - 128F;
            float cr = input.Cr - 128F;

            float r = MathF.Round(y + (1.402F * cr), MidpointRounding.AwayFromZero);
            float g = MathF.Round(y - (0.344136F * cb) - (0.714136F * cr), MidpointRounding.AwayFromZero);
            float b = MathF.Round(y + (1.772F * cb), MidpointRounding.AwayFromZero);

            return new Rgb(new Vector3(r, g, b) / MaxBytes);
        }

        /// <summary>
        /// Performs the conversion from the <see cref="Rgb"/> input to an instance of <see cref="YCbCr"/> type.
        /// </summary>
        /// <param name="input">The input color instance.</param>
        /// <returns>The converted result.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public YCbCr Convert(in Rgb input)
        {
            Vector3 rgb = input.ToVector3() * MaxBytes;
            float r = rgb.X;
            float g = rgb.Y;
            float b = rgb.Z;

            float y = (0.299F * r) + (0.587F * g) + (0.114F * b);
            float cb = 128F + ((-0.168736F * r) - (0.331264F * g) + (0.5F * b));
            float cr = 128F + ((0.5F * r) - (0.418688F * g) - (0.081312F * b));

            return new YCbCr(y, cb, cr);
        }
    }
}