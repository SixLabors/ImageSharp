// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Icc;

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
