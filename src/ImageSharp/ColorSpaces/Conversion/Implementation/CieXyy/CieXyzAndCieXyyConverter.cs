// <copyright file="CieXyzAndCieXyyConverter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.ColorSpaces.Conversion.Implementation.CieXyy
{
    using System.Runtime.CompilerServices;

    using ImageSharp.ColorSpaces;

    /// <summary>
    /// Color converter between CIE XYZ and CIE xyY
    /// <see href="http://www.brucelindbloom.com/"/> for formulas.
    /// </summary>
    internal class CieXyzAndCieXyyConverter : IColorConversion<CieXyz, CieXyy>, IColorConversion<CieXyy, CieXyz>
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CieXyy Convert(CieXyz input)
        {
            DebugGuard.NotNull(input, nameof(input));

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
        public CieXyz Convert(CieXyy input)
        {
            DebugGuard.NotNull(input, nameof(input));

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