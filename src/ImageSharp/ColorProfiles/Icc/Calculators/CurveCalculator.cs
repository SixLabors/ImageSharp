// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using SixLabors.ImageSharp.ColorProfiles.Icc.Calculators;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.ColorProfiles.Conversion.Icc;

internal partial class CurveCalculator : ISingleCalculator
{
    private readonly LutCalculator? lutCalculator;
    private readonly float gamma;
    private readonly CalculationType type;

    public CurveCalculator(IccCurveTagDataEntry entry, bool inverted)
    {
        if (entry.IsIdentityResponse)
        {
            this.type = CalculationType.Identity;
        }
        else if (entry.IsGamma)
        {
            this.gamma = entry.Gamma;
            if (inverted)
            {
                this.gamma = 1f / this.gamma;
            }

            this.type = CalculationType.Gamma;
        }
        else
        {
            this.lutCalculator = new LutCalculator(entry.CurveData, inverted);
            this.type = CalculationType.Lut;
        }
    }

    [MemberNotNullWhen(true, nameof(lutCalculator))]
    private bool IsLut => this.type == CalculationType.Lut;

    public float Calculate(float value)
    {
        if (this.IsLut)
        {
            return this.lutCalculator.Calculate(value);
        }

        if (this.type == CalculationType.Gamma)
        {
            return MathF.Pow(value, this.gamma);
        }

        if (this.type == CalculationType.Identity)
        {
            return value;
        }

        throw new InvalidOperationException("Invalid calculation type");
    }
}
