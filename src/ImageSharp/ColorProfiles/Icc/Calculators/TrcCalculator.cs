// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.ColorProfiles.Conversion.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.ColorProfiles.Icc.Calculators;

internal class TrcCalculator : IVector4Calculator
{
    private readonly ISingleCalculator[] calculators;

    public TrcCalculator(IccTagDataEntry[] entries, bool inverted)
    {
        Guard.NotNull(entries, nameof(entries));

        this.calculators = new ISingleCalculator[entries.Length];
        for (int i = 0; i < entries.Length; i++)
        {
            this.calculators[i] = entries[i] switch
            {
                IccCurveTagDataEntry curve => new CurveCalculator(curve, inverted),
                IccParametricCurveTagDataEntry parametricCurve => new ParametricCurveCalculator(parametricCurve, inverted),
                _ => throw new InvalidIccProfileException("Invalid Entry."),
            };
        }
    }

    public unsafe Vector4 Calculate(Vector4 value)
    {
        ref float f = ref Unsafe.As<Vector4, float>(ref value);
        for (int i = 0; i < this.calculators.Length; i++)
        {
            Unsafe.Add(ref f, i) = this.calculators[i].Calculate(Unsafe.Add(ref f, i));
        }

        return value;
    }
}
