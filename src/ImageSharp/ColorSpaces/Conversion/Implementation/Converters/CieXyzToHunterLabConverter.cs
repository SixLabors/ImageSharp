// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion
{
    /// <summary>
    /// Color converter between <see cref="CieXyz"/> and <see cref="HunterLab"/>
    /// </summary>
    internal sealed class CieXyzToHunterLabConverter : CieXyzAndHunterLabConverterBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CieXyzToHunterLabConverter"/> class.
        /// </summary>
        public CieXyzToHunterLabConverter()
            : this(HunterLab.DefaultWhitePoint)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CieXyzToHunterLabConverter"/> class.
        /// </summary>
        /// <param name="labWhitePoint">The hunter Lab white point.</param>
        public CieXyzToHunterLabConverter(CieXyz labWhitePoint) => this.HunterLabWhitePoint = labWhitePoint;

        /// <summary>
        /// Gets the target reference white. When not set, <see cref="HunterLab.DefaultWhitePoint"/> is used.
        /// </summary>
        public CieXyz HunterLabWhitePoint { get; }

        /// <summary>
        /// Performs the conversion from the <see cref="CieXyz"/> input to an instance of <see cref="HunterLab"/> type.
        /// </summary>
        /// <param name="input">The input color instance.</param>
        /// <returns>The converted result</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public HunterLab Convert(in CieXyz input)
        {
            // Conversion algorithm described here: http://en.wikipedia.org/wiki/Lab_color_space#Hunter_Lab
            float x = input.X, y = input.Y, z = input.Z;
            float xn = this.HunterLabWhitePoint.X, yn = this.HunterLabWhitePoint.Y, zn = this.HunterLabWhitePoint.Z;

            float ka = ComputeKa(this.HunterLabWhitePoint);
            float kb = ComputeKb(this.HunterLabWhitePoint);

            float yByYn = y / yn;
            float sqrtYbyYn = MathF.Sqrt(yByYn);
            float l = 100 * sqrtYbyYn;
            float a = ka * (((x / xn) - yByYn) / sqrtYbyYn);
            float b = kb * ((yByYn - (z / zn)) / sqrtYbyYn);

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