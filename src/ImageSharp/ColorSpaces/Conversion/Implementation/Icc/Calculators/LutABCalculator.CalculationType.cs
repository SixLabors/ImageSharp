// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Icc
{
    internal partial class LutABCalculator
    {
        private enum CalculationType
        {
            AtoB = 1 << 3,
            BtoA = 1 << 4,

            SingleCurve = 1,
            CurveMatrix = 2,
            CurveClut = 3,
            Full = 4,
        }
    }
}
