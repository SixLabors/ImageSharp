// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.YCbCrColorSapce
{
    /// <summary>
    /// Color converter between YCbCr and Rgb
    /// See <see href="https://en.wikipedia.org/wiki/YCbCr#JPEG_conversion"/> for formulas.
    /// </summary>
    internal class YCbCrAndRgbConverter : IColorConversion<YCbCr, Rgb>, IColorConversion<Rgb, YCbCr>
    {
        private static readonly Vector3 MaxBytes = new Vector3(255F);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public YCbCr Convert(in Rgb input)
        {
            Vector3 rgb = input.Vector * MaxBytes;
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