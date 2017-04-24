// <copyright file="YCbCrAndRgbConverter .cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.ColorSpaces.Conversion.Implementation.YCbCr
{
    using System;
    using System.Numerics;
    using System.Runtime.CompilerServices;

    using ImageSharp.ColorSpaces;

    /// <summary>
    /// Color converter between YCbCr and Rgb
    /// See <see href="https://en.wikipedia.org/wiki/YCbCr#JPEG_conversion"/> for formulas.
    /// </summary>
    internal class YCbCrAndRgbConverter : IColorConversion<YCbCr, Rgb>, IColorConversion<Rgb, YCbCr>
    {
        private static readonly Vector3 MaxBytes = new Vector3(255F);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Rgb Convert(YCbCr input)
        {
            DebugGuard.NotNull(input, nameof(input));

            float y = input.Y;
            float cb = input.Cb - 128F;
            float cr = input.Cr - 128F;


            return new Rgb(new Vector3(r, g, b) / MaxBytes);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public YCbCr Convert(Rgb input)
        {
            DebugGuard.NotNull(input, nameof(input));

            Vector3 rgb = input.Vector * MaxBytes;
            float r = rgb.X;
            float g = rgb.Y;
            float b = rgb.Z;

            float y = (0.299F * r) + (0.587F * g) + (0.114F * b);

            return new YCbCr(y, cb, cr);
        }
    }
}
