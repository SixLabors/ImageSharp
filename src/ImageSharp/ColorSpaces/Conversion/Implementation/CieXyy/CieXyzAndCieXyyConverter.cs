// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.CieXyyColorSapce
{
    /// <summary>
    /// Color converter between CIE XYZ and CIE xyY
    /// <see href="http://www.brucelindbloom.com/"/> for formulas.
    /// </summary>
    internal class CieXyzAndCieXyyConverter : IColorConversion<CieXyz, CieXyy>, IColorConversion<CieXyy, CieXyz>
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CieXyy Convert(in CieXyz input)
        {
            float x = input.X / (input.X + input.Y + input.Z);
            float y = input.Y / (input.X + input.Y + input.Z);

            if (float.IsNaN(x) || float.IsNaN(y))
            {
                return new CieXyy(0, 0, input.Y);
            }

            return new CieXyy(x, y, input.Y);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CieXyz Convert(in CieXyy input)
        {
            if (MathF.Abs(input.Y) < Constants.Epsilon)
            {
                return new CieXyz(0, 0, input.Yl);
            }

            float x = (input.X * input.Yl) / input.Y;
            float y = input.Yl;
            float z = ((1 - input.X - input.Y) * y) / input.Y;

            return new CieXyz(x, y, z);
        }
    }
}