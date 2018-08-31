// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.HunterLabColorSapce
{
    /// <summary>
    /// Color converter between CieXyz and HunterLab
    /// </summary>
    internal class CieXyzToHunterLabConverter : CieXyzAndHunterLabConverterBase, IColorConversion<CieXyz, HunterLab>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CieXyzToHunterLabConverter"/> class.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CieXyzToHunterLabConverter()
            : this(HunterLab.DefaultWhitePoint)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CieXyzToHunterLabConverter"/> class.
        /// </summary>
        /// <param name="labWhitePoint">The hunter Lab white point.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CieXyzToHunterLabConverter(CieXyz labWhitePoint)
        {
            this.HunterLabWhitePoint = labWhitePoint;
        }

        /// <summary>
        /// Gets the target reference white. When not set, <see cref="HunterLab.DefaultWhitePoint"/> is used.
        /// </summary>
        public CieXyz HunterLabWhitePoint { get; }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public HunterLab Convert(in CieXyz input)
        {
            // Conversion algorithm described here: http://en.wikipedia.org/wiki/Lab_color_space#Hunter_Lab
            float x = input.X, y = input.Y, z = input.Z;
            float xn = this.HunterLabWhitePoint.X, yn = this.HunterLabWhitePoint.Y, zn = this.HunterLabWhitePoint.Z;

            float ka = ComputeKa(this.HunterLabWhitePoint);
            float kb = ComputeKb(this.HunterLabWhitePoint);

            float l = 100 * MathF.Sqrt(y / yn);
            float a = ka * (((x / xn) - (y / yn)) / MathF.Sqrt(y / yn));
            float b = kb * (((y / yn) - (z / zn)) / MathF.Sqrt(y / yn));

            if (float.IsNaN(a))
            {
                a = 0;
            }

            if (float.IsNaN(b))
            {
                b = 0;
            }

            return new HunterLab(l, a, b, this.HunterLabWhitePoint);
        }
    }
}