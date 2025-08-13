// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using SixLabors.ImageSharp.ColorProfiles.Icc.Calculators;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.ColorProfiles.Conversion.Icc;

internal partial class CurveCalculator : ISingleCalculator
{
    private readonly LutCalculator lutCalculator;
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

    public float Calculate(float value)
        => this.type switch
        {
            CalculationType.Identity => value,
            CalculationType.Gamma => MathF.Pow(value, this.gamma), // TODO: This could be optimized using a LUT. See SrgbCompanding
            CalculationType.Lut => this.lutCalculator.Calculate(value),
            _ => throw new InvalidOperationException("Invalid calculation type"),
        };
}
