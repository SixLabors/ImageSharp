// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.Icc.Calculators
{
    internal partial class LutABCalculator
    {
        private enum CalculationType
        {
            AtoB = 0,
            BtoA = 1 << 2,

            SingleCurve = 0,
            CurveMatrix = 1,
            CurveClut = 2,
            Full = 3,
        }
    }
}
