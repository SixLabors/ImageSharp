// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.ColorProfiles.Icc.Calculators;

internal class GrayTrcCalculator : IVector4Calculator
{
    private readonly TrcCalculator calculator;

    public GrayTrcCalculator(IccTagDataEntry grayTrc, bool toPcs)
        => this.calculator = new TrcCalculator([grayTrc], !toPcs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector4 Calculate(Vector4 value) => this.calculator.Calculate(value);
}
