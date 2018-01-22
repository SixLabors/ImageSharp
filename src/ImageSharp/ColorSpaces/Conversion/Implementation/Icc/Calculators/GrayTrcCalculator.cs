// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.MetaData.Profiles.Icc;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.Icc.Calculators
{
    internal class GrayTrcCalculator : IVector4Calculator
    {
        private TrcCalculator calculator;

        public GrayTrcCalculator(IccTagDataEntry grayTrc, bool toPcs)
        {
            this.calculator = new TrcCalculator(new IccTagDataEntry[] { grayTrc }, !toPcs);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 Calculate(Vector4 value)
        {
            return this.calculator.Calculate(value);
        }
    }
}
